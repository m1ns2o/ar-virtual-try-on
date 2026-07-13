import { useCallback, useEffect, useRef, useState, type RefObject } from "react";
import type {
  NormalizedLandmark,
  PoseLandmarker,
  PoseLandmarkerResult,
} from "@mediapipe/tasks-vision";

import { mapPacketLandmarks } from "../lib/coordinates";
import { evaluatePoseQuality, FitStabilizer } from "../lib/fit";
import { PoseFilter } from "../lib/poseFilter";
import type { GarmentDefinition, MediaPipePosePacket, PoseFrame, PoseLandmark } from "../types/pose";

export type TrackerPhase = "idle" | "requesting" | "loadingModel" | "running" | "error";
export type TrackerErrorCode = "insecure" | "unsupported" | "denied" | "noCamera" | "camera" | "model";

interface Options {
  videoRef: RefObject<HTMLVideoElement | null>;
  stageRef: RefObject<HTMLElement | null>;
  definition: GarmentDefinition;
}

const EMPTY_FRAME: PoseFrame = {
  packet: null,
  points: Array.from({ length: 33 }, () => null),
  fit: null,
  quality: "ready",
  inferenceFps: 0,
  inferenceSize: "—",
};

function profile() {
  const extended = navigator as Navigator & { deviceMemory?: number };
  const coarse = window.matchMedia?.("(pointer: coarse)").matches ?? false;
  const lowPower =
    (navigator.hardwareConcurrency ?? 8) <= 4 ||
    (extended.deviceMemory ?? 8) <= 4 ||
    (coarse && Math.min(innerWidth, innerHeight) < 820);
  return {
    lowPower,
    width: lowPower ? 720 : 1280,
    height: lowPower ? 1280 : 720,
    longEdge: lowPower ? 288 : 384,
    interval: lowPower ? 50 : 33,
  };
}

function isSupported() {
  if (!navigator.mediaDevices?.getUserMedia || typeof WebAssembly === "undefined") return false;
  const canvas = document.createElement("canvas");
  return Boolean(canvas.getContext("webgl2") || canvas.getContext("webgl"));
}

function isSecure() {
  return window.isSecureContext || ["localhost", "127.0.0.1", "::1"].includes(location.hostname);
}

function cameraError(error: unknown): TrackerErrorCode {
  if (error instanceof DOMException) {
    if (["NotAllowedError", "SecurityError"].includes(error.name)) return "denied";
    if (["NotFoundError", "OverconstrainedError"].includes(error.name)) return "noCamera";
  }
  return "camera";
}

function cloneLandmark(landmark: NormalizedLandmark): PoseLandmark {
  const presence = (landmark as NormalizedLandmark & { presence?: number }).presence;
  return {
    x: landmark.x,
    y: landmark.y,
    z: landmark.z,
    visibility: landmark.visibility ?? 1,
    presence: presence ?? landmark.visibility ?? 1,
  };
}

function waitForVideo(video: HTMLVideoElement): Promise<void> {
  if (video.readyState >= HTMLMediaElement.HAVE_METADATA && video.videoWidth > 0) return Promise.resolve();
  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => finish(new Error("Camera metadata timed out.")), 8000);
    const finish = (error?: Error) => {
      window.clearTimeout(timeout);
      video.removeEventListener("loadedmetadata", loaded);
      video.removeEventListener("error", failed);
      if (error) reject(error);
      else resolve();
    };
    const loaded = () => finish();
    const failed = () => finish(new Error("Camera video failed."));
    video.addEventListener("loadedmetadata", loaded, { once: true });
    video.addEventListener("error", failed, { once: true });
  });
}

export function usePoseTracker({ videoRef, stageRef, definition }: Options) {
  const [phase, setPhase] = useState<TrackerPhase>("idle");
  const [errorCode, setErrorCode] = useState<TrackerErrorCode | null>(null);
  const [frame, setFrame] = useState<PoseFrame>(EMPTY_FRAME);
  const streamRef = useRef<MediaStream | null>(null);
  const landmarkerRef = useRef<PoseLandmarker | null>(null);
  const rafRef = useRef(0);
  const sessionRef = useRef(0);
  const startingRef = useRef(false);
  const definitionRef = useRef(definition);
  const filterRef = useRef(new PoseFilter());
  const fitRef = useRef(new FitStabilizer());
  const wasFittedRef = useRef(false);

  useEffect(() => {
    definitionRef.current = definition;
    fitRef.current.reset();
  }, [definition]);

  const release = useCallback(() => {
    cancelAnimationFrame(rafRef.current);
    try {
      landmarkerRef.current?.close();
    } catch {
      // A partially initialized task may already be closed.
    }
    landmarkerRef.current = null;
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
    if (videoRef.current) videoRef.current.srcObject = null;
    filterRef.current.reset();
    fitRef.current.reset();
    wasFittedRef.current = false;
  }, [videoRef]);

  const stop = useCallback(() => {
    sessionRef.current += 1;
    startingRef.current = false;
    release();
    setErrorCode(null);
    setPhase("idle");
    setFrame(EMPTY_FRAME);
  }, [release]);

  const start = useCallback(async () => {
    if (startingRef.current) return;
    startingRef.current = true;
    const session = sessionRef.current + 1;
    sessionRef.current = session;
    release();
    setErrorCode(null);
    setFrame(EMPTY_FRAME);

    if (!isSecure()) {
      setErrorCode("insecure");
      setPhase("error");
      startingRef.current = false;
      return;
    }
    if (!isSupported()) {
      setErrorCode("unsupported");
      setPhase("error");
      startingRef.current = false;
      return;
    }

    const device = profile();
    const video = videoRef.current;
    if (!video) {
      setErrorCode("camera");
      setPhase("error");
      startingRef.current = false;
      return;
    }

    setPhase("requesting");
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: {
          facingMode: { ideal: "user" },
          width: { ideal: device.width },
          height: { ideal: device.height },
          frameRate: { ideal: device.lowPower ? 24 : 30, max: 30 },
        },
      });
      if (sessionRef.current !== session) {
        stream.getTracks().forEach((track) => track.stop());
        return;
      }
      streamRef.current = stream;
      video.srcObject = stream;
      await waitForVideo(video);
      await video.play();
      stream.getVideoTracks()[0]?.addEventListener(
        "ended",
        () => {
          if (sessionRef.current !== session) return;
          release();
          setErrorCode("camera");
          setPhase("error");
        },
        { once: true },
      );
    } catch (error) {
      if (sessionRef.current === session) {
        release();
        setErrorCode(cameraError(error));
        setPhase("error");
      }
      startingRef.current = false;
      return;
    }

    setPhase("loadingModel");
    try {
      const { FilesetResolver, PoseLandmarker } = await import("@mediapipe/tasks-vision");
      const base = import.meta.env.BASE_URL;
      const wasmPath = new URL(`${base}mediapipe/wasm`, location.origin).href;
      const modelPath = new URL(`${base}models/pose_landmarker_lite.task`, location.origin).href;
      const fileset = await FilesetResolver.forVisionTasks(wasmPath);
      const landmarker = await PoseLandmarker.createFromOptions(fileset, {
        baseOptions: { modelAssetPath: modelPath, delegate: "CPU" },
        runningMode: "VIDEO",
        numPoses: 1,
        minPoseDetectionConfidence: 0.45,
        minPosePresenceConfidence: 0.45,
        minTrackingConfidence: 0.45,
        outputSegmentationMasks: false,
      });
      if (sessionRef.current !== session) {
        landmarker.close();
        return;
      }
      landmarkerRef.current = landmarker;
    } catch {
      if (sessionRef.current === session) {
        release();
        setErrorCode("model");
        setPhase("error");
      }
      startingRef.current = false;
      return;
    }

    if (sessionRef.current !== session) return;
    setPhase("running");
    startingRef.current = false;
    const inferenceCanvas = document.createElement("canvas");
    const context = inferenceCanvas.getContext("2d", { alpha: false });
    if (!context) {
      release();
      setErrorCode("unsupported");
      setPhase("error");
      return;
    }

    let lastInferenceAt = 0;
    let interval = device.interval;
    let longEdge = device.longEdge;
    let durationEma = 0;
    let fpsWindowStarted = performance.now();
    let fpsFrames = 0;
    let measuredFps = 0;

    const publish = (result: PoseLandmarkerResult, timestampMs: number) => {
      const landmarks = result.landmarks[0]?.map(cloneLandmark) ?? [];
      const packet: MediaPipePosePacket | null =
        landmarks.length >= 33
          ? {
              version: 1,
              timestamp: timestampMs / 1000,
              frameWidth: inferenceCanvas.width,
              frameHeight: inferenceCanvas.height,
              displayRotationDegrees: 0,
              inputMirrored: false,
              previewMirrored: true,
              landmarks,
              worldLandmarks: result.worldLandmarks[0]?.map(cloneLandmark),
            }
          : null;
      const stageRect = stageRef.current?.getBoundingClientRect();
      const mapped =
        packet && stageRect
          ? mapPacketLandmarks(packet, { width: stageRect.width, height: stageRect.height })
          : Array.from({ length: 33 }, () => null);
      const filtered = filterRef.current.update(mapped, timestampMs);
      const fit = fitRef.current.update(filtered, definitionRef.current, timestampMs);
      const quality = evaluatePoseQuality(filtered, fit, wasFittedRef.current);
      if (quality === "fitted") wasFittedRef.current = true;

      fpsFrames += 1;
      if (timestampMs - fpsWindowStarted >= 1000) {
        measuredFps = (fpsFrames * 1000) / (timestampMs - fpsWindowStarted);
        fpsFrames = 0;
        fpsWindowStarted = timestampMs;
      }
      setFrame({
        packet,
        points: filtered,
        fit,
        quality,
        inferenceFps: measuredFps,
        inferenceSize: `${inferenceCanvas.width}×${inferenceCanvas.height}`,
      });
    };

    const infer = (now: number) => {
      if (sessionRef.current !== session || !landmarkerRef.current) return;
      if (video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA || now - lastInferenceAt < interval) {
        rafRef.current = requestAnimationFrame(infer);
        return;
      }
      lastInferenceAt = now;
      const aspect = Math.max(0.1, video.videoWidth / Math.max(1, video.videoHeight));
      if (aspect >= 1) {
        inferenceCanvas.width = longEdge;
        inferenceCanvas.height = Math.max(1, Math.round(longEdge / aspect));
      } else {
        inferenceCanvas.height = longEdge;
        inferenceCanvas.width = Math.max(1, Math.round(longEdge * aspect));
      }
      context.drawImage(video, 0, 0, inferenceCanvas.width, inferenceCanvas.height);
      const startedAt = performance.now();
      try {
        landmarkerRef.current.detectForVideo(inferenceCanvas, now, (result) => publish(result, now));
      } catch {
        // Ignore a single malformed frame while the camera rotates or resizes.
      }
      const duration = performance.now() - startedAt;
      durationEma = durationEma === 0 ? duration : durationEma * 0.85 + duration * 0.15;
      if (durationEma > 58) {
        longEdge = 256;
        interval = 67;
      } else if (durationEma > 38) {
        longEdge = Math.min(longEdge, 288);
        interval = 50;
      }
      rafRef.current = requestAnimationFrame(infer);
    };
    rafRef.current = requestAnimationFrame(infer);
  }, [release, stageRef, videoRef]);

  useEffect(
    () => () => {
      sessionRef.current += 1;
      release();
    },
    [release],
  );

  return { phase, errorCode, frame, start, stop };
}
