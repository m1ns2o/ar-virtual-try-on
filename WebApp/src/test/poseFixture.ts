import type { FilteredLandmark, StageLandmark } from "../types/pose";

export function makeStageLandmarks(): StageLandmark[] {
  const landmarks: StageLandmark[] = Array.from({ length: 33 }, () => ({
    x: 0,
    y: 0,
    z: 0,
    visibility: 0.05,
    presence: 0.05,
    insideViewport: true,
  }));
  const set = (index: number, x: number, y: number, z = 0) => {
    landmarks[index] = { x, y, z, visibility: 0.95, presence: 0.95, insideViewport: true };
  };
  set(0, 0, 0.73);
  set(11, -0.25, 0.43);
  set(12, 0.25, 0.43);
  set(13, -0.4, 0.15);
  set(14, 0.4, 0.15);
  set(15, -0.44, -0.12);
  set(16, 0.44, -0.12);
  set(23, -0.17, -0.02);
  set(24, 0.17, -0.02);
  set(25, -0.15, -0.46);
  set(26, 0.15, -0.46);
  set(27, -0.14, -0.91);
  set(28, 0.14, -0.91);
  return landmarks;
}

export function asFiltered(landmarks = makeStageLandmarks()): Array<FilteredLandmark | null> {
  return landmarks.map((landmark) =>
    Math.max(landmark.visibility, landmark.presence) >= 0.24
      ? {
          x: landmark.x,
          y: landmark.y,
          z: landmark.z,
          confidence: Math.max(landmark.visibility, landmark.presence),
          insideViewport: landmark.insideViewport,
        }
      : null,
  );
}
