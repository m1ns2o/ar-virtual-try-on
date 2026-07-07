using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using UnityEngine;

namespace ARCloset
{
    public sealed class MediaPipeUnityPoseSource : MonoBehaviour
    {
        private enum PoseInferenceBackend
        {
            Auto,
            CPU,
            GPU
        }
        [Header("Output")]
        [SerializeField] private MediaPipePoseReceiver receiver;
        [SerializeField] private Renderer previewRenderer;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private float previewPlaneDistance = 12f;

        [Header("Camera")]
        [SerializeField] private int cameraDeviceIndex;
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFps = 30;
        [SerializeField] private bool startOnEnable = true;
        [SerializeField] private bool mirrorPreview = true;
        [SerializeField] private bool mirrorInput = true;
        [SerializeField] private bool allowRuntimeCameraSwitch = true;
        [SerializeField] private bool autoSwitchFlatCameraFeed = true;
        [SerializeField] private KeyCode cycleCameraKey = KeyCode.C;
        [SerializeField] private KeyCode mirrorVideoAndInputKey = KeyCode.M;
        [SerializeField] private float flatCameraVarianceThreshold = 300f;
        [SerializeField] private float emptyCameraMeanEdgeThreshold = 22f;
        [SerializeField] private float flatCameraAutoSwitchSeconds = 4f;

        [Header("Pose Landmarker")]
        [SerializeField] private string modelFileName = "pose_landmarker_lite.bytes";
        [SerializeField] private float minPoseDetectionConfidence = 0.22f;
        [SerializeField] private float minPosePresenceConfidence = 0.22f;
        [SerializeField] private float minTrackingConfidence = 0.22f;
        [SerializeField] private bool preferLiteModel = true;
        [SerializeField] private PoseInferenceBackend inferenceBackend = PoseInferenceBackend.Auto;
        [SerializeField, Range(1, 60)] private int maxInferenceFps = 20;
        [SerializeField, Range(1, 4)] private int cpuFrameStride = 2;

        private readonly HashSet<string> blockedDeviceNames = new(StringComparer.OrdinalIgnoreCase);
        private WebCamTexture webcamTexture;
        private PoseLandmarker poseLandmarker;
        private TextureFrame textureFrame;
        private PoseLandmarkerResult poseResult;
        private Coroutine runCoroutine;
        private long lastTimestampMillis;
        private string status = "Unity MediaPipe idle";
        private string activeDeviceName = "default";
        private bool restartRequested;
        private float flatFrameStartedAt = -1f;
        private float lastFrameMean = -1f;
        private float lastFrameVariance = -1f;
        private bool useGpuInference;
        private int webcamFrameCounter;
        private float nextInferenceTime;
        private string activeModelFileName = "pose_landmarker_lite.bytes";

        public bool IsRunning => runCoroutine != null;
        public string Status => status;
        public string ActiveDeviceName => activeDeviceName;

        private void Awake()
        {
            Application.runInBackground = true;
        }

        private void Reset()
        {
            receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
        }

        private void OnEnable()
        {
            if (startOnEnable)
            {
                StartSource();
            }
        }

        private void Start()
        {
            if (startOnEnable && runCoroutine == null)
            {
                StartSource();
            }
        }

        private void Update()
        {
            if (allowRuntimeCameraSwitch && Input.GetKeyDown(cycleCameraKey))
            {
                CycleCameraDevice();
            }

            if (Input.GetKeyDown(mirrorVideoAndInputKey))
            {
                SetVideoAndInputMirrored(!mirrorPreview);
            }
        }

        private void OnDisable()
        {
            StopSource();
        }

        private void OnDestroy()
        {
            StopSource();
        }

        public void StartSource()
        {
            if (runCoroutine != null)
            {
                return;
            }

            status = "Unity MediaPipe starting";
            runCoroutine = StartCoroutine(RunPoseLandmarker());
        }

        public void CycleCameraDevice()
        {
            if (!SelectNextCameraDevice("manual switch"))
            {
                return;
            }

            if (runCoroutine != null)
            {
                restartRequested = true;
                return;
            }

            StartSource();
        }

        public void SetVideoAndInputMirrored(bool mirrored)
        {
            mirrorPreview = mirrored;
            mirrorInput = mirrored;
            ApplyPreviewTexture();
        }

        public void StopSource()
        {
            if (runCoroutine != null)
            {
                StopCoroutine(runCoroutine);
                runCoroutine = null;
            }

            DisposePoseLandmarker();
            DisposeWebcamTexture();

            status = "Unity MediaPipe stopped";
        }

        private IEnumerator RunPoseLandmarker()
        {
            restartRequested = false;
            flatFrameStartedAt = -1f;
            lastFrameMean = -1f;
            lastFrameVariance = -1f;

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                status = "Unity MediaPipe requesting webcam permission";
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                status = "Unity MediaPipe webcam permission denied";
                Debug.LogError(status);
                runCoroutine = null;
                yield break;
            }

            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            if (receiver == null)
            {
                status = "Unity MediaPipe missing receiver";
                Debug.LogError(status);
                runCoroutine = null;
                yield break;
            }

            string modelPath = ResolveModelPath();
            if (!File.Exists(modelPath))
            {
                status = $"Unity MediaPipe model missing: {modelPath}";
                Debug.LogError(status);
                runCoroutine = null;
                yield break;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            string deviceName = devices.Length > 0
                ? devices[Mathf.Clamp(cameraDeviceIndex, 0, devices.Length - 1)].name
                : null;
            activeDeviceName = string.IsNullOrEmpty(deviceName) ? "default" : deviceName;

            webcamTexture = string.IsNullOrEmpty(deviceName)
                ? new WebCamTexture(requestedWidth, requestedHeight, requestedFps)
                : new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);
            webcamTexture.Play();

            if (previewRenderer != null)
            {
                ApplyPreviewTexture();
            }

            float waitDeadline = Time.realtimeSinceStartup + 5f;
            while (webcamTexture.width <= 16 && Time.realtimeSinceStartup < waitDeadline)
            {
                status = "Unity MediaPipe waiting for webcam";
                yield return null;
            }

            if (webcamTexture.width <= 16)
            {
                BlockActiveCameraDevice();
                if (SelectNextCameraDevice("webcam unavailable"))
                {
                    DisposeWebcamTexture();
                    runCoroutine = null;
                    StartSource();
                    yield break;
                }

                status = "Unity MediaPipe webcam unavailable";
                Debug.LogWarning(status);
                DisposeWebcamTexture();
                runCoroutine = null;
                yield break;
            }

            FitPreviewToCamera();
            yield return InitializeGpuIfRequested();
            InitializePoseLandmarker(modelPath, useGpuInference);
            textureFrame = new TextureFrame(webcamTexture.width, webcamTexture.height, TextureFormat.RGBA32);
            poseResult = PoseLandmarkerResult.Alloc(1, false);

            WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

            while (isActiveAndEnabled && !restartRequested)
            {
                yield return waitForEndOfFrame;

                if (webcamTexture == null || !webcamTexture.didUpdateThisFrame)
                {
                    continue;
                }

                webcamFrameCounter++;
                if (!ShouldRunInferenceThisFrame())
                {
                    continue;
                }

                if (Time.frameCount % 30 == 0)
                {
                    ApplyPreviewTexture();
                    UpdateFrameHealth();
                }

                long timestampMillis = NextTimestampMillis();

                try
                {
                    ImageTransformationOptions transformation = GetInputTransformationOptions();
                    string backend = useGpuInference ? "GPU" : "CPU";
                    if (TryDetectPoseFrame(timestampMillis, transformation))
                    {
                        PushPoseResult(poseResult);
                        status = $"Unity MediaPipe tracking {backend}/{activeModelFileName} {activeDeviceName} {webcamTexture.width}x{webcamTexture.height}";
                    }
                    else
                    {
                        status = $"Unity MediaPipe searching {backend}/{activeModelFileName} {activeDeviceName} {webcamTexture.width}x{webcamTexture.height}";
                    }
                }
                catch (Exception exception)
                {
                    status = $"Unity MediaPipe error: {exception.Message}";
                    Debug.LogWarning(status);
                }
            }

            DisposePoseLandmarker();

            DisposeWebcamTexture();

            runCoroutine = null;

            if (restartRequested && isActiveAndEnabled)
            {
                restartRequested = false;
                StartSource();
            }
        }

        private bool SelectNextCameraDevice(string reason)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                status = "Unity MediaPipe no webcam devices";
                return false;
            }

            int startIndex = Mathf.Clamp(cameraDeviceIndex, 0, Mathf.Max(0, devices.Length - 1));
            for (int i = 1; i <= devices.Length; i++)
            {
                int candidateIndex = (startIndex + i) % devices.Length;
                string candidateName = devices[candidateIndex].name;
                if (ShouldSkipCameraDevice(candidateName))
                {
                    continue;
                }

                cameraDeviceIndex = candidateIndex;
                activeDeviceName = candidateName;
                status = $"Unity MediaPipe switching camera to {activeDeviceName} ({reason})";
                return true;
            }

            if (blockedDeviceNames.Count > 0)
            {
                blockedDeviceNames.Clear();
                return SelectNextCameraDevice(reason);
            }

            status = "Unity MediaPipe no usable webcam devices";
            return false;
        }

        private bool ShouldSkipCameraDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return false;
            }

            if (blockedDeviceNames.Contains(deviceName))
            {
                return true;
            }

            return deviceName.IndexOf("EOS Webcam Utility Pro", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BlockActiveCameraDevice()
        {
            if (!string.IsNullOrWhiteSpace(activeDeviceName))
            {
                blockedDeviceNames.Add(activeDeviceName);
            }
        }

        private void DisposeWebcamTexture()
        {
            if (webcamTexture == null)
            {
                return;
            }

            if (webcamTexture.isPlaying)
            {
                webcamTexture.Stop();
            }

            Destroy(webcamTexture);
            webcamTexture = null;
        }

        private void UpdateFrameHealth()
        {
            if (!autoSwitchFlatCameraFeed || webcamTexture == null || WebCamTexture.devices.Length <= 1)
            {
                return;
            }

            if (!TryComputeFrameHealth(webcamTexture, out float mean, out float variance))
            {
                return;
            }

            lastFrameMean = mean;
            lastFrameVariance = variance;
            bool edgeMean = mean <= emptyCameraMeanEdgeThreshold || mean >= 255f - emptyCameraMeanEdgeThreshold;
            bool flatFrame = variance <= flatCameraVarianceThreshold || edgeMean;
            if (!flatFrame)
            {
                flatFrameStartedAt = -1f;
                return;
            }

            if (flatFrameStartedAt < 0f)
            {
                flatFrameStartedAt = Time.realtimeSinceStartup;
                return;
            }

            if (Time.realtimeSinceStartup - flatFrameStartedAt < flatCameraAutoSwitchSeconds)
            {
                return;
            }

            BlockActiveCameraDevice();
            CycleCameraDevice();
        }

        private static bool TryComputeFrameHealth(WebCamTexture texture, out float mean, out float variance)
        {
            mean = 0f;
            variance = 0f;

            try
            {
                Color32[] pixels = texture.GetPixels32();
                if (pixels == null || pixels.Length == 0)
                {
                    return false;
                }

                int step = Mathf.Max(1, pixels.Length / 256);
                int count = 0;
                float sum = 0f;
                float sumSquares = 0f;

                for (int i = 0; i < pixels.Length; i += step)
                {
                    Color32 pixel = pixels[i];
                    float luma = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                    sum += luma;
                    sumSquares += luma * luma;
                    count++;
                }

                if (count == 0)
                {
                    return false;
                }

                mean = sum / count;
                variance = Mathf.Max(0f, sumSquares / count - mean * mean);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldRunInferenceThisFrame()
        {
            if (!useGpuInference && cpuFrameStride > 1 && webcamFrameCounter % Mathf.Max(1, cpuFrameStride) != 0)
            {
                return false;
            }

            int fps = Mathf.Max(1, maxInferenceFps);
            float now = Time.realtimeSinceStartup;
            if (now < nextInferenceTime)
            {
                return false;
            }

            nextInferenceTime = now + 1f / fps;
            return true;
        }

        private bool TryDetectPoseFrame(long timestampMillis, ImageTransformationOptions transformation)
        {
            ImageProcessingOptions imageOptions = new ImageProcessingOptions(rotationDegrees: 0);

            if (useGpuInference)
            {
                textureFrame.ReadTextureOnGPU(webcamTexture, transformation.flipHorizontally, transformation.flipVertically);
                using Mediapipe.Image image = textureFrame.BuildGPUImage(GpuManager.GetGlContext());
                return poseLandmarker.TryDetectForVideo(image, timestampMillis, imageOptions, ref poseResult);
            }

            textureFrame.ReadTextureOnCPU(webcamTexture, transformation.flipHorizontally, transformation.flipVertically);
            using Mediapipe.Image cpuImage = textureFrame.BuildCPUImage();
            return poseLandmarker.TryDetectForVideo(cpuImage, timestampMillis, imageOptions, ref poseResult);
        }

        private IEnumerator InitializeGpuIfRequested()
        {
            useGpuInference = false;

            if (!ShouldUseGpuInference())
            {
                yield break;
            }

            yield return GpuManager.Initialize();
            useGpuInference = GpuManager.IsInitialized && GpuManager.GpuResources != null && GpuManager.GetGlContext() != null;
            if (!useGpuInference)
            {
                status = "Unity MediaPipe GPU unavailable; using CPU";
            }
        }

        private bool ShouldUseGpuInference()
        {
            if (!SupportsGpuImageInput())
            {
                return false;
            }

            return inferenceBackend == PoseInferenceBackend.GPU || inferenceBackend == PoseInferenceBackend.Auto;
        }

        private static bool SupportsGpuImageInput()
        {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_ANDROID
            return true;
#else
            return false;
#endif
        }
        private ImageTransformationOptions GetInputTransformationOptions()
        {
            return ImageTransformationOptions.Build(ShouldMirrorInput(), IsVideoVerticallyFlipped(), GetVideoRotationAngle());
        }

        private UnityEngine.Rect GetPreviewUvRect()
        {
            UnityEngine.Rect rect = new UnityEngine.Rect(0f, 0f, 1f, 1f);

            if (IsVideoVerticallyFlipped())
            {
                rect = FlipVertically(rect);
            }

            if (ShouldMirrorPreview())
            {
                RotationAngle rotation = GetVideoRotationAngle();
                rect = rotation == RotationAngle.Rotation0 || rotation == RotationAngle.Rotation180
                    ? FlipHorizontally(rect)
                    : FlipVertically(rect);
            }

            return rect;
        }

        private bool ShouldMirrorPreview()
        {
            return mirrorPreview;
        }

        private bool ShouldMirrorInput()
        {
            return mirrorInput;
        }

        private bool IsFrontFacingCamera()
        {
            if (webcamTexture == null)
            {
                return false;
            }

            foreach (WebCamDevice device in WebCamTexture.devices)
            {
                if (device.name == webcamTexture.deviceName)
                {
                    return device.isFrontFacing;
                }
            }

            return false;
        }

        private bool IsVideoVerticallyFlipped()
        {
            return webcamTexture != null && webcamTexture.videoVerticallyMirrored;
        }

        private static UnityEngine.Rect FlipHorizontally(UnityEngine.Rect rect)
        {
            return new UnityEngine.Rect(1f - rect.x, rect.y, -rect.width, rect.height);
        }

        private static UnityEngine.Rect FlipVertically(UnityEngine.Rect rect)
        {
            return new UnityEngine.Rect(rect.x, 1f - rect.y, rect.width, -rect.height);
        }

        private void ApplyPreviewTexture()
        {
            if (previewRenderer == null || webcamTexture == null)
            {
                return;
            }

            Material material = previewRenderer.material;
            material.mainTexture = webcamTexture;
            material.color = Color.white;

            UnityEngine.Rect uvRect = GetPreviewUvRect();
            Vector2 scale = new Vector2(uvRect.width, uvRect.height);
            Vector2 offset = new Vector2(uvRect.x, uvRect.y);

            material.mainTextureScale = scale;
            material.mainTextureOffset = offset;

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", webcamTexture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", webcamTexture);
                material.SetTextureScale("_MainTex", scale);
                material.SetTextureOffset("_MainTex", offset);
            }
        }

        private void FitPreviewToCamera()
        {
            if (previewRenderer == null)
            {
                return;
            }

            if (previewCamera == null)
            {
                previewCamera = Camera.main;
            }

            if (previewCamera == null || webcamTexture == null)
            {
                return;
            }

            float imageAspect = FrameWidth() > 0 && FrameHeight() > 0
                ? (float)FrameWidth() / FrameHeight()
                : 4f / 3f;

            Transform target = previewRenderer.transform;
            target.rotation = previewCamera.transform.rotation * Quaternion.AngleAxis(DisplayRotationDegrees(), Vector3.forward);

            if (previewCamera.orthographic)
            {
                float viewHeight = previewCamera.orthographicSize * 2f;
                float viewWidth = viewHeight * previewCamera.aspect;
                float planeWidth = viewWidth;
                float planeHeight = viewWidth / imageAspect;

                if (planeHeight < viewHeight)
                {
                    planeHeight = viewHeight;
                    planeWidth = viewHeight * imageAspect;
                }

                target.position = previewCamera.transform.position + previewCamera.transform.forward * previewPlaneDistance;
                target.localScale = new Vector3(planeWidth, planeHeight, 1f);
                return;
            }

            float frustumHeight = 2f * previewPlaneDistance * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * previewCamera.aspect;
            float width = frustumWidth;
            float height = frustumWidth / imageAspect;

            if (height < frustumHeight)
            {
                height = frustumHeight;
                width = frustumHeight * imageAspect;
            }

            target.position = previewCamera.transform.position + previewCamera.transform.forward * previewPlaneDistance;
            target.localScale = new Vector3(width, height, 1f);
        }

        private int FrameWidth()
        {
            if (webcamTexture == null)
            {
                return requestedWidth;
            }

            return webcamTexture.width;
        }

        private int FrameHeight()
        {
            if (webcamTexture == null)
            {
                return requestedHeight;
            }

            return webcamTexture.height;
        }

        private int NormalizedVideoRotationDegrees()
        {
            if (webcamTexture == null)
            {
                return 0;
            }

            int rotation = webcamTexture.videoRotationAngle % 360;
            return rotation < 0 ? rotation + 360 : rotation;
        }

        private RotationAngle GetVideoRotationAngle()
        {
            return (RotationAngle)NormalizedVideoRotationDegrees();
        }

        private int DisplayRotationDegrees()
        {
            int rotation = NormalizedVideoRotationDegrees();
            return rotation == 0 ? 0 : 360 - rotation;
        }

        private void InitializePoseLandmarker(string modelPath, bool preferGpu)
        {
            DisposePoseLandmarker();

            useGpuInference = preferGpu;
            try
            {
                poseLandmarker = CreatePoseLandmarker(modelPath, useGpuInference);
            }
            catch (Exception exception) when (useGpuInference)
            {
                Debug.LogWarning($"MediaPipe GPU initialization failed, falling back to CPU: {exception.Message}");
                useGpuInference = false;
                poseLandmarker = CreatePoseLandmarker(modelPath, false);
            }

            lastTimestampMillis = 0;
        }

        private PoseLandmarker CreatePoseLandmarker(string modelPath, bool useGpu)
        {
            BaseOptions baseOptions = new BaseOptions(
                useGpu ? BaseOptions.Delegate.GPU : BaseOptions.Delegate.CPU,
                modelAssetPath: modelPath);
            PoseLandmarkerOptions options = new PoseLandmarkerOptions(
                baseOptions,
                RunningMode.VIDEO,
                numPoses: 1,
                minPoseDetectionConfidence: minPoseDetectionConfidence,
                minPosePresenceConfidence: minPosePresenceConfidence,
                minTrackingConfidence: minTrackingConfidence,
                outputSegmentationMasks: false);
            return PoseLandmarker.CreateFromOptions(options, useGpu ? GpuManager.GpuResources : null);
        }

        private string EffectiveModelFileName()
        {
            if (!preferLiteModel || string.IsNullOrWhiteSpace(modelFileName))
            {
                return string.IsNullOrWhiteSpace(modelFileName) ? "pose_landmarker_lite.bytes" : modelFileName;
            }

            return modelFileName.Replace("pose_landmarker_full.bytes", "pose_landmarker_lite.bytes");
        }
        private void DisposePoseLandmarker()
        {
            poseLandmarker?.Close();
            poseLandmarker = null;

            textureFrame?.Dispose();
            textureFrame = null;

            if (useGpuInference && GpuManager.IsInitialized)
            {
                GpuManager.Shutdown();
            }

            useGpuInference = false;
        }

        private string ResolveModelPath()
        {
            activeModelFileName = EffectiveModelFileName();
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "MediaPipe", activeModelFileName);
            if (File.Exists(streamingPath))
            {
                return streamingPath;
            }

            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Packages",
                "com.github.homuler.mediapipe",
                "PackageResources",
                "MediaPipe",
                activeModelFileName));
        }

        private long NextTimestampMillis()
        {
            long timestampMillis = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
            if (timestampMillis <= lastTimestampMillis)
            {
                timestampMillis = lastTimestampMillis + 1;
            }

            lastTimestampMillis = timestampMillis;
            return timestampMillis;
        }

        private void PushPoseResult(PoseLandmarkerResult result)
        {
            if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
            {
                return;
            }

            List<NormalizedLandmark> landmarks = result.poseLandmarks[0].landmarks;
            if (landmarks == null || landmarks.Count < 33)
            {
                return;
            }

            MediaPipePosePacket packet = new MediaPipePosePacket
            {
                version = 1,
                timestamp = Time.realtimeSinceStartupAsDouble,
                frameWidth = FrameWidth(),
                frameHeight = FrameHeight(),
                displayRotationDegrees = DisplayRotationDegrees(),
                inputMirrored = ShouldMirrorInput(),
                previewMirrored = ShouldMirrorPreview(),
                landmarks = ConvertLandmarks(landmarks),
                worldLandmarks = ConvertWorldLandmarks(result),
            };

            receiver.PushPacket(packet, "Unity PoseLandmarker");
        }

        private static MediaPipePoseLandmark[] ConvertLandmarks(IReadOnlyList<NormalizedLandmark> source)
        {
            MediaPipePoseLandmark[] landmarks = new MediaPipePoseLandmark[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                NormalizedLandmark landmark = source[i];
                landmarks[i] = new MediaPipePoseLandmark
                {
                    x = landmark.x,
                    y = landmark.y,
                    z = landmark.z,
                    visibility = landmark.visibility ?? 1f,
                    presence = landmark.presence ?? landmark.visibility ?? 1f,
                };
            }

            return landmarks;
        }

        private static MediaPipePoseLandmark[] ConvertWorldLandmarks(PoseLandmarkerResult result)
        {
            if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0)
            {
                return null;
            }

            List<Landmark> source = result.poseWorldLandmarks[0].landmarks;
            if (source == null)
            {
                return null;
            }

            MediaPipePoseLandmark[] landmarks = new MediaPipePoseLandmark[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                Landmark landmark = source[i];
                landmarks[i] = new MediaPipePoseLandmark
                {
                    x = landmark.x,
                    y = landmark.y,
                    z = landmark.z,
                    visibility = landmark.visibility ?? 1f,
                    presence = landmark.presence ?? landmark.visibility ?? 1f,
                };
            }

            return landmarks;
        }

        private void OnGUI()
        {
            GUI.Label(new UnityEngine.Rect(18f, 42f, 560f, 28f), status);
            GUI.Label(new UnityEngine.Rect(18f, 66f, 900f, 28f), $"Camera: {activeDeviceName}  mirror:{(mirrorPreview ? "ON" : "OFF")}  rawRot:{NormalizedVideoRotationDegrees()}  displayRot:{DisplayRotationDegrees()}  videoVFlip:{IsVideoVerticallyFlipped()}");

            if (webcamTexture != null)
            {
                ImageTransformationOptions transform = GetInputTransformationOptions();
                UnityEngine.Rect uvRect = GetPreviewUvRect();
                GUI.Label(new UnityEngine.Rect(18f, 90f, 900f, 28f), $"MediaPipe input flip H/V:{transform.flipHorizontally}/{transform.flipVertically}  preview UV:{uvRect.x:0.00},{uvRect.y:0.00},{uvRect.width:0.00},{uvRect.height:0.00}");
            }

            if (lastFrameVariance >= 0f)
            {
                GUI.Label(new UnityEngine.Rect(18f, 114f, 520f, 28f), $"Camera frame mean/variance: {lastFrameMean:0.0} / {lastFrameVariance:0.0}");
                bool edgeMean = lastFrameMean <= emptyCameraMeanEdgeThreshold || lastFrameMean >= 255f - emptyCameraMeanEdgeThreshold;
                bool flatFrame = lastFrameVariance <= flatCameraVarianceThreshold || edgeMean;
                if (flatFrame)
                {
                    GUI.Label(new UnityEngine.Rect(18f, 186f, 760f, 28f), "Camera feed looks blank. Press C, close other camera apps, or check the camera privacy shutter.");
                }
            }
        }
    }
}
