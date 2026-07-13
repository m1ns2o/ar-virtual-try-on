import { useRef, type KeyboardEvent, type PointerEvent } from "react";

import { hexToHsv, hsvToHex, type HsvColor } from "../lib/color";

interface Props {
  color: string;
  label: string;
  onChange: (color: string) => void;
  onCommit: (color: string) => void;
}

const clamp01 = (value: number) => Math.min(1, Math.max(0, value));

export function HsvColorPicker({ color, label, onChange, onCommit }: Props) {
  const areaRef = useRef<HTMLDivElement>(null);
  const hsv = hexToHsv(color);
  const hueColor = hsvToHex({ h: hsv.h, s: 1, v: 1 });

  const emit = (next: HsvColor, commit = false) => {
    const hex = hsvToHex(next);
    onChange(hex);
    if (commit) onCommit(hex);
  };

  const updateFromPointer = (event: PointerEvent<HTMLDivElement>, commit = false) => {
    const rect = areaRef.current?.getBoundingClientRect();
    if (!rect) return;
    emit(
      {
        h: hsv.h,
        s: clamp01((event.clientX - rect.left) / rect.width),
        v: clamp01(1 - (event.clientY - rect.top) / rect.height),
      },
      commit,
    );
  };

  const handleKey = (event: KeyboardEvent<HTMLDivElement>) => {
    const step = event.shiftKey ? 0.1 : 0.02;
    const next = { ...hsv };
    if (event.key === "ArrowLeft") next.s -= step;
    else if (event.key === "ArrowRight") next.s += step;
    else if (event.key === "ArrowUp") next.v += step;
    else if (event.key === "ArrowDown") next.v -= step;
    else return;
    event.preventDefault();
    next.s = clamp01(next.s);
    next.v = clamp01(next.v);
    emit(next, true);
  };

  return (
    <div className="hsv-picker">
      <div
        ref={areaRef}
        className="hsv-area"
        style={{ backgroundColor: hueColor }}
        role="slider"
        tabIndex={0}
        aria-label={`${label} 채도와 밝기`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(hsv.v * 100)}
        aria-valuetext={`채도 ${Math.round(hsv.s * 100)}%, 밝기 ${Math.round(hsv.v * 100)}%`}
        onKeyDown={handleKey}
        onPointerDown={(event) => {
          event.currentTarget.setPointerCapture(event.pointerId);
          updateFromPointer(event);
        }}
        onPointerMove={(event) => {
          if (event.currentTarget.hasPointerCapture(event.pointerId)) updateFromPointer(event);
        }}
        onPointerUp={(event) => {
          updateFromPointer(event, true);
          event.currentTarget.releasePointerCapture(event.pointerId);
        }}
      >
        <span
          className="hsv-cursor"
          style={{ left: `${hsv.s * 100}%`, top: `${(1 - hsv.v) * 100}%`, background: color }}
        />
      </div>
      <label className="hue-control">
        <span className="sr-only">{label} 색상</span>
        <input
          type="range"
          min="0"
          max="360"
          value={Math.round(hsv.h * 360)}
          aria-label={`${label} 색상`}
          onChange={(event) => emit({ ...hsv, h: Number(event.target.value) / 360 })}
          onPointerUp={() => onCommit(color)}
          onKeyUp={() => onCommit(color)}
        />
      </label>
    </div>
  );
}
