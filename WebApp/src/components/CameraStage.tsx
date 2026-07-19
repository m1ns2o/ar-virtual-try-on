import { lazy, Suspense, type RefObject } from "react";

import type { TrackerErrorCode, TrackerPhase } from "../hooks/usePoseTracker";
import { ko } from "../i18n/ko";
import type { GarmentAppearance, GarmentDefinition, PoseFrame } from "../types/pose";
import { PoseOverlay } from "./PoseOverlay";

const GarmentScene = lazy(() =>
  import("./GarmentScene").then((module) => ({ default: module.GarmentScene })),
);

interface Props {
  stageRef: RefObject<HTMLElement | null>;
  videoRef: RefObject<HTMLVideoElement | null>;
  phase: TrackerPhase;
  errorCode: TrackerErrorCode | null;
  frame: PoseFrame;
  definition: GarmentDefinition;
  appearance: GarmentAppearance;
  onStart: () => void;
  onCanvas: (canvas: HTMLCanvasElement) => void;
  onGarmentLoading: (loading: boolean) => void;
  onGarmentError: () => void;
}

export function CameraStage({
  stageRef,
  videoRef,
  phase,
  errorCode,
  frame,
  definition,
  appearance,
  onStart,
  onCanvas,
  onGarmentLoading,
  onGarmentError,
}: Props) {
  const error = errorCode ? ko.errors[errorCode] : null;
  const running = phase === "running";
  const aligned = running && frame.quality === "fitted";

  return (
    <main ref={stageRef} className={`camera-stage phase-${phase}`} aria-label="3D 가상 피팅 카메라">
      <video
        ref={videoRef}
        className="camera-video"
        muted
        autoPlay
        playsInline
        aria-label="실시간 전면 카메라 미리보기"
      />
      <div className="camera-vignette" aria-hidden="true" />
      <PoseOverlay points={frame.points} visible={running} />
      <Suspense fallback={null}>
        <GarmentScene
          definition={definition}
          appearance={appearance}
          fit={running ? frame.fit : null}
          onCanvas={onCanvas}
          onLoadingChange={onGarmentLoading}
          onLoadError={onGarmentError}
        />
      </Suspense>

      {running ? (
        <>
          <div className={`body-guide${aligned ? " aligned" : ""}`} aria-hidden="true">
            <span className="guide-head" />
            <span className="guide-body" />
            <span className="guide-feet" />
          </div>
          {!aligned && (
            <div className={`alignment-message${frame.quality === "outOfFrame" ? " warning" : ""}`} role="status">
              <strong>{ko.guide.title}</strong>
              <span>{ko.guide.body}</span>
            </div>
          )}
          <div className="local-performance" title="포즈 추론은 브라우저 안에서만 실행됩니다">
            LOCAL · {frame.inferenceFps > 0 ? `${Math.round(frame.inferenceFps)} FPS` : "준비 중"}
          </div>
        </>
      ) : null}

      {(phase === "requesting" || phase === "loadingModel") && (
        <div className="system-layer" role="status" aria-live="polite">
          <div className="loading-ring" aria-hidden="true"><i /></div>
          <h2>{phase === "requesting" ? ko.camera.permissionTitle : ko.camera.modelTitle}</h2>
          <p>{phase === "requesting" ? ko.camera.permissionBody : ko.camera.modelBody}</p>
        </div>
      )}

      {phase === "error" && error && (
        <div className="system-layer error-layer" role="alert">
          <div className="error-symbol" aria-hidden="true">!</div>
          <h2>{error.title}</h2>
          <p>{error.body}</p>
          <button type="button" className="primary-button compact" onClick={onStart}>
            {ko.camera.retry}
          </button>
        </div>
      )}
    </main>
  );
}
