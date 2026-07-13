import type { MediaPipePosePacket, PoseLandmark, StageLandmark, Vec2 } from "../types/pose";

export interface ViewportSize {
  width: number;
  height: number;
}

export interface CoordinateTransform {
  frameWidth: number;
  frameHeight: number;
  displayRotationDegrees: 0 | 90 | 180 | 270;
  inputMirrored: boolean;
  previewMirrored: boolean;
  viewport: ViewportSize;
}

function rotateNormalized(point: Vec2, degrees: 0 | 90 | 180 | 270): Vec2 {
  switch (degrees) {
    case 90:
      return { x: 1 - point.y, y: point.x };
    case 180:
      return { x: 1 - point.x, y: 1 - point.y };
    case 270:
      return { x: point.y, y: 1 - point.x };
    default:
      return point;
  }
}

export function mapNormalizedToStage(point: Vec2, transform: CoordinateTransform): Vec2 {
  const rotated = rotateNormalized(point, transform.displayRotationDegrees);
  const mirrorForPreview = transform.inputMirrored !== transform.previewMirrored;
  const x = mirrorForPreview ? 1 - rotated.x : rotated.x;
  const y = rotated.y;
  const rotatedQuarterTurn = transform.displayRotationDegrees === 90 || transform.displayRotationDegrees === 270;
  const frameWidth = Math.max(1, rotatedQuarterTurn ? transform.frameHeight : transform.frameWidth);
  const frameHeight = Math.max(1, rotatedQuarterTurn ? transform.frameWidth : transform.frameHeight);
  const viewportWidth = Math.max(1, transform.viewport.width);
  const viewportHeight = Math.max(1, transform.viewport.height);
  const imageAspect = frameWidth / frameHeight;
  const viewportAspect = viewportWidth / viewportHeight;

  let renderedWidth = viewportWidth;
  let renderedHeight = viewportHeight;
  if (imageAspect > viewportAspect) renderedWidth = viewportHeight * imageAspect;
  else renderedHeight = viewportWidth / imageAspect;

  const croppedX = (renderedWidth - viewportWidth) * 0.5;
  const croppedY = (renderedHeight - viewportHeight) * 0.5;
  const screenX = x * renderedWidth - croppedX;
  const screenY = y * renderedHeight - croppedY;

  return {
    x: (screenX / viewportWidth) * 2 - 1,
    y: 1 - (screenY / viewportHeight) * 2,
  };
}

export function mapLandmarkToStage(
  landmark: PoseLandmark,
  transform: CoordinateTransform,
): StageLandmark {
  const point = mapNormalizedToStage(landmark, transform);
  return {
    ...landmark,
    x: point.x,
    y: point.y,
    z: -landmark.z * 0.12,
    insideViewport: point.x >= -1.06 && point.x <= 1.06 && point.y >= -1.06 && point.y <= 1.06,
  };
}

export function mapPacketLandmarks(
  packet: MediaPipePosePacket,
  viewport: ViewportSize,
): StageLandmark[] {
  const transform: CoordinateTransform = {
    frameWidth: packet.frameWidth,
    frameHeight: packet.frameHeight,
    displayRotationDegrees: packet.displayRotationDegrees,
    inputMirrored: packet.inputMirrored,
    previewMirrored: packet.previewMirrored,
    viewport,
  };
  return packet.landmarks.map((landmark) => mapLandmarkToStage(landmark, transform));
}
