import { describe, it, expect, vi } from "vitest";
import {
  askKeep,
  FALLBACK_LINE,
  DEFLECTION_LINE,
} from "../src/oracle.js";
import type { FactSheet, OracleClient } from "../src/types.js";

const FACT_SHEET: FactSheet = {
  language: "Spanish",
  country: "Mexico",
  continent: "North America",
  forbidden: ["spanish", "mexico", "mexican", "peso", "espanol"],
};

/** Fake client returning canned replies in sequence — no network. */
function fakeClient(replies: Array<string | Error>): OracleClient {
  const queue = [...replies];
  return {
    complete: vi.fn(async () => {
      const next = queue.shift();
      if (next instanceof Error) throw next;
      return next ?? "";
    }),
  };
}

describe("askKeep", () => {
  it("returns the model's answer on a clean (non-leaking) reply", async () => {
    const client = fakeClient(["Cold up north, and aye, they've a coastline, feathers."]);
    const answer = await askKeep("Is it near an ocean?", FACT_SHEET, client);
    expect(answer).toBe("Cold up north, and aye, they've a coastline, feathers.");
    expect(client.complete).toHaveBeenCalledTimes(1);
  });

  it("retries ONCE with a stricter reminder when the first reply leaks, then returns the clean retry", async () => {
    const client = fakeClient([
      "Aye, that's Spanish, you daft piggy.", // leaks -> triggers retry
      "It's an alphabet, not squiggles. Drink your ale.", // clean
    ]);
    const answer = await askKeep("Alphabet or characters?", FACT_SHEET, client);
    expect(answer).toBe("It's an alphabet, not squiggles. Drink your ale.");
    expect(client.complete).toHaveBeenCalledTimes(2);

    // The retry system prompt must carry the stricter reminder.
    const secondCallSystem = (client.complete as ReturnType<typeof vi.fn>).mock
      .calls[1]?.[0] as string;
    expect(secondCallSystem).toMatch(/STRICT REMINDER/);
  });

  it("deflects when the retry still leaks", async () => {
    const client = fakeClient([
      "That's Spanish.", // leak
      "Fine — it's Mexican, happy?", // still leaks
    ]);
    const answer = await askKeep("What is it?", FACT_SHEET, client);
    expect(answer).toBe(DEFLECTION_LINE);
    expect(client.complete).toHaveBeenCalledTimes(2);
  });

  it("returns the gruff fallback when the client throws (Anthropic failure)", async () => {
    const client = fakeClient([new Error("timeout")]);
    const answer = await askKeep("Near an ocean?", FACT_SHEET, client);
    expect(answer).toBe(FALLBACK_LINE);
  });

  it("returns the fallback when the RETRY call throws", async () => {
    const client = fakeClient(["That's Spanish.", new Error("500")]);
    const answer = await askKeep("Near an ocean?", FACT_SHEET, client);
    expect(answer).toBe(FALLBACK_LINE);
    expect(client.complete).toHaveBeenCalledTimes(2);
  });

  it("treats empty/garbled model output as a deflection", async () => {
    const client = fakeClient(["   "]);
    const answer = await askKeep("Anything?", FACT_SHEET, client);
    expect(answer).toBe(DEFLECTION_LINE);
    expect(client.complete).toHaveBeenCalledTimes(1);
  });

  it("catches accented/case leaks (Layer 3 folding)", async () => {
    const client = fakeClient([
      "They call it México, obviously.", // accented leak
      "Warm and coastal, that's all you get, whiskers.", // clean
    ]);
    const answer = await askKeep("Where?", FACT_SHEET, client);
    expect(answer).toBe("Warm and coastal, that's all you get, whiskers.");
    expect(client.complete).toHaveBeenCalledTimes(2);
  });
});
