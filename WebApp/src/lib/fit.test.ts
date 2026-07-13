import { describe, expect, it } from "vitest";

import { GARMENTS } from "../data/garments";
import { asFiltered } from "../test/poseFixture";
import { POSE_INDEX } from "../types/pose";
import { buildGarmentFit, FitStabilizer, isFullBodyAligned } from "./fit";

describe("garment fitting", () => {
  it("builds a finite fitting target for every catalog slot", () => {
    const points = asFiltered();
    for (const garment of GARMENTS) {
      const fit = buildGarmentFit(points, garment);
      expect(fit, garment.id).not.toBeNull();
      expect(fit!.width).toBeGreaterThan(0.3);
      expect(fit!.height).toBeGreaterThan(0.3);
      expect(Number.isFinite(fit!.center.x)).toBe(true);
      expect(Number.isFinite(fit!.anchor.y)).toBe(true);
      expect(Number.isFinite(fit!.rotation)).toBe(true);
    }
  });

  it("anchors tops to the MediaPipe shoulder line with clothing ease", () => {
    const points = asFiltered();
    const fit = buildGarmentFit(points, GARMENTS[0])!;
    expect(fit.anchor.y).toBeCloseTo(0.43, 5);
    expect(fit.width).toBeGreaterThan(0.5 * 1.3);
  });

  it("places one-piece dress shoulder seams slightly above the MediaPipe shoulder line", () => {
    const points = asFiltered();
    const dress = GARMENTS.find((garment) => garment.id === "mh-flapper-dress")!;
    const fit = buildGarmentFit(points, dress)!;

    expect(dress.fitAnchorOffset.y).toBeCloseTo(0.018, 5);
    expect(fit.anchor.y).toBeCloseTo(0.43 + 0.018, 5);
  });

  it("holds the latest fit briefly when shoulders disappear", () => {
    const points = asFiltered();
    const stabilizer = new FitStabilizer();
    const garment = GARMENTS[0];
    expect(stabilizer.update(points, garment, 1000)).not.toBeNull();
    const missing = [...points];
    missing[POSE_INDEX.leftShoulder] = null;
    missing[POSE_INDEX.rightShoulder] = null;
    expect(stabilizer.update(missing, garment, 1150)?.held).toBe(true);
    expect(stabilizer.update(missing, garment, 1300)).toBeNull();
  });

  it("recognizes a complete in-frame body", () => {
    expect(isFullBodyAligned(asFiltered())).toBe(true);
  });
});
