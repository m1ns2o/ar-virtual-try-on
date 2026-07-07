# Ubuntu Migration Notes

Unity version: 6000.0.62f1

## Recommended Environment

- Ubuntu 24.04 or newer
- Unity Hub with Unity 6000.0.62f1 installed
- Intel Arc/Xe graphics driver through a current Mesa stack
- X11 session preferred for first validation
- Webcam exposed as `/dev/video*`

## First Run

1. Clone or copy this repository to Ubuntu.
2. Open the project folder in Unity 6000.0.62f1.
3. Open `Assets/ARCloset/Scenes/ARClosetDemo.unity`.
4. Run the scene and confirm the on-screen MediaPipe status.

Expected status examples:

- `Unity MediaPipe tracking GPU/pose_landmarker_lite.bytes ...`: GPU path is active.
- `Unity MediaPipe tracking CPU/pose_landmarker_lite.bytes ...`: GPU was unavailable and CPU fallback is active.

## Graphics Notes

For MediaPipe GPU input, Linux/Android are the supported paths in the bundled package. On Ubuntu, prefer OpenGLCore over Vulkan if GPU initialization fails.

Useful checks:

```bash
glxinfo -B
vulkaninfo --summary
ls /dev/video*
```
