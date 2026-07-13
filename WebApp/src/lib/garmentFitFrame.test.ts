import { describe, expect, it } from "vitest";

import { buildGarmentModelFitFrame } from "./garmentFitFrame";

describe("garment model fit frame", () => {
  it("fits an upper garment by its torso width instead of sleeve-tip bounds", () => {
    const frame = buildGarmentModelFitFrame({ x: 2, y: 1.794, z: 0.8 }, "upper");
    expect(frame.fitWidth).toBeGreaterThan(1.2);
    expect(frame.fitWidth).toBeLessThan(1.5);
    expect(frame.fitWidth).toBeLessThan(2);
  });

  it("keeps the upper anchor inside the model shoulder region", () => {
    const frame = buildGarmentModelFitFrame({ x: 2, y: 1.13, z: 0.62 }, "upper");
    expect(frame.anchorLocal.y).toBeCloseTo(1.13 * 0.32, 5);
    expect(frame.fitHeight).toBeCloseTo(1.13 * 0.82, 5);
  });

  it("uses the measured polo seam instead of the older low rig point", () => {
    const height = 1.794;
    const frame = buildGarmentModelFitFrame({ x: 2, y: height, z: 0.8 }, "upper");
    expect(frame.anchorLocal.y).toBeCloseTo(height * 0.38, 2);
    expect(frame.fitHeight).toBeCloseTo(height * 0.88, 2);
  });
});
