import { describe, it, expect } from "vitest";
import { processOracleRequest, createServer } from "../src/server.js";
import type { OracleClient } from "../src/types.js";
import type { AddressInfo } from "node:net";

const FACT_SHEET = {
  language: "Spanish",
  country: "Mexico",
  continent: "North America",
  forbidden: ["spanish", "mexico"],
};

const okClient: OracleClient = {
  async complete() {
    return "Cold up north, aye, coastline too. Happy, feathers?";
  },
};

describe("processOracleRequest", () => {
  it("returns 200 { answer } for a well-formed body", async () => {
    const raw = JSON.stringify({ question: "Near an ocean?", factSheet: FACT_SHEET });
    const res = await processOracleRequest(raw, okClient);
    expect(res.status).toBe(200);
    expect(res.body).toEqual({
      answer: "Cold up north, aye, coastline too. Happy, feathers?",
    });
  });

  it("400s on non-JSON body", async () => {
    const res = await processOracleRequest("not json", okClient);
    expect(res.status).toBe(400);
  });

  it("400s on a missing question", async () => {
    const raw = JSON.stringify({ factSheet: FACT_SHEET });
    const res = await processOracleRequest(raw, okClient);
    expect(res.status).toBe(400);
  });

  it("400s on a malformed fact sheet", async () => {
    const raw = JSON.stringify({
      question: "hi",
      factSheet: { language: "Spanish" },
    });
    const res = await processOracleRequest(raw, okClient);
    expect(res.status).toBe(400);
  });

  it("still returns 200 (fallback) when the client errors", async () => {
    const failing: OracleClient = {
      async complete() {
        throw new Error("down");
      },
    };
    const raw = JSON.stringify({ question: "hi", factSheet: FACT_SHEET });
    const res = await processOracleRequest(raw, failing);
    expect(res.status).toBe(200);
    expect(res.body).toMatchObject({ answer: expect.stringContaining("grunts") });
  });
});

describe("http server (in-process)", () => {
  it("serves GET /health and POST /oracle", async () => {
    const server = createServer(okClient);
    await new Promise<void>((r) => server.listen(0, r));
    const { port } = server.address() as AddressInfo;
    try {
      const health = await fetch(`http://127.0.0.1:${port}/health`);
      expect(health.status).toBe(200);
      expect(await health.json()).toEqual({ ok: true });

      const oracle = await fetch(`http://127.0.0.1:${port}/oracle`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ question: "Near an ocean?", factSheet: FACT_SHEET }),
      });
      expect(oracle.status).toBe(200);
      expect(await oracle.json()).toHaveProperty("answer");
    } finally {
      await new Promise<void>((r) => server.close(() => r()));
    }
  });
});
