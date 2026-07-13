import { describe, expect, it } from "vitest";

import { hexToHsv, hsvToHex, normalizeHex } from "./color";

describe("color utilities", () => {
  it("normalizes shorthand and full HEX values", () => {
    expect(normalizeHex("abc")).toBe("#AABBCC");
    expect(normalizeHex("#12ef90")).toBe("#12EF90");
    expect(normalizeHex("not-a-color")).toBeNull();
  });

  it("round-trips RGB colors through HSV", () => {
    for (const color of ["#FF0000", "#32C97B", "#7B3032", "#FFFFFF", "#000000"]) {
      expect(hsvToHex(hexToHsv(color))).toBe(color);
    }
  });
});
