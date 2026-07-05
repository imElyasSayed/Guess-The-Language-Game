import { describe, it, expect } from "vitest";
import { buildSystemPrompt, ANIMAL_INSULTS } from "../src/prompt.js";
import type { FactSheet } from "../src/types.js";

const FACT_SHEET: FactSheet = {
  language: "Spanish",
  country: "Mexico",
  continent: "North America",
  forbidden: ["spanish", "mexico", "mexican", "peso", "espanol"],
};

describe("buildSystemPrompt", () => {
  const prompt = buildSystemPrompt(FACT_SHEET);

  it("embeds the hidden facts for reasoning", () => {
    expect(prompt).toContain("Spanish");
    expect(prompt).toContain("Mexico");
    expect(prompt).toContain("North America");
  });

  it("lists the forbidden words", () => {
    for (const word of FACT_SHEET.forbidden) {
      expect(prompt).toContain(word);
    }
  });

  it("establishes the rude/funny/honest voice and no-lying rule", () => {
    expect(prompt).toMatch(/The Keep/);
    expect(prompt.toLowerCase()).toMatch(/honest/);
    expect(prompt.toLowerCase()).toMatch(/never (lie|invent|output)|must be true/);
  });

  it("instructs broad non-identifying truths", () => {
    expect(prompt.toLowerCase()).toMatch(/hemisphere/);
    expect(prompt.toLowerCase()).toMatch(/coastal|landlocked/);
    expect(prompt.toLowerCase()).toMatch(/script/);
    expect(prompt.toLowerCase()).toMatch(/climate/);
  });

  it("covers point-blank deflection with animal insults", () => {
    for (const insult of ANIMAL_INSULTS) {
      expect(prompt).toContain(insult);
    }
    expect(prompt.toLowerCase()).toMatch(/never answer/);
  });

  it("includes the scope / injection clamp (Layer 4)", () => {
    expect(prompt.toLowerCase()).toMatch(/injection|instructions|ignore/);
    expect(prompt.toLowerCase()).toMatch(/in-game question/);
  });

  it("asks for short 1-2 sentence replies", () => {
    expect(prompt.toLowerCase()).toMatch(/1-2 (short )?sentence/);
  });
});
