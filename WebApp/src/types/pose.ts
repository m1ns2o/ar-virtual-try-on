export interface Vec2 {
  x: number;
  y: number;
}

export interface Vec3 extends Vec2 {
  z: number;
}

export interface PoseLandmark extends Vec3 {
  visibility: number;
  presence: number;
}

export interface MediaPipePosePacket {
  version: 1;
  timestamp: number;
  frameWidth: number;
  frameHeight: number;
  displayRotationDegrees: 0 | 90 | 180 | 270;
  inputMirrored: boolean;
  previewMirrored: boolean;
  landmarks: PoseLandmark[];
  worldLandmarks?: PoseLandmark[];
}

export interface StageLandmark extends PoseLandmark {
  insideViewport: boolean;
}

export interface FilteredLandmark extends Vec3 {
  confidence: number;
  insideViewport: boolean;
}

export type GarmentSlot = "upper" | "lower" | "onePiece" | "outerwear";

export interface GarmentDefinition {
  id: string;
  displayName: string;
  shortName: string;
  slot: GarmentSlot;
  author: string;
  license: string;
  sourceUrl: string;
  modelPath: string;
  texturePath?: string;
  normalTexturePath?: string;
  detailTexturePath?: string;
  positionOffset: readonly [number, number, number];
  rotationOffset: readonly [number, number, number];
  scaleOffset: readonly [number, number, number];
  fitAnchorOffset: Vec2;
  fitWidthMultiplier: number;
  fitHeightMultiplier: number;
  fitVerticalBias: number;
  heightBlend: number;
  silhouette: "polo" | "sweater" | "pants" | "dress" | "shortSleeveDress" | "robe";
  defaultBaseColor: string;
  defaultStripeColor: string;
}

export interface DeformationMetrics {
  shoulderTilt: number;
  leftArmAngle: number;
  rightArmAngle: number;
  hipShift: number;
  kneeSpread: number;
}

export interface GarmentFit {
  center: Vec2;
  anchor: Vec2;
  width: number;
  height: number;
  rotation: number;
  heightBlend: number;
  held: boolean;
  deformation: DeformationMetrics;
}

export type PoseQuality = "ready" | "searching" | "fitted" | "outOfFrame";

export interface PoseFrame {
  packet: MediaPipePosePacket | null;
  points: Array<FilteredLandmark | null>;
  fit: GarmentFit | null;
  quality: PoseQuality;
  inferenceFps: number;
  inferenceSize: string;
}

export interface GarmentAppearance {
  baseColor: string;
  stripeColors: [string, string, string];
  stripeColorCount: 1 | 2 | 3;
  pattern: "none" | "stripes" | "dots";
  stripeWidth: number;
  stripeDirection: "horizontal" | "vertical" | "diagonal";
  dotSize: number;
}

export const POSE_INDEX = {
  nose: 0,
  leftShoulder: 11,
  rightShoulder: 12,
  leftElbow: 13,
  rightElbow: 14,
  leftWrist: 15,
  rightWrist: 16,
  leftHip: 23,
  rightHip: 24,
  leftKnee: 25,
  rightKnee: 26,
  leftAnkle: 27,
  rightAnkle: 28,
} as const;
