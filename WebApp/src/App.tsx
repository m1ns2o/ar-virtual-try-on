import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { CameraStage } from "./components/CameraStage";
import { ControlPanel } from "./components/ControlPanel";
import { StatusPill } from "./components/StatusPill";
import { createDefaultAppearance, findGarment, GARMENTS } from "./data/garments";
import { usePoseTracker } from "./hooks/usePoseTracker";
import { ko } from "./i18n/ko";
import { captureTryOn, saveCapture } from "./lib/capture";
import { createDebugPoseFrame } from "./lib/debugPose";
import type { GarmentAppearance, PoseFrame } from "./types/pose";

const APPEARANCE_KEY = "ibobom-appearances-v2";
const RECENT_KEY = "ibobom-recent-colors-v1";

function defaultAppearances(): Record<string, GarmentAppearance> {
  return Object.fromEntries(GARMENTS.map((garment) => [garment.id, createDefaultAppearance(garment)]));
}

function readAppearances(): Record<string, GarmentAppearance> {
  const defaults = defaultAppearances();
  try {
    const stored = JSON.parse(localStorage.getItem(APPEARANCE_KEY) ?? "{}") as Record<string, GarmentAppearance>;
    for (const garment of GARMENTS) defaults[garment.id] = { ...defaults[garment.id], ...stored[garment.id] };
  } catch {
    // Invalid local preferences are safely ignored.
  }
  return defaults;
}

function readRecentColors(): string[] {
  try {
    const stored = JSON.parse(localStorage.getItem(RECENT_KEY) ?? "[]") as unknown;
    if (Array.isArray(stored)) return stored.filter((value): value is string => typeof value === "string").slice(0, 5);
  } catch {
    // Invalid local preferences are safely ignored.
  }
  return ["#D9E6DF", "#7B3032", "#C65F4C", "#4D5150", "#E8C59B"];
}

export function App() {
  const stageRef = useRef<HTMLElement | null>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const webglCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const [selectedId, setSelectedId] = useState(GARMENTS[0].id);
  const [appearances, setAppearances] = useState(readAppearances);
  const [recentColors, setRecentColors] = useState(readRecentColors);
  const [garmentLoading, setGarmentLoading] = useState(false);
  const [toast, setToast] = useState<string | null>(null);
  const definition = useMemo(() => findGarment(selectedId), [selectedId]);
  const appearance = appearances[selectedId] ?? createDefaultAppearance(definition);
  const tracker = usePoseTracker({ videoRef, stageRef, definition });
  const debugPose = import.meta.env.DEV && new URLSearchParams(window.location.search).get("debugPose") === "1";
  const debugFrame = useMemo<PoseFrame | null>(
    () => (debugPose ? createDebugPoseFrame(definition) : null),
    [debugPose, definition],
  );
  const visibleFrame = debugFrame ?? tracker.frame;
  const visiblePhase = debugFrame ? "running" : tracker.phase;

  useEffect(() => {
    localStorage.setItem(APPEARANCE_KEY, JSON.stringify(appearances));
  }, [appearances]);
  useEffect(() => {
    localStorage.setItem(RECENT_KEY, JSON.stringify(recentColors));
  }, [recentColors]);
  useEffect(() => {
    if (!toast) return;
    const timeout = window.setTimeout(() => setToast(null), 2600);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  const updateAppearance = (next: GarmentAppearance) => {
    setAppearances((current) => ({ ...current, [selectedId]: next }));
  };

  const commitColor = (color: string) => {
    setRecentColors((current) => [color, ...current.filter((value) => value !== color)].slice(0, 5));
  };

  const resetAppearance = () => {
    setAppearances((current) => ({ ...current, [selectedId]: createDefaultAppearance(definition) }));
  };

  const handleCanvas = useCallback((canvas: HTMLCanvasElement) => {
    webglCanvasRef.current = canvas;
  }, []);
  const handleGarmentLoading = useCallback((loading: boolean) => setGarmentLoading(loading), []);
  const handleGarmentError = useCallback(() => setToast(ko.errors.garment), []);

  const capture = async () => {
    if (
      tracker.phase !== "running" ||
      !tracker.frame.fit ||
      !videoRef.current ||
      !webglCanvasRef.current ||
      !stageRef.current
    ) {
      setToast(ko.capture.unavailable);
      return;
    }
    try {
      const blob = await captureTryOn(videoRef.current, webglCanvasRef.current, stageRef.current);
      saveCapture(blob);
      setToast(ko.capture.saved);
    } catch {
      setToast(ko.capture.failed);
    }
  };

  return (
    <div className="app-shell">
      <header className="top-bar">
        <a className="brand" href="/" aria-label={`${ko.appName} 홈`}>
          <span className="brand-mark" aria-hidden="true">핏</span>
          <span>
            <strong>{ko.appName}</strong>
            <small>3D VIRTUAL FITTING</small>
          </span>
        </a>
        <StatusPill phase={visiblePhase} quality={visibleFrame.quality} garmentLoading={garmentLoading} />
        <button
          type="button"
          className="capture-button"
          onClick={capture}
          aria-disabled={visiblePhase !== "running" || !visibleFrame.fit || Boolean(debugFrame)}
          title={ko.capture.action}
        >
          <span aria-hidden="true">◎</span>
          {ko.capture.action}
        </button>
      </header>

      <div className="workspace">
        <CameraStage
          stageRef={stageRef}
          videoRef={videoRef}
          phase={visiblePhase}
          errorCode={tracker.errorCode}
          frame={visibleFrame}
          definition={definition}
          appearance={appearance}
          onStart={tracker.start}
          onStop={tracker.stop}
          onCanvas={handleCanvas}
          onGarmentLoading={handleGarmentLoading}
          onGarmentError={handleGarmentError}
        />
        <ControlPanel
          selected={definition}
          appearance={appearance}
          recentColors={recentColors}
          onSelect={(garment) => setSelectedId(garment.id)}
          onAppearanceChange={updateAppearance}
          onColorCommit={commitColor}
          onReset={resetAppearance}
        />
      </div>

      {toast ? <div className="toast" role="status" aria-live="polite">{toast}</div> : null}
    </div>
  );
}
