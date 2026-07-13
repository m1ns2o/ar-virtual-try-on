import type { GarmentSlot, Vec3 } from "../types/pose";

export interface GarmentModelFitFrame {
  anchorLocal: Vec3;
  fitWidth: number;
  fitHeight: number;
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}

function inverseLerp(a: number, b: number, value: number): number {
  return clamp01((value - a) / Math.max(0.0001, b - a));
}

function smoothStep(value: number): number {
  return value * value * (3 - 2 * value);
}

function torsoWidthFraction(aspect: number, fittedFraction: number, wideSleeveFraction: number): number {
  const wideSleeve = smoothStep(inverseLerp(0.82, 1.7, aspect));
  return fittedFraction + (wideSleeveFraction - fittedFraction) * wideSleeve;
}

/**
 * Matches GarmentFittingController.GarmentFitFrame.FromBounds in Unity.
 * The web model is centered before this frame is used, so X/Z anchors are zero.
 */
export function buildGarmentModelFitFrame(size: Vec3, slot: GarmentSlot): GarmentModelFitFrame {
  const width = Math.max(0.001, size.x);
  const height = Math.max(0.001, size.y);
  const aspect = width / height;

  switch (slot) {
    case "lower": {
      const anchorY = height * 0.4;
      return {
        anchorLocal: { x: 0, y: anchorY, z: 0 },
        fitWidth: width * (0.76 + (0.88 - 0.76) * inverseLerp(0.36, 0.82, aspect)),
        fitHeight: Math.max(0.001, height * 0.9),
      };
    }
    case "onePiece": {
      const shoulderInset = 0.08;
      const anchorY = height * (0.5 - shoulderInset);
      return {
        anchorLocal: { x: 0, y: anchorY, z: 0 },
        fitWidth: width * torsoWidthFraction(aspect, 0.7, 0.49),
        fitHeight: Math.max(0.001, height * (1 - shoulderInset)),
      };
    }
    case "outerwear": {
      const shoulderInset = 0.18;
      const anchorY = height * (0.5 - shoulderInset);
      return {
        anchorLocal: { x: 0, y: anchorY, z: 0 },
        fitWidth: width * torsoWidthFraction(aspect, 0.7, 0.48),
        fitHeight: Math.max(0.001, height * (1 - shoulderInset)),
      };
    }
    case "upper":
    default: {
      const shoulderInset = 0.12 + 0.06 * inverseLerp(1.1, 1.75, aspect);
      const anchorY = height * (0.5 - shoulderInset);
      return {
        anchorLocal: { x: 0, y: anchorY, z: 0 },
        fitWidth: width * torsoWidthFraction(aspect, 0.72, 0.5),
        fitHeight: Math.max(0.001, height * (1 - shoulderInset)),
      };
    }
  }
}
