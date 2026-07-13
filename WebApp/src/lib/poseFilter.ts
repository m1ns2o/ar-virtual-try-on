import type { FilteredLandmark, StageLandmark } from "../types/pose";

export interface PoseFilterOptions {
  minVisibility: number;
  smoothing: number;
  smoothTime: number;
  lowConfidenceSmoothTime: number;
  maxSpeed: number;
  outlierDistance: number;
  maxMissingFrames: number;
}

const DEFAULT_OPTIONS: PoseFilterOptions = {
  minVisibility: 0.24,
  smoothing: 0.75,
  smoothTime: 0.045,
  lowConfidenceSmoothTime: 0.14,
  maxSpeed: 14,
  outlierDistance: 1.2,
  maxMissingFrames: 4,
};

const clamp = (value: number, minimum: number, maximum: number) =>
  Math.min(maximum, Math.max(minimum, value));
const lerp = (a: number, b: number, t: number) => a + (b - a) * t;

export class PoseFilter {
  private readonly options: PoseFilterOptions;
  private readonly points: Array<FilteredLandmark | null> = Array.from({ length: 33 }, () => null);
  private readonly missingFrames = new Uint8Array(33);
  private lastTimestampMs = -1;

  constructor(options: Partial<PoseFilterOptions> = {}) {
    this.options = { ...DEFAULT_OPTIONS, ...options };
  }

  reset(): void {
    this.points.fill(null);
    this.missingFrames.fill(0);
    this.lastTimestampMs = -1;
  }

  update(input: Array<StageLandmark | null>, timestampMs: number): Array<FilteredLandmark | null> {
    const rawDelta = this.lastTimestampMs > 0 ? (timestampMs - this.lastTimestampMs) / 1000 : 1 / 30;
    const deltaTime = clamp(rawDelta, 1 / 120, 0.12);
    this.lastTimestampMs = timestampMs;

    for (let index = 0; index < 33; index += 1) {
      const landmark = input[index] ?? null;
      const confidence = landmark ? Math.max(landmark.visibility, landmark.presence) : 0;
      if (!landmark || confidence < this.options.minVisibility) {
        this.markMissing(index);
        continue;
      }
      this.updatePoint(index, landmark, confidence, deltaTime);
    }

    return this.snapshot();
  }

  snapshot(): Array<FilteredLandmark | null> {
    return this.points.map((point) => (point ? { ...point } : null));
  }

  private markMissing(index: number): void {
    if (!this.points[index]) return;
    this.missingFrames[index] += 1;
    if (this.missingFrames[index] > this.options.maxMissingFrames) {
      this.points[index] = null;
      this.missingFrames[index] = 0;
    }
  }

  private updatePoint(index: number, target: StageLandmark, confidence: number, deltaTime: number): void {
    this.missingFrames[index] = 0;
    const current = this.points[index];
    if (!current) {
      this.points[index] = {
        x: target.x,
        y: target.y,
        z: target.z,
        confidence,
        insideViewport: target.insideViewport,
      };
      return;
    }

    let dx = target.x - current.x;
    let dy = target.y - current.y;
    let dz = target.z - current.z;
    let distance = Math.hypot(dx, dy, dz);
    const limitDelta = (limit: number) => {
      if (distance <= limit || distance <= 1e-8) return;
      const ratio = limit / distance;
      dx *= ratio;
      dy *= ratio;
      dz *= ratio;
      distance = limit;
    };

    limitDelta(Math.max(0.001, this.options.outlierDistance));
    limitDelta(Math.max(0.001, this.options.maxSpeed) * deltaTime);

    const confidence01 = clamp(
      (confidence - this.options.minVisibility) / (1 - this.options.minVisibility),
      0,
      1,
    );
    const confidenceAlpha = lerp(this.options.smoothing * 0.45, this.options.smoothing, confidence01);
    const smoothTime = lerp(
      this.options.lowConfidenceSmoothTime,
      this.options.smoothTime,
      confidence01,
    );
    const timeAlpha = 1 - Math.exp(-deltaTime / Math.max(0.001, smoothTime));
    const alpha = clamp(Math.max(confidenceAlpha, timeAlpha), 0, 1);

    this.points[index] = {
      x: current.x + dx * alpha,
      y: current.y + dy * alpha,
      z: current.z + dz * alpha,
      confidence,
      insideViewport: target.insideViewport,
    };
  }
}
