import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { GARMENTS } from "../data/garments";
import type { GarmentSlot } from "../types/pose";

const repositoryRoot = fileURLToPath(new URL("../../../", import.meta.url));
const unitySlots: Record<GarmentSlot, number> = {
  upper: 0,
  lower: 1,
  onePiece: 2,
  outerwear: 3,
};

function readValue(yaml: string, key: string): string {
  const match = yaml.match(new RegExp("^  " + key + ":\\s*(.+)$", "m"));
  if (!match) throw new Error("Unity catalog field is missing: " + key);
  return match[1].trim();
}

function readNumber(yaml: string, key: string): number {
  const value = Number(readValue(yaml, key));
  if (!Number.isFinite(value)) throw new Error("Unity catalog field is not numeric: " + key);
  return value;
}

function readVector(yaml: string, key: string): Record<"x" | "y" | "z", number> {
  const values = { x: 0, y: 0, z: 0 };
  for (const match of readValue(yaml, key).matchAll(/([xyz]):\s*([^,}]+)/g)) {
    values[match[1] as "x" | "y" | "z"] = Number(match[2]);
  }
  return values;
}

describe("Unity and web garment catalog parity", () => {
  it("keeps shared fitting controls synchronized", () => {
    for (const garment of GARMENTS) {
      const assetPath = resolve(
        repositoryRoot,
        "Assets",
        "ARCloset",
        "Catalog",
        garment.id + ".asset",
      );
      const yaml = readFileSync(assetPath, "utf8");
      const anchor = readVector(yaml, "fitAnchorOffset");
      const anchorUnitScale = garment.slot === "onePiece" ? 0.3 : 1;

      expect(readValue(yaml, "garmentId"), garment.id).toBe(garment.id);
      expect(readNumber(yaml, "slot"), garment.id).toBe(unitySlots[garment.slot]);
      expect(readValue(yaml, "author"), garment.id).toBe(garment.author);
      expect(readValue(yaml, "license"), garment.id).toBe(garment.license);
      expect(readValue(yaml, "sourceUrl"), garment.id).toBe(garment.sourceUrl);
      expect(anchor.x, garment.id).toBeCloseTo(garment.fitAnchorOffset.x, 6);
      expect(anchor.y * anchorUnitScale, garment.id).toBeCloseTo(garment.fitAnchorOffset.y, 6);
      expect(readNumber(yaml, "fitWidthMultiplier"), garment.id).toBeCloseTo(
        garment.fitWidthMultiplier,
        6,
      );
      expect(readNumber(yaml, "fitHeightMultiplier"), garment.id).toBeCloseTo(
        garment.fitHeightMultiplier,
        6,
      );
      expect(readNumber(yaml, "fitVerticalBias"), garment.id).toBeCloseTo(
        garment.fitVerticalBias,
        6,
      );
    }
  });
});
