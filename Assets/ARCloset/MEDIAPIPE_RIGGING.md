# MediaPipe Rigging MVP

This Unity desktop MVP can run pose tracking in two modes:

- Unity-native: `homuler/MediaPipeUnityPlugin` PoseLandmarker runs inside Unity.
- Python bridge: Python MediaPipe sends Pose landmarks over UDP as a fallback.

## Run

Unity-native mode is now the default for the demo scene:

1. Open `Assets/ARCloset/Scenes/ARClosetDemo.unity`.
2. Press Play.
3. `MediaPipeUnityPoseSource` starts `WebCamTexture`, runs `PoseLandmarker`, and pushes landmarks into `MediaPipePoseReceiver`.
4. If the preview is gray or black, press `C` to switch webcam devices. The demo also auto-skips flat gray/black camera feeds when multiple devices are available.

The model used by the Unity-native path is:

`Assets/StreamingAssets/MediaPipe/pose_landmarker_lite.bytes`

Python bridge fallback from the repository root:

```powershell
py -3.12 -m venv .venv-mediapipe
.\.venv-mediapipe\Scripts\python.exe -m pip install -r tools\requirements-mediapipe.txt
.\.venv-mediapipe\Scripts\python.exe tools\mediapipe_pose_udp_sender.py --camera 0 --port 5052 --video-port 5053
```

Then open `Assets/ARCloset/Scenes/ARClosetDemo.unity` in Unity and press Play.

## Controls

- `1`: Polo shirt
- `2`: Fisherman sweater
- `3`: Wool pants
- `4`: Short-sleeve flapper dress
- `5`: High-neck short-sleeve dress
- `C`: Switch webcam device
- `D`: Toggle the debug skeleton overlay
- `M`: Mirror the webcam preview and MediaPipe input together
- `X`: Mirror the skeleton/garment overlay only
- `R`: Start/stop pose trace recording to `PoseTraces/*.ndjson`
- `P`: Replay the latest trace from `PoseTraces`
- `G`: Toggle synthetic pose input
- `V`: Start/stop fit stability monitoring to `PoseValidation/*.csv`
- Arrow keys: Move the garment fit offset while debugging
- `+` / `-`: Scale the garment fit while debugging
- `Backspace`: Reset garment fit offset and scale
- Color field: enter a `#RRGGBB` color code and press `Apply` to tint the equipped garment
- Stripe field: enter a stripe `#RRGGBB` color code, adjust width/gap, then press `Apply` or `Clear`

## Data Path

`Webcam -> Unity WebCamTexture -> homuler PoseLandmarker -> MediaPipePoseReceiver.PushPacket -> MediaPipePoseRigDriver`

`Webcam -> Python MediaPipe Pose Landmarker -> UDP JSON -> MediaPipePoseReceiver -> MediaPipePoseRigDriver`

`PoseTraces/*.ndjson -> PoseTraceReplaySource -> MediaPipePoseReceiver.PushPacket -> MediaPipePoseRigDriver`

`SyntheticPoseSource -> MediaPipePoseReceiver.PushPacket -> MediaPipePoseRigDriver`

`Webcam -> Python JPEG frame stream -> TCP 5053 -> MediaPipeVideoReceiver -> LiveVideoBackground`

The rig driver uses MediaPipe pose landmarks for shoulders, elbows, wrists, hips, knees, and ankles. MediaPipe is the CV layer for this MVP; a separate body detector is not needed just to locate the torso and arms.

Garment placement is calculated from a body fit frame:

- Upper/outerwear: shoulder center, estimated or tracked hip center, shoulder width, torso height.
- Lower: hip, knee, ankle landmarks.
- One-piece: shoulder, hip, knee landmarks.

The selected MakeHuman garment is aligned by its renderer bounds, not by the prefab pivot. This keeps imported OBJ meshes from drifting when their origin is not at the visual center of the clothing. The rig driver also ignores stale pose packets after `1.5s` and clamps the fitted garment target to the camera viewport so old or partial detections do not throw the garment off-screen.

The current garments are OBJ meshes, so the base garment still uses pose-driven overlay alignment and garment anchoring. To demonstrate arm-following rig behavior, the scene also creates a dynamic sleeve rig:

- T-shirts show upper-arm sleeve segments.
- Short-sleeve dresses show upper-arm sleeve segments.
- Long-sleeve garments such as sweaters, jackets, coats, and hoodies show upper-arm and forearm sleeve segments.
- Sleeve transforms are driven by shoulder, elbow, and wrist landmarks and reuse the current garment material.

This is a rigging MVP, not full cloth simulation. Production-quality sleeve bending should replace the procedural sleeve overlay with skinned garment meshes bound to the tracked rig, or a cloth simulation layer on top of the current landmark fit.

## Camera-free Validation

The demo scene includes a pose replay and synthetic validation path so garment fitting can be tested without someone standing in front of the camera.

- `PoseTraceRecorder` writes compact MediaPipe pose packets as newline-delimited JSON.
- `PoseTraceReplaySource` loads the latest `PoseTraces/*.ndjson`, stops the live Unity pose source while replaying, and injects packets through the same receiver path as the camera.
- `SyntheticPoseSource` generates stable, swaying, noisy, outlier, low-confidence, and missing-lower-body pose packets.
- `PoseFitStabilityMonitor` reports pass/fail windows using pose FPS, garment anchor jitter, scale jitter, target-center jitter, stale samples, and missing fit samples.

Use this flow for regression checks:

1. Press `R` while live tracking is working to record a trace, then press `R` again to stop.
2. Press `P` to replay the latest trace without the camera.
3. Press `G` to switch to a synthetic pose source when no trace is available.
4. Press `V` during replay or synthetic input to write stability metrics to `PoseValidation`.

The current demo scene is configured as an AR overlay:

- The webcam preview is not mirrored by default.
- The main camera is orthographic.
- `LiveVideoBackground` is resized at runtime to cover the whole camera view.
- `MediaPipePoseRigDriver` maps normalized landmark coordinates onto the same camera viewport plane as the webcam background.
- The debug skeleton renderers are visible by default. Use this first: if the skeleton does not sit on the person, fix camera/mirroring before tuning the garment.

## Unity MediaPipe Package

Installed package:

- `homuler/MediaPipeUnityPlugin` v0.16.3
- Package id: `com.github.homuler.mediapipe`
- License: MIT
- Source: https://github.com/homuler/MediaPipeUnityPlugin

The package is embedded under `Packages/com.github.homuler.mediapipe` because Unity's PackageCache rename step hit Windows `EPERM` errors in this project. The sample annotation UI and package test folders are disabled with `~` suffixes because the AR Closet runtime only needs the core Task API and PoseLandmarker types.
