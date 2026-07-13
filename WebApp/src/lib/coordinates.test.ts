import { describe, expect, it } from "vitest";

import { mapNormalizedToStage } from "./coordinates";

const base = {
  frameWidth: 1000,
  frameHeight: 1000,
  displayRotationDegrees: 0 as const,
  inputMirrored: false,
  previewMirrored: false,
  viewport: { width: 500, height: 500 },
};

describe("mapNormalizedToStage", () => {
  it("maps the center to the stage origin", () => {
    expect(mapNormalizedToStage({ x: 0.5, y: 0.5 }, base)).toEqual({ x: 0, y: 0 });
  });

  it("mirrors only when input and preview states differ", () => {
    const mapped = mapNormalizedToStage(
      { x: 0.25, y: 0.5 },
      { ...base, previewMirrored: true },
    );
    expect(mapped.x).toBeCloseTo(0.5);
  });

  it("accounts for object-cover cropping", () => {
    const mapped = mapNormalizedToStage(
      { x: 0, y: 0.5 },
      { ...base, frameWidth: 400, frameHeight: 300 },
    );
    expect(mapped.x).toBeCloseTo(-4 / 3);
  });

  it("supports right-angle display rotation", () => {
    const mapped = mapNormalizedToStage(
      { x: 0, y: 0 },
      { ...base, displayRotationDegrees: 90 },
    );
    expect(mapped.x).toBeCloseTo(1);
    expect(mapped.y).toBeCloseTo(1);
  });
});
