import type { DeformationMetrics, GarmentSlot } from "../types/pose";

export interface GeometryBounds {
  min: readonly [number, number, number];
  max: readonly [number, number, number];
}

const clamp = (value: number, minimum: number, maximum: number) =>
  Math.min(maximum, Math.max(minimum, value));
const smoothstep = (edge0: number, edge1: number, value: number) => {
  const t = clamp((value - edge0) / Math.max(1e-6, edge1 - edge0), 0, 1);
  return t * t * (3 - 2 * t);
};

export function deformGarmentPositions(
  rest: Float32Array,
  bounds: GeometryBounds,
  metrics: DeformationMetrics,
  slot: GarmentSlot,
  target?: Float32Array,
): Float32Array {
  const result = target ?? new Float32Array(rest.length);
  const width = Math.max(1e-5, bounds.max[0] - bounds.min[0]);
  const height = Math.max(1e-5, bounds.max[1] - bounds.min[1]);
  const centerX = (bounds.min[0] + bounds.max[0]) * 0.5;
  const shoulderY = bounds.min[1] + height * 0.72;
  const shoulderHalf = width * 0.24;
  const canBendSleeves = slot !== "lower";

  for (let offset = 0; offset < rest.length; offset += 3) {
    const originalX = rest[offset];
    const originalY = rest[offset + 1];
    const originalZ = rest[offset + 2];
    const xNormalized = (originalX - centerX) / width;
    const y01 = clamp((originalY - bounds.min[1]) / height, 0, 1);
    let x = originalX;
    let y = originalY;

    const torsoShear = metrics.hipShift * width * 0.12 * (1 - y01);
    x += torsoShear;

    if (canBendSleeves) {
      const side = xNormalized < 0 ? -1 : 1;
      const sleeveWeight =
        smoothstep(0.24, 0.48, Math.abs(xNormalized)) *
        smoothstep(0.42, 0.64, y01) *
        (1 - smoothstep(0.94, 1, y01));
      const angle = (side < 0 ? metrics.leftArmAngle : metrics.rightArmAngle) * sleeveWeight;
      const pivotX = centerX + shoulderHalf * side;
      const dx = x - pivotX;
      const dy = y - shoulderY;
      const cosine = Math.cos(angle);
      const sine = Math.sin(angle);
      x = pivotX + dx * cosine - dy * sine;
      y = shoulderY + dx * sine + dy * cosine;
    }

    if (slot === "lower" || slot === "onePiece") {
      const lowerWeight = 1 - smoothstep(0.3, 0.72, y01);
      x += Math.sign(xNormalized || 1) * metrics.kneeSpread * width * 0.08 * lowerWeight;
    }

    const deltaX = x - originalX;
    const deltaY = y - originalY;
    const deltaLength = Math.hypot(deltaX, deltaY);
    const maxDisplacement = width * 0.18;
    const limiter = deltaLength > maxDisplacement ? maxDisplacement / deltaLength : 1;
    result[offset] = originalX + deltaX * limiter;
    result[offset + 1] = originalY + deltaY * limiter;
    result[offset + 2] = originalZ;
  }

  return result;
}
