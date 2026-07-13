import { describe, expect, it } from "vitest";

import { makeStageLandmarks } from "../test/poseFixture";
import { PoseFilter } from "./poseFilter";

describe("PoseFilter", () => {
  it("limits single-frame landmark outliers by speed", () => {
    const filter = new PoseFilter();
    const initial = makeStageLandmarks();
    filter.update(initial, 1000);
    const jumped = makeStageLandmarks();
    jumped[11].x = 10;
    const result = filter.update(jumped, 1010);
    expect(result[11]!.x).toBeGreaterThan(initial[11].x);
    expect(result[11]!.x - initial[11].x).toBeLessThanOrEqual(0.14);
  });

  it("holds a missing landmark for four frames", () => {
    const filter = new PoseFilter();
    filter.update(makeStageLandmarks(), 1000);
    const missing = makeStageLandmarks();
    missing[11].visibility = 0;
    missing[11].presence = 0;
    for (let frame = 1; frame <= 4; frame += 1) {
      expect(filter.update(missing, 1000 + frame * 33)[11]).not.toBeNull();
    }
    expect(filter.update(missing, 1165)[11]).toBeNull();
  });
});
