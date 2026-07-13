import { ko } from "../i18n/ko";
import type { PoseQuality } from "../types/pose";
import type { TrackerPhase } from "../hooks/usePoseTracker";

interface Props {
  phase: TrackerPhase;
  quality: PoseQuality;
  garmentLoading: boolean;
}

export function StatusPill({ phase, quality, garmentLoading }: Props) {
  let state: PoseQuality | "modelLoading" = quality;
  if (phase === "idle" || phase === "requesting" || phase === "loadingModel") state = "ready";
  if (garmentLoading && phase === "running") state = "modelLoading";
  return (
    <div className={`status-pill status-${state}`} role="status" aria-live="polite">
      <span className="status-dot" />
      {ko.status[state]}
    </div>
  );
}
