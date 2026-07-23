import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { CameraStage } from "./components/CameraStage";
import { ControlPanel } from "./components/ControlPanel";
import { StatusPill } from "./components/StatusPill";
import { createDefaultAppearance, findGarment, GARMENTS } from "./data/garments";
import { usePoseTracker } from "./hooks/usePoseTracker";
import { ko } from "./i18n/ko";
import { captureTryOn, saveCapture } from "./lib/capture";
import { createDebugPoseFrame } from "./lib/debugPose";
import { buildGarmentFit } from "./lib/fit";
import type { GarmentAppearance, GarmentDefinition, PoseFrame } from "./types/pose";

const APPEARANCE_KEY = "ibobom-appearances-v2";
const RECENT_KEY = "ibobom-recent-colors-v1";
type EditorSlot = "upper" | "lower";
interface OutfitSelection { upperId: string; lowerId: string | null; }

function firstGarment(...slots: GarmentDefinition["slot"][]) {
  const garment = GARMENTS.find((item) => slots.includes(item.slot));
  if (!garment) throw new Error("Required garment slot is missing.");
  return garment.id;
}

function defaultAppearances(): Record<string, GarmentAppearance> {
  return Object.fromEntries(GARMENTS.map((garment) => [garment.id, createDefaultAppearance(garment)]));
}

function readAppearances(): Record<string, GarmentAppearance> {
  const defaults = defaultAppearances();
  try {
    const stored = JSON.parse(localStorage.getItem(APPEARANCE_KEY) ?? "{}") as Record<string, GarmentAppearance>;
    for (const garment of GARMENTS) {
      const saved = stored[garment.id] as (Partial<GarmentAppearance> & {
        stripeEnabled?: boolean;
        stripeColor?: string;
      }) | undefined;
      if (!saved) continue;
      const fallback = defaults[garment.id].stripeColors;
      const stripeColors: GarmentAppearance["stripeColors"] = [
        saved.stripeColors?.[0] ?? saved.stripeColor ?? fallback[0],
        saved.stripeColors?.[1] ?? fallback[1],
        saved.stripeColors?.[2] ?? fallback[2],
      ];
      defaults[garment.id] = {
        ...defaults[garment.id],
        ...saved,
        stripeColors,
        stripeColorCount: saved.stripeColorCount === 3 ? 3 : saved.stripeColorCount === 2 ? 2 : 1,
        pattern: saved.pattern ?? (saved.stripeEnabled ? "stripes" : "none"),
      };
    }
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

function runningFit(frame: PoseFrame, definition: GarmentDefinition, phase: string) {
  return phase === "running" ? buildGarmentFit(frame.points, definition) : null;
}

export function App() {
  const stageRef = useRef<HTMLElement | null>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const webglCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const [outfit, setOutfit] = useState<OutfitSelection>(() => ({
    upperId: firstGarment("upper", "outerwear"),
    lowerId: firstGarment("lower"),
  }));
  const [editorSlot, setEditorSlot] = useState<EditorSlot>("upper");
  const [appearances, setAppearances] = useState(readAppearances);
  const [recentColors, setRecentColors] = useState(readRecentColors);
  const [garmentLoading, setGarmentLoading] = useState(false);
  const [toast, setToast] = useState<string | null>(null);
  const activeGarments = useMemo(() => {
    const upper = findGarment(outfit.upperId);
    return upper.slot === "onePiece" || !outfit.lowerId ? [upper] : [upper, findGarment(outfit.lowerId)];
  }, [outfit]);
  const selectedId = editorSlot === "upper" ? outfit.upperId : outfit.lowerId ?? outfit.upperId;
  const definition = useMemo(() => findGarment(selectedId), [selectedId]);
  const appearance = appearances[selectedId] ?? createDefaultAppearance(definition);
  const tracker = usePoseTracker({ videoRef, stageRef, definition: activeGarments[0] });
  const debugPose = import.meta.env.DEV && new URLSearchParams(window.location.search).get("debugPose") === "1";
  const debugFrame = useMemo<PoseFrame | null>(
    () => (debugPose ? createDebugPoseFrame(activeGarments[0]) : null),
    [activeGarments, debugPose],
  );
  const visibleFrame = debugFrame ?? tracker.frame;
  const visiblePhase = debugFrame ? "running" : tracker.phase;

  useEffect(() => {
    if (debugPose) return;
    const timeout = window.setTimeout(() => void tracker.start(), 0);
    return () => window.clearTimeout(timeout);
  }, [debugPose, tracker.start]);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      try {
        localStorage.setItem(APPEARANCE_KEY, JSON.stringify(appearances));
      } catch {
        // The fitting experience remains usable when storage is unavailable or full.
      }
    }, 180);
    return () => window.clearTimeout(timeout);
  }, [appearances]);
  useEffect(() => {
    const timeout = window.setTimeout(() => {
      try {
        localStorage.setItem(RECENT_KEY, JSON.stringify(recentColors));
      } catch {
        // The fitting experience remains usable when storage is unavailable or full.
      }
    }, 180);
    return () => window.clearTimeout(timeout);
  }, [recentColors]);
  useEffect(() => {
    if (!toast) return;
    const timeout = window.setTimeout(() => setToast(null), 2600);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  const selectGarment = useCallback((garment: GarmentDefinition) => {
    if (garment.slot === "lower") {
      const deselecting = outfit.lowerId === garment.id;
      setOutfit((current) => ({ ...current, lowerId: deselecting ? null : garment.id }));
      setEditorSlot(deselecting ? "upper" : "lower");
    } else {
      setOutfit((current) => ({ ...current, upperId: garment.id }));
      setEditorSlot("upper");
    }
  }, [outfit.lowerId]);

  const updateAppearance = useCallback((next: GarmentAppearance) => {
    setAppearances((current) => ({ ...current, [selectedId]: next }));
  }, [selectedId]);

  const commitColor = useCallback((color: string) => {
    setRecentColors((current) => [color, ...current.filter((value) => value !== color)].slice(0, 5));
  }, []);

  const resetAppearance = useCallback(() => {
    setAppearances((current) => ({ ...current, [selectedId]: createDefaultAppearance(definition) }));
  }, [definition, selectedId]);

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
          garments={activeGarments.map((item) => ({
            definition: item,
            appearance: appearances[item.id] ?? createDefaultAppearance(item),
            fit: runningFit(visibleFrame, item, visiblePhase),
          }))}
          onStart={tracker.start}
          onCanvas={handleCanvas}
          onGarmentLoading={handleGarmentLoading}
          onGarmentError={handleGarmentError}
        />
        <ControlPanel
          outfit={outfit}
          editorSlot={editorSlot}
          selected={definition}
          appearance={appearance}
          recentColors={recentColors}
          onSelect={selectGarment}
          onEditorSlotChange={setEditorSlot}
          onAppearanceChange={updateAppearance}
          onColorCommit={commitColor}
          onReset={resetAppearance}
        />
      </div>

      {toast ? <div className="toast" role="status" aria-live="polite">{toast}</div> : null}
    </div>
  );
}
