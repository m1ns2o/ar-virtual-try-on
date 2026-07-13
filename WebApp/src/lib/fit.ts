import {
  POSE_INDEX,
  type DeformationMetrics,
  type FilteredLandmark,
  type GarmentDefinition,
  type GarmentFit,
  type GarmentSlot,
  type PoseQuality,
  type Vec2,
} from "../types/pose";

const MIN_CONFIDENCE = 0.24;
const FIT_HOLD_MS = 220;
const clamp = (value: number, minimum: number, maximum: number) =>
  Math.min(maximum, Math.max(minimum, value));
const lerp = (a: number, b: number, t: number) => a + (b - a) * t;
const mixPoint = (a: Vec2, b: Vec2, t: number): Vec2 => ({ x: lerp(a.x, b.x, t), y: lerp(a.y, b.y, t) });
const distance = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);

function point(points: Array<FilteredLandmark | null>, index: number): FilteredLandmark | null {
  const value = points[index] ?? null;
  return value && value.confidence >= MIN_CONFIDENCE ? value : null;
}

function pairCenter(
  points: Array<FilteredLandmark | null>,
  leftIndex: number,
  rightIndex: number,
): Vec2 | null {
  const left = point(points, leftIndex);
  const right = point(points, rightIndex);
  return left && right ? { x: (left.x + right.x) * 0.5, y: (left.y + right.y) * 0.5 } : null;
}

function visibleBoundsWidth(points: Array<FilteredLandmark | null>, slot: GarmentSlot): number {
  const indices: number[] = [
    POSE_INDEX.leftShoulder,
    POSE_INDEX.rightShoulder,
    POSE_INDEX.leftHip,
    POSE_INDEX.rightHip,
  ];
  if (slot === "lower" || slot === "onePiece") indices.push(POSE_INDEX.leftKnee, POSE_INDEX.rightKnee);
  if (slot === "lower") indices.push(POSE_INDEX.leftAnkle, POSE_INDEX.rightAnkle);
  const values = indices.map((index) => point(points, index)?.x).filter((value): value is number => value !== undefined);
  if (values.length < 2) return 0;
  return Math.max(...values) - Math.min(...values);
}

function reliableForSlot(points: Array<FilteredLandmark | null>, slot: GarmentSlot): boolean {
  const leftShoulder = point(points, POSE_INDEX.leftShoulder);
  const rightShoulder = point(points, POSE_INDEX.rightShoulder);
  if (!leftShoulder || !rightShoulder || Math.abs(rightShoulder.x - leftShoulder.x) < 0.09) return false;

  const leftHip = point(points, POSE_INDEX.leftHip);
  const rightHip = point(points, POSE_INDEX.rightHip);
  if (!leftHip || !rightHip) return slot === "upper" || slot === "outerwear";
  const shoulderCenter = pairCenter(points, POSE_INDEX.leftShoulder, POSE_INDEX.rightShoulder)!;
  const hipCenter = pairCenter(points, POSE_INDEX.leftHip, POSE_INDEX.rightHip)!;
  const geometryReliable =
    Math.abs(rightHip.x - leftHip.x) >= 0.05 &&
    Math.abs(hipCenter.y - shoulderCenter.y) >= 0.16 &&
    Math.abs(hipCenter.x - shoulderCenter.x) <= 0.64;
  if (!geometryReliable) return false;
  if (slot === "lower") return Boolean(point(points, POSE_INDEX.leftKnee) || point(points, POSE_INDEX.rightKnee));
  return true;
}

function deformationMetrics(points: Array<FilteredLandmark | null>, shoulderCenter: Vec2, hipCenter: Vec2): DeformationMetrics {
  const shoulderPairs = [
    [point(points, POSE_INDEX.leftShoulder), point(points, POSE_INDEX.leftElbow)],
    [point(points, POSE_INDEX.rightShoulder), point(points, POSE_INDEX.rightElbow)],
  ] as const;
  const screenPairs = [...shoulderPairs].sort((a, b) => (a[0]?.x ?? 0) - (b[0]?.x ?? 0));
  const armDelta = (pair: (typeof screenPairs)[number], restAngle: number) => {
    if (!pair[0] || !pair[1]) return 0;
    return clamp(Math.atan2(pair[1].y - pair[0].y, pair[1].x - pair[0].x) - restAngle, -0.72, 0.72);
  };
  const leftShoulder = point(points, POSE_INDEX.leftShoulder)!;
  const rightShoulder = point(points, POSE_INDEX.rightShoulder)!;
  const shoulderWidth = Math.max(0.001, distance(leftShoulder, rightShoulder));
  const leftKnee = point(points, POSE_INDEX.leftKnee);
  const rightKnee = point(points, POSE_INDEX.rightKnee);
  const leftHip = point(points, POSE_INDEX.leftHip);
  const rightHip = point(points, POSE_INDEX.rightHip);
  const hipWidth = leftHip && rightHip ? Math.max(0.001, distance(leftHip, rightHip)) : shoulderWidth * 0.82;
  const kneeWidth = leftKnee && rightKnee ? distance(leftKnee, rightKnee) : hipWidth * 0.8;

  return {
    shoulderTilt: clamp(Math.atan2(rightShoulder.y - leftShoulder.y, rightShoulder.x - leftShoulder.x), -0.38, 0.38),
    leftArmAngle: armDelta(screenPairs[0], -2.36),
    rightArmAngle: armDelta(screenPairs[1], -0.78),
    hipShift: clamp((hipCenter.x - shoulderCenter.x) / shoulderWidth, -0.35, 0.35),
    kneeSpread: clamp(kneeWidth / hipWidth - 0.8, -0.45, 0.45),
  };
}

export function buildGarmentFit(
  points: Array<FilteredLandmark | null>,
  definition: GarmentDefinition,
): GarmentFit | null {
  if (!reliableForSlot(points, definition.slot)) return null;
  const leftShoulder = point(points, POSE_INDEX.leftShoulder)!;
  const rightShoulder = point(points, POSE_INDEX.rightShoulder)!;
  const shoulderCenter = pairCenter(points, POSE_INDEX.leftShoulder, POSE_INDEX.rightShoulder)!;
  const shoulderWidth = Math.max(0.001, distance(leftShoulder, rightShoulder));
  const hipCenter = pairCenter(points, POSE_INDEX.leftHip, POSE_INDEX.rightHip) ?? {
    x: shoulderCenter.x,
    y: shoulderCenter.y - Math.max(0.32, shoulderWidth * 1.25),
  };
  const leftHip = point(points, POSE_INDEX.leftHip);
  const rightHip = point(points, POSE_INDEX.rightHip);
  const hipWidth = leftHip && rightHip ? Math.max(0.001, distance(leftHip, rightHip)) : shoulderWidth * 0.82;
  const torsoHeight = Math.max(0.001, distance(shoulderCenter, hipCenter));
  const torsoAxis = { x: hipCenter.x - shoulderCenter.x, y: hipCenter.y - shoulderCenter.y };
  const kneeCenter = pairCenter(points, POSE_INDEX.leftKnee, POSE_INDEX.rightKnee) ?? {
    x: hipCenter.x + torsoAxis.x,
    y: hipCenter.y + torsoAxis.y,
  };
  const ankleCenter = pairCenter(points, POSE_INDEX.leftAnkle, POSE_INDEX.rightAnkle) ?? {
    x: hipCenter.x + torsoAxis.x * 1.85,
    y: hipCenter.y + torsoAxis.y * 1.85,
  };
  const legHeight = Math.max(0.001, distance(hipCenter, ankleCenter));
  const dressHeight = Math.max(torsoHeight * 1.55, distance(shoulderCenter, kneeCenter));
  const boundsWidth = visibleBoundsWidth(points, definition.slot);
  const torsoBoundsWidth = boundsWidth > 0.001 ? clamp(boundsWidth, shoulderWidth * 0.78, shoulderWidth * 1.28) : shoulderWidth;
  const structuralWidth = Math.max(shoulderWidth, hipWidth * 0.9);
  const torsoWidth = lerp(structuralWidth, torsoBoundsWidth, 0.35);
  const lowerWidth = (allowance: number, floor: number) =>
    Math.max(hipWidth * allowance, torsoWidth * floor, shoulderWidth * floor * 0.94);
  const verticalBias = definition.fitVerticalBias;
  let center: Vec2;
  let anchor: Vec2;
  let width: number;
  let height: number;

  switch (definition.slot) {
    case "lower":
      center = mixPoint(hipCenter, ankleCenter, clamp(0.48 + verticalBias, 0, 1));
      anchor = mixPoint(hipCenter, kneeCenter, clamp(0.04 + verticalBias * 0.35, 0, 1));
      width = lowerWidth(1.3, 0.68) * 1.22;
      height = legHeight * 1.04;
      break;
    case "onePiece":
      center = mixPoint(shoulderCenter, kneeCenter, clamp(0.52 + verticalBias, 0, 1));
      anchor = shoulderCenter;
      width = Math.max(torsoWidth, lowerWidth(1.26, 1.04)) * 1.3;
      height = dressHeight * 1.05;
      break;
    case "outerwear":
      center = mixPoint(shoulderCenter, hipCenter, clamp(0.54 + verticalBias, 0, 1));
      anchor = shoulderCenter;
      width = Math.max(torsoWidth, hipWidth * 0.92) * 1.42;
      height = torsoHeight * 1.22;
      break;
    default:
      center = mixPoint(shoulderCenter, hipCenter, clamp(0.5 + verticalBias, 0, 1));
      anchor = shoulderCenter;
      width = Math.max(torsoWidth, hipWidth * 0.86) * 1.36;
      height = torsoHeight * 1.16;
      break;
  }

  center = {
    x: center.x + definition.fitAnchorOffset.x,
    y: center.y + definition.fitAnchorOffset.y,
  };
  anchor = {
    x: anchor.x + definition.fitAnchorOffset.x,
    y: anchor.y + definition.fitAnchorOffset.y,
  };

  const deformation = deformationMetrics(points, shoulderCenter, hipCenter);
  return {
    center,
    anchor,
    width: width * definition.fitWidthMultiplier,
    height: height * definition.fitHeightMultiplier,
    rotation: deformation.shoulderTilt,
    heightBlend: definition.heightBlend,
    held: false,
    deformation,
  };
}

export class FitStabilizer {
  private last: { definitionId: string; timestampMs: number; fit: GarmentFit } | null = null;

  reset(): void {
    this.last = null;
  }

  update(
    points: Array<FilteredLandmark | null>,
    definition: GarmentDefinition,
    timestampMs: number,
  ): GarmentFit | null {
    const fit = buildGarmentFit(points, definition);
    if (fit) {
      this.last = { definitionId: definition.id, timestampMs, fit };
      return fit;
    }
    if (
      this.last &&
      this.last.definitionId === definition.id &&
      timestampMs - this.last.timestampMs <= FIT_HOLD_MS
    ) {
      return { ...this.last.fit, held: true };
    }
    return null;
  }
}

export function isFullBodyAligned(points: Array<FilteredLandmark | null>): boolean {
  return [
    POSE_INDEX.leftShoulder,
    POSE_INDEX.rightShoulder,
    POSE_INDEX.leftHip,
    POSE_INDEX.rightHip,
    POSE_INDEX.leftKnee,
    POSE_INDEX.rightKnee,
    POSE_INDEX.leftAnkle,
    POSE_INDEX.rightAnkle,
  ].every((index) => {
    const value = point(points, index);
    return Boolean(value?.insideViewport && Math.abs(value.x) <= 0.98 && Math.abs(value.y) <= 0.98);
  });
}

export function evaluatePoseQuality(
  points: Array<FilteredLandmark | null>,
  fit: GarmentFit | null,
  wasFitted: boolean,
): PoseQuality {
  if (fit && isFullBodyAligned(points)) return "fitted";
  const shouldersVisible = Boolean(
    point(points, POSE_INDEX.leftShoulder) && point(points, POSE_INDEX.rightShoulder),
  );
  if (shouldersVisible || fit) return "searching";
  return wasFitted ? "outOfFrame" : "searching";
}
