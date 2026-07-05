import { describe, it, expect } from "vitest";
import { normalize, containsForbidden } from "../src/leakFilter.js";

const FORBIDDEN = ["spanish", "mexico", "mexican", "espanol", "peso"];

describe("normalize", () => {
  it("lowercases and strips diacritics via NFKD", () => {
    expect(normalize("MÉXICO")).toBe("mexico");
    expect(normalize("Español")).toBe("espanol");
    expect(normalize("Peso")).toBe("peso");
  });
});

describe("containsForbidden — whole word, case & accent insensitive", () => {
  it("catches an exact forbidden word", () => {
    expect(containsForbidden("Aye, they speak Spanish there.", FORBIDDEN)).toBe(true);
  });

  it("is case-insensitive", () => {
    expect(containsForbidden("MEXICO is warm.", FORBIDDEN)).toBe(true);
  });

  it("is accent-insensitive (accented model output vs plain forbidden term)", () => {
    expect(containsForbidden("They call it México.", FORBIDDEN)).toBe(true);
    expect(containsForbidden("The word is Español.", FORBIDDEN)).toBe(true);
  });

  it("matches a forbidden term written with accents against a plain-list entry", () => {
    // forbidden list is normalized lowercase per the prep pipeline
    expect(containsForbidden("¡Hola! Es Español.", ["espanol"])).toBe(true);
  });

  it("does NOT false-positive on a substring — 'spain' inside 'explain'", () => {
    expect(containsForbidden("Let me explain the coastline.", ["spain"])).toBe(false);
  });

  it("does NOT match 'mexico' inside a longer token", () => {
    expect(containsForbidden("mexiconauts are not a thing", ["mexico"])).toBe(false);
  });

  it("matches a whole word bounded by punctuation", () => {
    expect(containsForbidden("It's the peso, obviously.", FORBIDDEN)).toBe(true);
    expect(containsForbidden("(Mexican) cooking.", FORBIDDEN)).toBe(true);
  });

  it("returns false when nothing forbidden appears", () => {
    expect(
      containsForbidden("Cold up north, and yes they've a coastline.", FORBIDDEN),
    ).toBe(false);
  });

  it("ignores empty forbidden entries", () => {
    expect(containsForbidden("anything at all", ["", "  "])).toBe(false);
  });

  it("handles multi-word forbidden phrases", () => {
    expect(containsForbidden("You'd hear it at the World Cup.", ["world cup"])).toBe(true);
    expect(containsForbidden("A cup of ale.", ["world cup"])).toBe(false);
  });
});
