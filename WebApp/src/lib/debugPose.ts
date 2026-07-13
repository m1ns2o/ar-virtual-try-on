import { buildGarmentFit } from "./fit";
import type { FilteredLandmark, GarmentDefinition, PoseFrame } from "../types/pose";

/** Development-only full-body pose used for browser visual regression checks. */
export function createDebugPoseFrame(definition: GarmentDefinition): PoseFrame {
  const points: Array<FilteredLandmark | null> = Array.from({ length: 33 }, () => null);
  const set = (index: number, x: number, y: number) => {
    points[index] = { x, y, z: 0, confidence: 0.95, insideViewport: true };
  };

  set(0, 0, 0.72);
  set(11, -0.25, 0.43);
  set(12, 0.25, 0.43);
  set(13, -0.42, 0.12);
  set(14, 0.42, 0.12);
  set(15, -0.45, -0.18);
  set(16, 0.45, -0.18);
  set(23, -0.17, -0.02);
  set(24, 0.17, -0.02);
  set(25, -0.15, -0.47);
  set(26, 0.15, -0.47);
  set(27, -0.14, -0.91);
  set(28, 0.14, -0.91);

  return {
    packet: null,
    points,
    fit: buildGarmentFit(points, definition),
    quality: "fitted",
    inferenceFps: 30,
    inferenceSize: "debug",
  };
}
