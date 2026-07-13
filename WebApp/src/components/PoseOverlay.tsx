import { useEffect, useRef, useState } from "react";

import { POSE_INDEX, type FilteredLandmark } from "../types/pose";

interface Props {
  points: Array<FilteredLandmark | null>;
  visible: boolean;
}

const CONNECTIONS: Array<[number, number]> = [
  [POSE_INDEX.leftShoulder, POSE_INDEX.rightShoulder],
  [POSE_INDEX.leftShoulder, POSE_INDEX.leftElbow],
  [POSE_INDEX.leftElbow, POSE_INDEX.leftWrist],
  [POSE_INDEX.rightShoulder, POSE_INDEX.rightElbow],
  [POSE_INDEX.rightElbow, POSE_INDEX.rightWrist],
  [POSE_INDEX.leftShoulder, POSE_INDEX.leftHip],
  [POSE_INDEX.rightShoulder, POSE_INDEX.rightHip],
  [POSE_INDEX.leftHip, POSE_INDEX.rightHip],
  [POSE_INDEX.leftHip, POSE_INDEX.leftKnee],
  [POSE_INDEX.leftKnee, POSE_INDEX.leftAnkle],
  [POSE_INDEX.rightHip, POSE_INDEX.rightKnee],
  [POSE_INDEX.rightKnee, POSE_INDEX.rightAnkle],
];

export function PoseOverlay({ points, visible }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [size, setSize] = useState({ width: 0, height: 0, ratio: 1 });

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const updateSize = () => {
      const rect = canvas.getBoundingClientRect();
      const ratio = Math.min(window.devicePixelRatio || 1, 2);
      const width = Math.max(1, Math.round(rect.width));
      const height = Math.max(1, Math.round(rect.height));
      const backingWidth = Math.max(1, Math.round(width * ratio));
      const backingHeight = Math.max(1, Math.round(height * ratio));
      if (canvas.width !== backingWidth) canvas.width = backingWidth;
      if (canvas.height !== backingHeight) canvas.height = backingHeight;
      setSize((current) =>
        current.width === width && current.height === height && current.ratio === ratio
          ? current
          : { width, height, ratio },
      );
    };
    const observer = new ResizeObserver(updateSize);
    observer.observe(canvas);
    window.addEventListener("resize", updateSize);
    updateSize();
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", updateSize);
    };
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || size.width === 0 || size.height === 0) return;
    const context = canvas.getContext("2d");
    if (!context) return;
    context.setTransform(size.ratio, 0, 0, size.ratio, 0, 0);
    context.clearRect(0, 0, size.width, size.height);
    if (!visible) return;
    const toCanvas = (point: FilteredLandmark) => ({
      x: ((point.x + 1) * 0.5) * size.width,
      y: ((1 - point.y) * 0.5) * size.height,
    });

    context.lineCap = "round";
    context.lineJoin = "round";
    context.lineWidth = 2;
    context.strokeStyle = "rgba(213, 255, 178, 0.38)";
    for (const [startIndex, endIndex] of CONNECTIONS) {
      const start = points[startIndex];
      const end = points[endIndex];
      if (!start || !end) continue;
      const a = toCanvas(start);
      const b = toCanvas(end);
      context.beginPath();
      context.moveTo(a.x, a.y);
      context.lineTo(b.x, b.y);
      context.stroke();
    }
    context.fillStyle = "rgba(224, 255, 197, 0.72)";
    for (const index of Object.values(POSE_INDEX)) {
      const point = points[index];
      if (!point) continue;
      const position = toCanvas(point);
      context.beginPath();
      context.arc(position.x, position.y, 2.8, 0, Math.PI * 2);
      context.fill();
    }
  }, [points, size, visible]);

  return <canvas ref={canvasRef} className="pose-overlay" aria-hidden="true" />;
}
