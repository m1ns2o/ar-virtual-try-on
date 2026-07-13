import { describe, expect, it } from "vitest";

import { deformGarmentPositions } from "./deform";

describe("garment deformation", () => {
  it("moves sleeve and lower-body vertices without exceeding the deformation limit", () => {
    const rest = new Float32Array([
      -1, 1.5, 0,
      1, 1.5, 0,
      -0.5, -1, 0,
      0.5, -1, 0,
      0, 0, 0,
    ]);
    const output = deformGarmentPositions(
      rest,
      { min: [-1, -1, 0], max: [1, 2, 0] },
      { shoulderTilt: 0, leftArmAngle: 0.7, rightArmAngle: -0.7, hipShift: 0.35, kneeSpread: 0.45 },
      "onePiece",
    );
    let moved = false;
    for (let offset = 0; offset < rest.length; offset += 3) {
      const displacement = Math.hypot(output[offset] - rest[offset], output[offset + 1] - rest[offset + 1]);
      if (displacement > 0.001) moved = true;
      expect(displacement).toBeLessThanOrEqual(0.36 + 1e-6);
      expect(output[offset + 2]).toBe(rest[offset + 2]);
    }
    expect(moved).toBe(true);
  });
});
