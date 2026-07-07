using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem;
using UnityEngine;

namespace ARCloset
{
    public sealed class MediaPipePoseRigDriver : MonoBehaviour
    {
        private const int Nose = 0;
        private const int LeftShoulder = 11;
        private const int RightShoulder = 12;
        private const int LeftElbow = 13;
        private const int RightElbow = 14;
        private const int LeftWrist = 15;
        private const int RightWrist = 16;
        private const int LeftHip = 23;
        private const int RightHip = 24;
        private const int LeftKnee = 25;
        private const int RightKnee = 26;
        private const int LeftAnkle = 27;
        private const int RightAnkle = 28;

        [Header("MediaPipe")]
        [SerializeField] private MediaPipePoseReceiver receiver;
        [SerializeField] private GarmentFittingController fittingController;
        [SerializeField] private bool mirrorX = false;
        [SerializeField] private bool usePacketMirrorState = true;
        [SerializeField] private bool keepVisualLeftRightOrder = true;
        [SerializeField] private float minVisibility = 0.24f;

        [Header("Coordinate Mapping")]
        [SerializeField] private bool mapToVideoRenderer = true;
        [SerializeField] private Renderer trackingRenderer;
        [SerializeField] private bool mapToCameraViewport = true;
        [SerializeField] private Camera overlayCamera;
        [SerializeField] private float horizontalScale = 3.0f;
        [SerializeField] private float verticalScale = 3.15f;
        [SerializeField] private float depthScale = 0.12f;
        [SerializeField] private float verticalOffset = 0.0f;
        [SerializeField] private float smoothing = 0.75f;
        [SerializeField] private float overlayZ = 0.0f;

        [Header("Pose Filter")]
        [SerializeField, Range(0.02f, 0.7f)] private float landmarkSmoothTime = 0.045f;
        [SerializeField, Range(0.02f, 0.9f)] private float lowConfidenceLandmarkSmoothTime = 0.14f;
        [SerializeField, Range(0.2f, 24f)] private float maxLandmarkSpeed = 14f;
        [SerializeField, Range(0.05f, 2f)] private float landmarkOutlierDistance = 1.2f;
        [SerializeField, Range(1, 30)] private int maxMissingLandmarkFrames = 4;

        [Header("Anchor Filter")]
        [SerializeField] private bool lowLatencyGarmentAnchor = true;
        [SerializeField, Range(0.02f, 0.8f)] private float anchorSmoothTime = 0.20f;
        [SerializeField, Range(0.1f, 10f)] private float maxAnchorSpeed = 2.4f;
        [SerializeField, Range(15f, 360f)] private float maxAnchorAngularSpeed = 100f;
        [SerializeField, Range(0.02f, 0.8f)] private float scaleSmoothTime = 0.26f;
        [SerializeField, Range(0.1f, 10f)] private float maxScaleSpeed = 1.8f;
        [SerializeField, Range(0.1f, 5f)] private float maxAnchorJumpDistance = 0.9f;

        [Header("Rig")]
        [SerializeField] private bool showDebugRig = true;
        [SerializeField] private KeyCode toggleDebugRigKey = KeyCode.D;
        [SerializeField] private KeyCode mirrorOverlayKey = KeyCode.X;
        [SerializeField] private float debugRigZOffset = -0.35f;
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform garmentAnchor;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform hips;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftForearm;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightForearm;
        [SerializeField] private Transform leftThigh;
        [SerializeField] private Transform leftCalf;
        [SerializeField] private Transform rightThigh;
        [SerializeField] private Transform rightCalf;

        [Header("Fit")]
        [SerializeField] private bool hideGarmentWhenPoseLost = true;
        [SerializeField] private bool deformGarmentWithPose = false;
        [SerializeField] private bool rotateGarmentWithShoulders = false;
        [SerializeField] private bool allowSmoothedFitFallback = true;
        [SerializeField] private bool fitGarmentByRendererBounds = true;
        [SerializeField] private bool clampGarmentTargetToCamera = true;
        [SerializeField] private float restShoulderWidth = 0.92f;
        [SerializeField] private float minGarmentScale = 0.12f;
        [SerializeField] private float maxGarmentScale = 8.0f;
        [SerializeField] private float minFitVisibility = 0.24f;
        [SerializeField] private float normalizedOverscan = 0.08f;
        [SerializeField, Range(0.005f, 0.2f)] private float minNormalizedShoulderWidth = 0.045f;
        [SerializeField, Range(0.005f, 0.2f)] private float minNormalizedHipWidth = 0.025f;
        [SerializeField, Range(0.02f, 0.7f)] private float minNormalizedTorsoHeight = 0.12f;
        [SerializeField, Range(0.02f, 0.7f)] private float maxNormalizedShoulderHipOffset = 0.32f;
        [SerializeField, Range(0.005f, 0.2f)] private float minMappedShoulderWidth = 0.045f;
        [SerializeField, Range(0f, 1f)] private float torsoBoundsWidthWeight = 0.35f;
        [SerializeField, Range(0.05f, 1f)] private float fitHoldSeconds = 0.22f;
        [SerializeField] private float fitScaleMultiplier = 1.0f;
        [SerializeField] private Vector2 fitOffset = Vector2.zero;
        [SerializeField] private float keyboardOffsetStep = 0.04f;
        [SerializeField] private float keyboardScaleStep = 0.04f;
        [SerializeField] private float upperWidthPadding = 1.18f;
        [SerializeField] private float lowerWidthPadding = 1.22f;
        [SerializeField] private float onePieceWidthPadding = 1.18f;
        [SerializeField] private float outerwearWidthPadding = 1.28f;

        [Header("Fit Debug")]
        [SerializeField] private bool showFitDebugOverlay = false;
        [SerializeField] private KeyCode toggleFitDebugKey = KeyCode.F;
        [SerializeField] private float fitDebugZOffset = -0.28f;

        [Header("Dynamic Sleeve Rig")]
        [SerializeField] private bool showDynamicSleeves = false;
        [SerializeField] private Transform leftUpperSleeve;
        [SerializeField] private Transform leftForearmSleeve;
        [SerializeField] private Transform rightUpperSleeve;
        [SerializeField] private Transform rightForearmSleeve;
        [SerializeField] private float upperSleeveRadius = 0.135f;
        [SerializeField] private float forearmSleeveRadius = 0.115f;
        [SerializeField] private float shortSleeveCoverage = 0.58f;
        [SerializeField] private float sleeveZOffset = -0.16f;

        private readonly Vector3[] smoothedPoints = new Vector3[33];
        private readonly Vector3[] pointVelocities = new Vector3[33];
        private readonly int[] missedPointFrames = new int[33];
        private readonly bool[] hasPoint = new bool[33];
        private int lastSequence = -1;
        private float lastPoseFilterTime = -1f;
        private float mappedWidth;
        private float mappedHeight;
        private int displayRotationDegrees;
        private int mappedFrameWidth;
        private int mappedFrameHeight;
        private Rect mappedRect = new Rect(-1.5f, -1.5f, 3.0f, 3.0f);
        private Vector3 lastFitTargetCenter;
        private float lastFitTargetWidth;
        private float lastFitTargetHeight;
        private bool hasLastFitTarget;
        private Vector3 garmentAnchorVelocity;
        private float garmentScaleVelocity;
        private bool hasGarmentAnchorFilter;
        private BodyFit lastReliableBodyFit;
        private bool hasLastReliableBodyFit;
        private float lastReliableBodyFitTime = -1f;
        private LineRenderer bodyFitDebugLine;
        private LineRenderer garmentFitDebugLine;
        private LineRenderer fitAxisDebugLine;

        public bool HasFitTarget => hasLastFitTarget;
        public Vector3 LastFitTargetCenter => lastFitTargetCenter;
        public float LastFitTargetWidth => lastFitTargetWidth;
        public float LastFitTargetHeight => lastFitTargetHeight;
        public bool HasGarmentAnchorFilter => hasGarmentAnchorFilter;
        public Transform GarmentAnchorTransform => garmentAnchor;

        private void Reset()
        {
            receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            fittingController = GetComponent<GarmentFittingController>();
            rigRoot = transform;
            overlayCamera = Camera.main;
            trackingRenderer = GameObject.Find("LiveVideoBackground")?.GetComponent<Renderer>();
        }

        private void Update()
        {
            HandleDebugInput();
            TickPose();
        }

        private void LateUpdate()
        {
            if (receiver != null && !receiver.HasFreshPose)
            {
                SetGarmentVisibility(!hideGarmentWhenPoseLost);
                ResetRuntimeGarmentRig();
                ResetGarmentAnchorFilter();
                ClearFitTarget();
                SetDynamicSleevesVisible(false);
                SetDebugRigVisible(false);
            }
        }

        private void HandleDebugInput()
        {
            if (Input.GetKeyDown(toggleDebugRigKey))
            {
                showDebugRig = !showDebugRig;
                ApplyDebugRigVisibility();
            }

            if (Input.GetKeyDown(mirrorOverlayKey))
            {
                mirrorX = !mirrorX;
                ClearSmoothedPoints();
            }

            if (Input.GetKeyDown(toggleFitDebugKey))
            {
                showFitDebugOverlay = !showFitDebugOverlay;
                if (!showFitDebugOverlay)
                {
                    HideFitDebugOverlay();
                }
            }

            float offsetStep = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                ? keyboardOffsetStep * 3f
                : keyboardOffsetStep;

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                fitOffset.x -= offsetStep;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                fitOffset.x += offsetStep;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                fitOffset.y += offsetStep;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                fitOffset.y -= offsetStep;
            }
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                fitScaleMultiplier += keyboardScaleStep;
            }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                fitScaleMultiplier = Mathf.Max(0.1f, fitScaleMultiplier - keyboardScaleStep);
            }
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                fitOffset = Vector2.zero;
                fitScaleMultiplier = 1.0f;
            }
        }

        public bool TickPose()
        {
            if (receiver == null || !receiver.TryGetLatestPacket(out MediaPipePosePacket packet))
            {
                SetGarmentVisibility(!hideGarmentWhenPoseLost);
                ResetRuntimeGarmentRig();
                ResetGarmentAnchorFilter();
                ClearFitTarget();
                SetDynamicSleevesVisible(false);
                SetDebugRigVisible(false);
                ClearSmoothedPoints();
                return false;
            }

            if (packet.landmarks == null || packet.landmarks.Length < 33)
            {
                SetGarmentVisibility(!hideGarmentWhenPoseLost);
                ResetRuntimeGarmentRig();
                ResetGarmentAnchorFilter();
                ClearFitTarget();
                SetDynamicSleevesVisible(false);
                SetDebugRigVisible(false);
                ClearSmoothedPoints();
                return false;
            }

            bool newPacket = receiver.LatestSequence != lastSequence;
            lastSequence = receiver.LatestSequence;

            UpdatePosePoints(packet, newPacket);
            ApplyRig();
            ApplyGarmentAnchor(packet);
            return true;
        }

        private void UpdatePosePoints(MediaPipePosePacket packet, bool newPacket)
        {
            UpdateViewportMapping(packet);
            if (!newPacket)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            float dt = lastPoseFilterTime > 0f ? now - lastPoseFilterTime : Time.deltaTime;
            lastPoseFilterTime = now;
            dt = Mathf.Clamp(dt, 1f / 120f, 0.12f);

            bool swapLeftRight = keepVisualLeftRightOrder && ShouldSwapLeftRight(packet);

            for (int i = 0; i < smoothedPoints.Length; i++)
            {
                MediaPipePoseLandmark landmark = packet.landmarks[i];
                float confidence = Mathf.Max(landmark.visibility, landmark.presence);
                int targetIndex = swapLeftRight ? SwappedPoseIndex(i) : i;
                bool visible = confidence >= minVisibility;

                if (!visible)
                {
                    MarkLandmarkMissing(targetIndex);
                    continue;
                }

                Vector3 target = ToRigPoint(landmark, packet);
                UpdateFilteredLandmark(targetIndex, target, confidence, dt);
            }
        }

        private void UpdateFilteredLandmark(int index, Vector3 target, float confidence, float dt)
        {
            if (index < 0 || index >= smoothedPoints.Length)
            {
                return;
            }

            missedPointFrames[index] = 0;
            if (!hasPoint[index])
            {
                smoothedPoints[index] = target;
                pointVelocities[index] = Vector3.zero;
                hasPoint[index] = true;
                return;
            }

            Vector3 current = smoothedPoints[index];
            Vector3 delta = target - current;
            float maxJump = Mathf.Max(0.001f, landmarkOutlierDistance);
            if (delta.magnitude > maxJump)
            {
                target = current + delta.normalized * maxJump;
                delta = target - current;
            }

            float maxStep = Mathf.Max(0.001f, maxLandmarkSpeed) * Mathf.Max(dt, 1f / 120f);
            if (delta.magnitude > maxStep)
            {
                target = current + delta.normalized * maxStep;
            }

            float confidence01 = Mathf.InverseLerp(minVisibility, 1f, confidence);
            float confidenceAlpha = Mathf.Lerp(Mathf.Clamp01(smoothing) * 0.45f, Mathf.Clamp01(smoothing), confidence01);
            float smoothTime = Mathf.Lerp(lowConfidenceLandmarkSmoothTime, landmarkSmoothTime, confidence01);
            float timeAlpha = 1f - Mathf.Exp(-Mathf.Max(dt, 1f / 120f) / Mathf.Max(0.001f, smoothTime));
            float alpha = Mathf.Clamp01(Mathf.Max(confidenceAlpha, timeAlpha));
            smoothedPoints[index] = Vector3.Lerp(current, target, alpha);
            pointVelocities[index] = Vector3.zero;
        }

        private void MarkLandmarkMissing(int index)
        {
            if (index < 0 || index >= smoothedPoints.Length || !hasPoint[index])
            {
                return;
            }

            missedPointFrames[index]++;
            if (missedPointFrames[index] <= maxMissingLandmarkFrames)
            {
                pointVelocities[index] *= 0.65f;
                return;
            }

            hasPoint[index] = false;
            pointVelocities[index] = Vector3.zero;
            missedPointFrames[index] = 0;
        }
        private Vector3 ToRigPoint(MediaPipePoseLandmark landmark, MediaPipePosePacket packet)
        {
            Vector3 point = ImageCoordinate.ImageNormalizedToPoint(
                mappedRect,
                landmark.x,
                landmark.y,
                0f,
                RotationAngle.Rotation0,
                EffectiveMirrorX(packet));
            point.y += verticalOffset;
            float z = overlayZ - landmark.z * depthScale;
            return new Vector3(point.x, point.y, z);
        }

        private bool EffectiveMirrorX(MediaPipePosePacket packet)
        {
            if (usePacketMirrorState && packet != null)
            {
                return mirrorX ^ (packet.inputMirrored != packet.previewMirrored);
            }

            return mirrorX;
        }

        private bool ShouldSwapLeftRight(MediaPipePosePacket packet)
        {
            if (packet?.landmarks == null || packet.landmarks.Length <= RightShoulder)
            {
                return false;
            }

            MediaPipePoseLandmark left = packet.landmarks[LeftShoulder];
            MediaPipePoseLandmark right = packet.landmarks[RightShoulder];
            if (Mathf.Max(left.visibility, left.presence) < minVisibility ||
                Mathf.Max(right.visibility, right.presence) < minVisibility)
            {
                return false;
            }

            return ToRigPoint(left, packet).x > ToRigPoint(right, packet).x;
        }

        private static int SwappedPoseIndex(int index)
        {
            return index switch
            {
                LeftShoulder => RightShoulder,
                RightShoulder => LeftShoulder,
                LeftElbow => RightElbow,
                RightElbow => LeftElbow,
                LeftWrist => RightWrist,
                RightWrist => LeftWrist,
                LeftHip => RightHip,
                RightHip => LeftHip,
                LeftKnee => RightKnee,
                RightKnee => LeftKnee,
                LeftAnkle => RightAnkle,
                RightAnkle => LeftAnkle,
                _ => index,
            };
        }
        private void UpdateViewportMapping(MediaPipePosePacket packet)
        {
            mappedWidth = horizontalScale;
            mappedHeight = verticalScale;
            mappedRect = CenteredRect(mappedWidth, mappedHeight);
            displayRotationDegrees = NormalizeDisplayRotation(packet.displayRotationDegrees);
            mappedFrameWidth = packet.frameWidth;
            mappedFrameHeight = packet.frameHeight;

            if (mapToVideoRenderer && TryUpdateMappingFromVideoRenderer())
            {
                return;
            }

            if (!mapToCameraViewport)
            {
                return;
            }

            if (overlayCamera == null)
            {
                overlayCamera = Camera.main;
            }

            if (overlayCamera == null || !overlayCamera.orthographic)
            {
                return;
            }

            float imageAspect = packet.frameWidth > 0 && packet.frameHeight > 0
                ? (float)packet.frameWidth / packet.frameHeight
                : 4f / 3f;
            float viewHeight = overlayCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * overlayCamera.aspect;

            mappedWidth = viewWidth;
            mappedHeight = viewWidth / imageAspect;

            if (mappedHeight < viewHeight)
            {
                mappedHeight = viewHeight;
                mappedWidth = viewHeight * imageAspect;
            }

            mappedRect = CenteredRect(mappedWidth, mappedHeight);
        }

        private bool TryUpdateMappingFromVideoRenderer()
        {
            if (trackingRenderer == null)
            {
                GameObject background = GameObject.Find("LiveVideoBackground");
                if (background != null)
                {
                    trackingRenderer = background.GetComponent<Renderer>();
                }
            }

            if (trackingRenderer == null)
            {
                return false;
            }

            Bounds bounds = trackingRenderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            Transform mappingRoot = transform;

            foreach (Vector3 corner in corners)
            {
                Vector3 local = mappingRoot.InverseTransformPoint(corner);
                minX = Mathf.Min(minX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxX = Mathf.Max(maxX, local.x);
                maxY = Mathf.Max(maxY, local.y);
            }

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0.001f || height <= 0.001f)
            {
                return false;
            }

            mappedRect = new Rect(minX, minY, width, height);
            mappedWidth = width;
            mappedHeight = height;
            return true;
        }

        private static Rect CenteredRect(float width, float height)
        {
            return new Rect(width * -0.5f, height * -0.5f, width, height);
        }

        private static int NormalizeDisplayRotation(int degrees)
        {
            int normalized = degrees % 360;
            if (normalized < 0)
            {
                normalized += 360;
            }

            int nearestRightAngle = Mathf.RoundToInt(normalized / 90f) * 90;
            return nearestRightAngle % 360;
        }

        private void ApplyRig()
        {
            SetSegment(torso, ShoulderCenter(), HipCenter(), 0.34f);
            SetSphere(hips, HipCenter(), 0.34f);
            SetSphere(head, HeadPoint(), 0.22f);
            SetSegment(leftUpperArm, LeftShoulder, LeftElbow, 0.09f);
            SetSegment(leftForearm, LeftElbow, LeftWrist, 0.075f);
            SetSegment(rightUpperArm, RightShoulder, RightElbow, 0.09f);
            SetSegment(rightForearm, RightElbow, RightWrist, 0.075f);
            SetSegment(leftThigh, LeftHip, LeftKnee, 0.105f);
            SetSegment(leftCalf, LeftKnee, LeftAnkle, 0.09f);
            SetSegment(rightThigh, RightHip, RightKnee, 0.105f);
            SetSegment(rightCalf, RightKnee, RightAnkle, 0.09f);
        }

        private void ApplyGarmentAnchor(MediaPipePosePacket packet)
        {
            if (garmentAnchor == null)
            {
                return;
            }

            GarmentSlot slot = fittingController != null ? fittingController.CurrentSlot : GarmentSlot.Upper;
            bool hasCurrentBodyFit = TryBuildBodyFit(slot, out BodyFit bodyFit);
            bool hasReliableFit = hasCurrentBodyFit &&
                                  (HasReliableFitInput(packet, slot) ||
                                   (allowSmoothedFitFallback && HasSmoothedFitInput(slot)));
            if (hasReliableFit)
            {
                lastReliableBodyFit = bodyFit;
                hasLastReliableBodyFit = true;
                lastReliableBodyFitTime = Time.realtimeSinceStartup;
            }
            else
            {
                hasReliableFit = TryGetHeldBodyFit(out bodyFit);
            }

            if (!hasReliableFit)
            {
                SetGarmentVisibility(!hideGarmentWhenPoseLost);
                ResetRuntimeGarmentRig();
                ResetGarmentAnchorFilter();
                ClearFitTarget();
                SetDynamicSleevesVisible(false);
                HideFitDebugOverlay();
                return;
            }

            SetDebugRigVisible(showDebugRig);
            SetGarmentVisibility(true);
            GarmentDefinition definition = fittingController != null ? fittingController.CurrentDefinition : null;
            float profileWidthMultiplier = definition != null ? Mathf.Max(0.01f, definition.fitWidthMultiplier) : 1f;
            float profileHeightMultiplier = definition != null ? Mathf.Max(0.01f, definition.fitHeightMultiplier) : 1f;
            float profileVerticalBias = definition != null ? definition.fitVerticalBias : 0f;
            Vector2 profileOffset = definition != null ? definition.fitAnchorOffset : Vector2.zero;
            GetGarmentTarget(slot, bodyFit, profileVerticalBias, out Vector3 targetCenter, out float targetWidth, out float targetHeight, out float heightBlend);
            Vector3 targetAnchor = GetGarmentAnchorTarget(slot, bodyFit, profileVerticalBias);
            Vector3 offset = new Vector3(profileOffset.x + fitOffset.x, profileOffset.y + fitOffset.y, 0f);
            targetCenter += offset;
            targetAnchor += offset;
            targetWidth *= fitScaleMultiplier * profileWidthMultiplier;
            targetHeight *= fitScaleMultiplier * profileHeightMultiplier;
            Quaternion targetRotation = rotateGarmentWithShoulders ? bodyFit.Rotation : Quaternion.identity;
            float scale = Mathf.Clamp(targetWidth / Mathf.Max(0.01f, restShoulderWidth), minGarmentScale, maxGarmentScale);
            float clampWidth = targetWidth;
            float clampHeight = targetHeight;
            Vector3 targetPosition = targetCenter;

            if (fitGarmentByRendererBounds &&
                fittingController != null &&
                fittingController.TryGetCurrentGarmentFitFrame(slot, out GarmentFittingController.GarmentFitFrame fitFrame) &&
                fitFrame.FitWidth > 0.001f &&
                fitFrame.FitHeight > 0.001f)
            {
                float widthScale = targetWidth / fitFrame.FitWidth;
                float heightScale = targetHeight / fitFrame.FitHeight;
                scale = Mathf.Clamp(Mathf.Lerp(widthScale, heightScale, Mathf.Clamp01(heightBlend)), minGarmentScale, maxGarmentScale);
                targetPosition = targetAnchor - targetRotation * (fitFrame.AnchorLocal * scale);
                targetCenter = targetPosition + targetRotation * (fitFrame.Bounds.center * scale);
                clampWidth = fitFrame.Bounds.size.x * scale;
                clampHeight = fitFrame.Bounds.size.y * scale;
            }

            if (clampGarmentTargetToCamera)
            {
                Vector3 clampedCenter = ClampTargetCenterToCamera(targetCenter, clampWidth, clampHeight);
                targetPosition += clampedCenter - targetCenter;
                targetCenter = clampedCenter;
            }

            lastFitTargetCenter = targetCenter;
            lastFitTargetWidth = targetWidth;
            lastFitTargetHeight = targetHeight;
            hasLastFitTarget = true;

            UpdateFitDebugOverlay(bodyFit, targetCenter, clampWidth, clampHeight);
            ApplyGarmentAnchorFilter(targetPosition, targetRotation, scale);
            if (deformGarmentWithPose)
            {
                ApplyRuntimeGarmentRig();
                ApplyDynamicSleeves(slot);
            }
            else
            {
                ResetRuntimeGarmentRig();
                SetDynamicSleevesVisible(false);
            }
        }

        private void ApplyGarmentAnchorFilter(Vector3 targetPosition, Quaternion targetRotation, float targetScale)
        {
            if (garmentAnchor == null)
            {
                return;
            }

            float dt = Mathf.Clamp(Time.deltaTime, 1f / 120f, 0.12f);
            if (!hasGarmentAnchorFilter)
            {
                garmentAnchor.localPosition = targetPosition;
                garmentAnchor.localRotation = targetRotation;
                garmentAnchor.localScale = Vector3.one * targetScale;
                garmentAnchorVelocity = Vector3.zero;
                garmentScaleVelocity = 0f;
                hasGarmentAnchorFilter = true;
                return;
            }

            Vector3 currentPosition = garmentAnchor.localPosition;
            Vector3 delta = targetPosition - currentPosition;
            if (delta.magnitude > maxAnchorJumpDistance)
            {
                targetPosition = currentPosition + delta.normalized * maxAnchorJumpDistance;
            }

            if (lowLatencyGarmentAnchor)
            {
                float alpha = Mathf.Clamp01(smoothing);
                garmentAnchor.localPosition = Vector3.Lerp(currentPosition, targetPosition, alpha);
                garmentAnchor.localRotation = Quaternion.Slerp(garmentAnchor.localRotation, targetRotation, alpha);
                garmentAnchor.localScale = Vector3.Lerp(garmentAnchor.localScale, Vector3.one * targetScale, alpha);
                garmentAnchorVelocity = Vector3.zero;
                garmentScaleVelocity = 0f;
                return;
            }

            garmentAnchor.localPosition = Vector3.SmoothDamp(
                currentPosition,
                targetPosition,
                ref garmentAnchorVelocity,
                anchorSmoothTime,
                maxAnchorSpeed,
                dt);
            garmentAnchor.localRotation = Quaternion.RotateTowards(
                garmentAnchor.localRotation,
                targetRotation,
                maxAnchorAngularSpeed * dt);

            float currentScale = Mathf.Max(0.0001f, garmentAnchor.localScale.x);
            float nextScale = Mathf.SmoothDamp(
                currentScale,
                targetScale,
                ref garmentScaleVelocity,
                scaleSmoothTime,
                maxScaleSpeed,
                dt);
            garmentAnchor.localScale = Vector3.one * Mathf.Max(0.0001f, nextScale);
        }

        private void ResetGarmentAnchorFilter()
        {
            hasGarmentAnchorFilter = false;
            garmentAnchorVelocity = Vector3.zero;
            garmentScaleVelocity = 0f;
        }

        private void ClearFitTarget()
        {
            hasLastFitTarget = false;
            lastFitTargetCenter = Vector3.zero;
            lastFitTargetWidth = 0f;
            lastFitTargetHeight = 0f;
        }

        private void UpdateFitDebugOverlay(BodyFit bodyFit, Vector3 targetCenter, float targetWidth, float targetHeight)
        {
            if (!showFitDebugOverlay)
            {
                HideFitDebugOverlay();
                return;
            }

            float z = overlayZ + fitDebugZOffset;
            Rect bodyBounds = bodyFit.VisibleBodyBounds;
            if (bodyBounds.width > 0.001f && bodyBounds.height > 0.001f)
            {
                Vector2 bodyCenter = bodyBounds.center;
                SetRectLine(
                    EnsureFitLineRenderer(ref bodyFitDebugLine, "BodyFitDebugBounds", new Color(0f, 0.95f, 1f, 0.9f), 0.018f),
                    new Vector3(bodyCenter.x, bodyCenter.y, z),
                    bodyBounds.width,
                    bodyBounds.height);
            }

            SetRectLine(
                EnsureFitLineRenderer(ref garmentFitDebugLine, "GarmentFitDebugTarget", new Color(1f, 0.85f, 0.05f, 0.95f), 0.022f),
                new Vector3(targetCenter.x, targetCenter.y, z),
                targetWidth,
                targetHeight);

            LineRenderer axisLine = EnsureFitLineRenderer(ref fitAxisDebugLine, "FitDebugTorsoAxis", new Color(1f, 0.2f, 0.85f, 0.95f), 0.018f);
            axisLine.positionCount = 2;
            axisLine.SetPosition(0, new Vector3(bodyFit.ShoulderCenter.x, bodyFit.ShoulderCenter.y, z));
            axisLine.SetPosition(1, new Vector3(bodyFit.HipCenter.x, bodyFit.HipCenter.y, z));
            axisLine.enabled = true;
        }

        private void HideFitDebugOverlay()
        {
            SetFitLineVisible(bodyFitDebugLine, false);
            SetFitLineVisible(garmentFitDebugLine, false);
            SetFitLineVisible(fitAxisDebugLine, false);
        }

        private LineRenderer EnsureFitLineRenderer(ref LineRenderer line, string objectName, Color color, float width)
        {
            if (line == null)
            {
                GameObject lineObject = new GameObject(objectName);
                lineObject.hideFlags = HideFlags.DontSave;
                lineObject.transform.SetParent(transform, false);
                line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = false;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                line.sortingOrder = 100;
            }

            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
            if (line.material != null)
            {
                line.material.color = color;
            }

            line.enabled = true;
            return line;
        }

        private static void SetRectLine(LineRenderer line, Vector3 center, float width, float height)
        {
            if (line == null)
            {
                return;
            }

            float halfWidth = Mathf.Max(0.001f, width) * 0.5f;
            float halfHeight = Mathf.Max(0.001f, height) * 0.5f;
            line.positionCount = 5;
            line.SetPosition(0, center + new Vector3(-halfWidth, -halfHeight, 0f));
            line.SetPosition(1, center + new Vector3(-halfWidth, halfHeight, 0f));
            line.SetPosition(2, center + new Vector3(halfWidth, halfHeight, 0f));
            line.SetPosition(3, center + new Vector3(halfWidth, -halfHeight, 0f));
            line.SetPosition(4, center + new Vector3(-halfWidth, -halfHeight, 0f));
            line.enabled = true;
        }

        private static void SetFitLineVisible(LineRenderer line, bool visible)
        {
            if (line != null)
            {
                line.enabled = visible;
            }
        }

        private bool HasReliableFitInput(MediaPipePosePacket packet, GarmentSlot slot)
        {
            if (packet?.landmarks == null || packet.landmarks.Length < 33)
            {
                return false;
            }

            if (!IsReliableLandmark(packet, LeftShoulder) || !IsReliableLandmark(packet, RightShoulder))
            {
                return false;
            }

            float shoulderWidthNormalized = Mathf.Abs(packet.landmarks[RightShoulder].x - packet.landmarks[LeftShoulder].x);
            if (shoulderWidthNormalized < minNormalizedShoulderWidth)
            {
                return false;
            }

            switch (slot)
            {
                case GarmentSlot.Lower:
                    return IsReliableLandmark(packet, LeftHip) &&
                           IsReliableLandmark(packet, RightHip) &&
                           HasReliableTorsoGeometry(packet, true) &&
                           (IsReliableLandmark(packet, LeftKnee) || IsReliableLandmark(packet, RightKnee));
                case GarmentSlot.OnePiece:
                    return IsReliableLandmark(packet, LeftHip) &&
                           IsReliableLandmark(packet, RightHip) &&
                           HasReliableTorsoGeometry(packet, true);
                case GarmentSlot.Outerwear:
                case GarmentSlot.Upper:
                default:
                    return HasReliableTorsoGeometry(packet, false);
            }
        }

        private bool HasSmoothedFitInput(GarmentSlot slot)
        {
            if (!HasPoint(LeftShoulder) || !HasPoint(RightShoulder))
            {
                return false;
            }

            float shoulderWidth = Vector3.Distance(smoothedPoints[LeftShoulder], smoothedPoints[RightShoulder]);
            float widthReference = Mathf.Max(0.001f, mappedWidth);
            if (shoulderWidth < widthReference * minMappedShoulderWidth)
            {
                return false;
            }

            switch (slot)
            {
                case GarmentSlot.Lower:
                    return HasPoint(LeftHip) && HasPoint(RightHip) && HasSmoothedTorsoGeometry(true);
                case GarmentSlot.OnePiece:
                    return HasPoint(LeftHip) && HasPoint(RightHip) && HasSmoothedTorsoGeometry(true);
                case GarmentSlot.Outerwear:
                case GarmentSlot.Upper:
                default:
                    return HasSmoothedTorsoGeometry(false);
            }
        }

        private bool HasReliableTorsoGeometry(MediaPipePosePacket packet, bool requireHips)
        {
            bool hasHips = IsReliableLandmark(packet, LeftHip) && IsReliableLandmark(packet, RightHip);
            if (!hasHips)
            {
                return !requireHips;
            }

            MediaPipePoseLandmark leftShoulder = packet.landmarks[LeftShoulder];
            MediaPipePoseLandmark rightShoulder = packet.landmarks[RightShoulder];
            MediaPipePoseLandmark leftHip = packet.landmarks[LeftHip];
            MediaPipePoseLandmark rightHip = packet.landmarks[RightHip];

            float hipWidth = Mathf.Abs(rightHip.x - leftHip.x);
            if (hipWidth < minNormalizedHipWidth)
            {
                return false;
            }

            float shoulderCenterX = (leftShoulder.x + rightShoulder.x) * 0.5f;
            float shoulderCenterY = (leftShoulder.y + rightShoulder.y) * 0.5f;
            float hipCenterX = (leftHip.x + rightHip.x) * 0.5f;
            float hipCenterY = (leftHip.y + rightHip.y) * 0.5f;

            if (Mathf.Abs(hipCenterY - shoulderCenterY) < minNormalizedTorsoHeight)
            {
                return false;
            }

            return Mathf.Abs(hipCenterX - shoulderCenterX) <= maxNormalizedShoulderHipOffset;
        }

        private bool HasSmoothedTorsoGeometry(bool requireHips)
        {
            bool hasHips = HasPoint(LeftHip) && HasPoint(RightHip);
            if (!hasHips)
            {
                return !requireHips;
            }

            float widthReference = Mathf.Max(0.001f, mappedWidth);
            float heightReference = Mathf.Max(0.001f, mappedHeight);
            float hipWidth = Vector3.Distance(smoothedPoints[LeftHip], smoothedPoints[RightHip]);
            if (hipWidth < widthReference * minNormalizedHipWidth)
            {
                return false;
            }

            Vector3 shoulderCenter = (smoothedPoints[LeftShoulder] + smoothedPoints[RightShoulder]) * 0.5f;
            Vector3 hipCenter = (smoothedPoints[LeftHip] + smoothedPoints[RightHip]) * 0.5f;
            if (Mathf.Abs(hipCenter.y - shoulderCenter.y) < heightReference * minNormalizedTorsoHeight)
            {
                return false;
            }

            return Mathf.Abs(hipCenter.x - shoulderCenter.x) <= widthReference * maxNormalizedShoulderHipOffset;
        }

        private bool IsReliableLandmark(MediaPipePosePacket packet, int index)
        {
            if (packet.landmarks == null || index < 0 || index >= packet.landmarks.Length)
            {
                return false;
            }

            MediaPipePoseLandmark landmark = packet.landmarks[index];
            float confidence = Mathf.Max(landmark.visibility, landmark.presence);
            if (confidence < minFitVisibility)
            {
                return false;
            }

            return landmark.x >= -normalizedOverscan &&
                   landmark.x <= 1f + normalizedOverscan &&
                   landmark.y >= -normalizedOverscan &&
                   landmark.y <= 1f + normalizedOverscan;
        }

        private Vector3 ClampTargetCenterToCamera(Vector3 targetCenter, float targetWidth, float targetHeight)
        {
            float halfWidth = Mathf.Max(0.01f, mappedWidth * 0.5f - targetWidth * 0.25f);
            float halfHeight = Mathf.Max(0.01f, mappedHeight * 0.5f - targetHeight * 0.25f);

            targetCenter.x = Mathf.Clamp(targetCenter.x, -halfWidth, halfWidth);
            targetCenter.y = Mathf.Clamp(targetCenter.y, -halfHeight, halfHeight);
            return targetCenter;
        }

        private void SetGarmentVisibility(bool visible)
        {
            if (fittingController != null)
            {
                fittingController.SetCurrentGarmentVisible(visible);
            }
        }

        private void ResetRuntimeGarmentRig()
        {
            if (fittingController != null)
            {
                fittingController.ResetRuntimeRig();
            }
        }

        private void ApplyRuntimeGarmentRig()
        {
            if (fittingController == null)
            {
                return;
            }

            bool hasLeftShoulder = TryGetWorldPoint(LeftShoulder, out Vector3 leftShoulder);
            bool hasRightShoulder = TryGetWorldPoint(RightShoulder, out Vector3 rightShoulder);
            bool hasLeftElbow = TryGetWorldPoint(LeftElbow, out Vector3 leftElbow);
            bool hasRightElbow = TryGetWorldPoint(RightElbow, out Vector3 rightElbow);
            bool hasLeftWrist = TryGetWorldPoint(LeftWrist, out Vector3 leftWrist);
            bool hasRightWrist = TryGetWorldPoint(RightWrist, out Vector3 rightWrist);
            bool hasLeftHip = TryGetWorldPoint(LeftHip, out Vector3 leftHip);
            bool hasRightHip = TryGetWorldPoint(RightHip, out Vector3 rightHip);
            bool hasLeftKnee = TryGetWorldPoint(LeftKnee, out Vector3 leftKnee);
            bool hasRightKnee = TryGetWorldPoint(RightKnee, out Vector3 rightKnee);
            bool hasLeftAnkle = TryGetWorldPoint(LeftAnkle, out Vector3 leftAnkle);
            bool hasRightAnkle = TryGetWorldPoint(RightAnkle, out Vector3 rightAnkle);

            GarmentRuntimeRig.Pose pose = new GarmentRuntimeRig.Pose
            {
                HasLeftShoulder = hasLeftShoulder,
                HasRightShoulder = hasRightShoulder,
                HasLeftElbow = hasLeftElbow,
                HasRightElbow = hasRightElbow,
                HasLeftWrist = hasLeftWrist,
                HasRightWrist = hasRightWrist,
                HasLeftHip = hasLeftHip,
                HasRightHip = hasRightHip,
                HasLeftKnee = hasLeftKnee,
                HasRightKnee = hasRightKnee,
                HasLeftAnkle = hasLeftAnkle,
                HasRightAnkle = hasRightAnkle,
                LeftShoulderWorld = leftShoulder,
                RightShoulderWorld = rightShoulder,
                LeftElbowWorld = leftElbow,
                RightElbowWorld = rightElbow,
                LeftWristWorld = leftWrist,
                RightWristWorld = rightWrist,
                LeftHipWorld = leftHip,
                RightHipWorld = rightHip,
                LeftKneeWorld = leftKnee,
                RightKneeWorld = rightKnee,
                LeftAnkleWorld = leftAnkle,
                RightAnkleWorld = rightAnkle,
            };

            fittingController.ApplyRuntimeRig(pose);
        }

        private bool TryGetWorldPoint(int index, out Vector3 point)
        {
            if (!HasPoint(index))
            {
                point = default;
                return false;
            }

            point = transform.TransformPoint(smoothedPoints[index]);
            return true;
        }

        private void ApplyDynamicSleeves(GarmentSlot slot)
        {
            if (!showDynamicSleeves || slot == GarmentSlot.Lower)
            {
                SetDynamicSleevesVisible(false);
                return;
            }

            ApplyDynamicSleeveMaterial();

            bool longSleeves = UsesLongSleeves(slot);
            ApplySleeveSegment(leftUpperSleeve, LeftShoulder, LeftElbow, upperSleeveRadius, longSleeves ? 1f : shortSleeveCoverage);
            ApplySleeveSegment(rightUpperSleeve, RightShoulder, RightElbow, upperSleeveRadius, longSleeves ? 1f : shortSleeveCoverage);

            if (longSleeves)
            {
                ApplySleeveSegment(leftForearmSleeve, LeftElbow, LeftWrist, forearmSleeveRadius, 0.92f);
                ApplySleeveSegment(rightForearmSleeve, RightElbow, RightWrist, forearmSleeveRadius, 0.92f);
            }
            else
            {
                SetSegmentVisible(leftForearmSleeve, false);
                SetSegmentVisible(rightForearmSleeve, false);
            }
        }

        private bool UsesLongSleeves(GarmentSlot slot)
        {
            if (slot == GarmentSlot.Outerwear)
            {
                return true;
            }

            string name = fittingController != null && fittingController.CurrentDefinition != null
                ? fittingController.CurrentDefinition.displayName
                : string.Empty;

            return name.IndexOf("sweater", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("kimono", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("jacket", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("coat", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("hoodie", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ApplySleeveSegment(Transform segment, int startIndex, int endIndex, float radius, float coverage)
        {
            if (segment == null || !HasPoint(startIndex) || !HasPoint(endIndex))
            {
                SetSegmentVisible(segment, false);
                return;
            }

            Vector3 start = smoothedPoints[startIndex];
            Vector3 end = Vector3.Lerp(start, smoothedPoints[endIndex], Mathf.Clamp01(coverage));
            start.z = overlayZ + sleeveZOffset;
            end.z = overlayZ + sleeveZOffset;

            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.02f)
            {
                SetSegmentVisible(segment, false);
                return;
            }

            segment.gameObject.SetActive(true);
            SetSegmentVisible(segment, true);
            segment.localPosition = (start + end) * 0.5f;
            segment.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            segment.localScale = new Vector3(radius, length * 0.5f, radius);
        }

        private void ApplyDynamicSleeveMaterial()
        {
            if (fittingController == null || !fittingController.TryGetCurrentGarmentMaterial(out Material material))
            {
                return;
            }

            ApplyMaterial(leftUpperSleeve, material);
            ApplyMaterial(leftForearmSleeve, material);
            ApplyMaterial(rightUpperSleeve, material);
            ApplyMaterial(rightForearmSleeve, material);
        }

        private static void ApplyMaterial(Transform target, Material material)
        {
            if (target == null || material == null)
            {
                return;
            }

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
        }

        private void SetDynamicSleevesVisible(bool visible)
        {
            SetSegmentVisible(leftUpperSleeve, visible);
            SetSegmentVisible(leftForearmSleeve, visible);
            SetSegmentVisible(rightUpperSleeve, visible);
            SetSegmentVisible(rightForearmSleeve, visible);
        }

        private static void SetSegmentVisible(Transform segment, bool visible)
        {
            if (segment == null)
            {
                return;
            }

            foreach (Renderer renderer in segment.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        private bool TryBuildBodyFit(GarmentSlot slot, out BodyFit bodyFit)
        {
            bodyFit = default;
            if (!HasPoint(LeftShoulder) || !HasPoint(RightShoulder))
            {
                return false;
            }

            Vector3 shoulderCenter = ShoulderCenter();
            Vector3 hipCenter = HasPoint(LeftHip) && HasPoint(RightHip)
                ? HipCenter()
                : shoulderCenter + Vector3.down * Mathf.Max(0.8f, mappedHeight * 0.18f);
            Vector3 torsoAxis = hipCenter - shoulderCenter;
            if (torsoAxis.sqrMagnitude <= 0.0001f)
            {
                torsoAxis = Vector3.down * Mathf.Max(0.8f, mappedHeight * 0.18f);
            }

            Vector3 kneeCenter = HasPoint(LeftKnee) && HasPoint(RightKnee)
                ? (smoothedPoints[LeftKnee] + smoothedPoints[RightKnee]) * 0.5f
                : hipCenter + torsoAxis;
            Vector3 ankleCenter = HasPoint(LeftAnkle) && HasPoint(RightAnkle)
                ? (smoothedPoints[LeftAnkle] + smoothedPoints[RightAnkle]) * 0.5f
                : hipCenter + torsoAxis * 1.85f;

            float shoulderWidth = Mathf.Max(0.001f, Vector3.Distance(smoothedPoints[LeftShoulder], smoothedPoints[RightShoulder]));
            float hipWidth = HasPoint(LeftHip) && HasPoint(RightHip)
                ? Mathf.Max(0.001f, Vector3.Distance(smoothedPoints[LeftHip], smoothedPoints[RightHip]))
                : shoulderWidth * 0.82f;
            float torsoHeight = Mathf.Max(0.001f, Vector3.Distance(shoulderCenter, hipCenter));
            float legHeight = Mathf.Max(0.001f, Vector3.Distance(hipCenter, ankleCenter));
            float dressHeight = Mathf.Max(torsoHeight * 1.55f, Vector3.Distance(shoulderCenter, kneeCenter));

            Rect bodyBounds = BuildVisibleBodyBounds(slot);
            float torsoBoundsWidth = bodyBounds.width > 0.001f
                ? Mathf.Clamp(bodyBounds.width, shoulderWidth * 0.78f, shoulderWidth * 1.28f)
                : shoulderWidth;
            float structuralWidth = Mathf.Max(shoulderWidth, hipWidth * 0.9f);
            float torsoWidth = Mathf.Lerp(structuralWidth, torsoBoundsWidth, Mathf.Clamp01(torsoBoundsWidthWeight));

            Vector3 shoulderVector = smoothedPoints[RightShoulder] - smoothedPoints[LeftShoulder];
            float tilt = Mathf.Atan2(shoulderVector.y, shoulderVector.x) * Mathf.Rad2Deg;

            bodyFit = new BodyFit
            {
                ShoulderCenter = shoulderCenter,
                HipCenter = hipCenter,
                KneeCenter = kneeCenter,
                AnkleCenter = ankleCenter,
                ShoulderWidth = shoulderWidth,
                HipWidth = hipWidth,
                TorsoWidth = Mathf.Max(0.001f, torsoWidth),
                TorsoHeight = torsoHeight,
                LegHeight = legHeight,
                DressHeight = dressHeight,
                VisibleBodyBounds = bodyBounds,
                Rotation = Quaternion.AngleAxis(Mathf.Clamp(tilt, -22f, 22f), Vector3.forward),
            };
            return true;
        }

        private Rect BuildVisibleBodyBounds(GarmentSlot slot)
        {
            bool hasBounds = false;
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;

            EncapsulateBodyPoint(LeftShoulder, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);
            EncapsulateBodyPoint(RightShoulder, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);
            EncapsulateBodyPoint(LeftHip, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);
            EncapsulateBodyPoint(RightHip, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);

            if (slot == GarmentSlot.Lower || slot == GarmentSlot.OnePiece)
            {
                EncapsulateBodyPoint(LeftKnee, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);
                EncapsulateBodyPoint(RightKnee, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);
            }

            if (slot == GarmentSlot.Lower)
            {
                EncapsulateBodyPoint(LeftAnkle, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);
                EncapsulateBodyPoint(RightAnkle, ref hasBounds, ref minX, ref maxX, ref minY, ref maxY);
            }

            if (!hasBounds)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private void EncapsulateBodyPoint(int index, ref bool hasBounds, ref float minX, ref float maxX, ref float minY, ref float maxY)
        {
            if (!HasPoint(index))
            {
                return;
            }

            Vector3 point = smoothedPoints[index];
            if (!hasBounds)
            {
                minX = maxX = point.x;
                minY = maxY = point.y;
                hasBounds = true;
                return;
            }

            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        private bool TryGetHeldBodyFit(out BodyFit bodyFit)
        {
            if (hasLastReliableBodyFit &&
                Time.realtimeSinceStartup - lastReliableBodyFitTime <= fitHoldSeconds)
            {
                bodyFit = lastReliableBodyFit;
                return true;
            }

            bodyFit = default;
            return false;
        }

        private void GetGarmentTarget(
            GarmentSlot slot,
            BodyFit bodyFit,
            float verticalBias,
            out Vector3 targetCenter,
            out float targetWidth,
            out float targetHeight,
            out float heightBlend)
        {
            switch (slot)
            {
                case GarmentSlot.Lower:
                    targetCenter = Vector3.Lerp(bodyFit.HipCenter, bodyFit.AnkleCenter, Mathf.Clamp01(0.48f + verticalBias));
                    targetWidth = Mathf.Max(bodyFit.HipWidth, bodyFit.ShoulderWidth * 0.58f) * lowerWidthPadding;
                    targetHeight = bodyFit.LegHeight * 1.04f;
                    heightBlend = 0.35f;
                    break;
                case GarmentSlot.OnePiece:
                    targetCenter = Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.KneeCenter, Mathf.Clamp01(0.52f + verticalBias));
                    targetWidth = Mathf.Max(bodyFit.TorsoWidth, bodyFit.HipWidth) * onePieceWidthPadding;
                    targetHeight = bodyFit.DressHeight * 1.05f;
                    heightBlend = 0.45f;
                    break;
                case GarmentSlot.Outerwear:
                    targetCenter = Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.HipCenter, Mathf.Clamp01(0.54f + verticalBias));
                    targetWidth = Mathf.Max(bodyFit.TorsoWidth, bodyFit.HipWidth * 0.92f) * outerwearWidthPadding;
                    targetHeight = bodyFit.TorsoHeight * 1.22f;
                    heightBlend = 0.2f;
                    break;
                case GarmentSlot.Upper:
                default:
                    targetCenter = Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.HipCenter, Mathf.Clamp01(0.50f + verticalBias));
                    targetWidth = Mathf.Max(bodyFit.TorsoWidth, bodyFit.HipWidth * 0.86f) * upperWidthPadding;
                    targetHeight = bodyFit.TorsoHeight * 1.16f;
                    heightBlend = 0.18f;
                    break;
            }

            targetCenter.z = overlayZ;
        }

        private Vector3 GetGarmentAnchorTarget(GarmentSlot slot, BodyFit bodyFit, float verticalBias)
        {
            Vector3 target = slot switch
            {
                GarmentSlot.Lower => Vector3.Lerp(bodyFit.HipCenter, bodyFit.KneeCenter, Mathf.Clamp01(0.04f + verticalBias * 0.35f)),
                GarmentSlot.OnePiece => Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.HipCenter, Mathf.Clamp01(0.07f + verticalBias * 0.35f)),
                GarmentSlot.Outerwear => Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.HipCenter, Mathf.Clamp01(0.06f + verticalBias * 0.35f)),
                _ => Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.HipCenter, Mathf.Clamp01(0.08f + verticalBias * 0.35f)),
            };
            target.z = overlayZ;
            return target;
        }
        private struct BodyFit
        {
            public Vector3 ShoulderCenter;
            public Vector3 HipCenter;
            public Vector3 KneeCenter;
            public Vector3 AnkleCenter;
            public float ShoulderWidth;
            public float HipWidth;
            public float TorsoWidth;
            public float TorsoHeight;
            public float LegHeight;
            public float DressHeight;
            public Rect VisibleBodyBounds;
            public Quaternion Rotation;
        }

        private void SetSegment(Transform segment, int startIndex, int endIndex, float radius)
        {
            if (!HasPoint(startIndex) || !HasPoint(endIndex))
            {
                return;
            }

            SetSegment(segment, smoothedPoints[startIndex], smoothedPoints[endIndex], radius);
        }

        private void SetSegment(Transform segment, Vector3 start, Vector3 end, float radius)
        {
            if (segment == null)
            {
                return;
            }

            start.z = overlayZ + debugRigZOffset;
            end.z = overlayZ + debugRigZOffset;
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.02f)
            {
                return;
            }

            segment.gameObject.SetActive(true);
            SetRenderersVisible(segment, showDebugRig);
            segment.localPosition = (start + end) * 0.5f;
            segment.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            segment.localScale = new Vector3(radius, length * 0.5f, radius);
        }

        private void SetSphere(Transform sphere, Vector3 position, float radius)
        {
            if (sphere == null)
            {
                return;
            }

            position.z = overlayZ + debugRigZOffset;
            sphere.gameObject.SetActive(true);
            SetRenderersVisible(sphere, showDebugRig);
            sphere.localPosition = position;
            sphere.localRotation = Quaternion.identity;
            sphere.localScale = Vector3.one * radius;
        }

        private static void SetRenderersVisible(Transform target, bool visible)
        {
            if (target == null)
            {
                return;
            }

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        private void ApplyDebugRigVisibility()
        {
            SetDebugRigVisible(showDebugRig);
        }

        private void SetDebugRigVisible(bool visible)
        {
            SetRenderersVisible(torso, visible);
            SetRenderersVisible(hips, visible);
            SetRenderersVisible(head, visible);
            SetRenderersVisible(leftUpperArm, visible);
            SetRenderersVisible(leftForearm, visible);
            SetRenderersVisible(rightUpperArm, visible);
            SetRenderersVisible(rightForearm, visible);
            SetRenderersVisible(leftThigh, visible);
            SetRenderersVisible(leftCalf, visible);
            SetRenderersVisible(rightThigh, visible);
            SetRenderersVisible(rightCalf, visible);
        }

        private void ClearSmoothedPoints()
        {
            for (int i = 0; i < hasPoint.Length; i++)
            {
                hasPoint[i] = false;
                smoothedPoints[i] = Vector3.zero;
                pointVelocities[i] = Vector3.zero;
                missedPointFrames[i] = 0;
            }

            lastPoseFilterTime = -1f;
            ResetGarmentAnchorFilter();
            ClearFitTarget();
            hasLastReliableBodyFit = false;
            lastReliableBodyFitTime = -1f;
            HideFitDebugOverlay();
        }

        private Vector3 ShoulderCenter()
        {
            if (HasPoint(LeftShoulder) && HasPoint(RightShoulder))
            {
                return (smoothedPoints[LeftShoulder] + smoothedPoints[RightShoulder]) * 0.5f;
            }

            return Vector3.up * 0.75f;
        }

        private Vector3 HipCenter()
        {
            if (HasPoint(LeftHip) && HasPoint(RightHip))
            {
                return (smoothedPoints[LeftHip] + smoothedPoints[RightHip]) * 0.5f;
            }

            return ShoulderCenter() + Vector3.down * 0.85f;
        }

        private Vector3 HeadPoint()
        {
            if (HasPoint(Nose))
            {
                return smoothedPoints[Nose] + Vector3.up * 0.07f;
            }

            return ShoulderCenter() + Vector3.up * 0.58f;
        }

        private bool HasPoint(int index)
        {
            return index >= 0 && index < hasPoint.Length && hasPoint[index];
        }

        private void OnGUI()
        {
            if (receiver == null)
            {
                return;
            }

            if (!receiver.HasFreshPose)
            {
                SetGarmentVisibility(!hideGarmentWhenPoseLost);
                ResetRuntimeGarmentRig();
                ClearFitTarget();
                SetDynamicSleevesVisible(false);
                SetDebugRigVisible(false);
            }

            string state = receiver.HasFreshPose
                ? $"MediaPipe tracking {receiver.LatestSource}"
                : "Waiting for pose landmarks";
            GUI.Label(new Rect(18f, 18f, 420f, 28f), state);
            GUI.Label(new Rect(18f, 138f, 960f, 28f), $"Skeleton D:{(showDebugRig ? "ON" : "OFF")}  |  mirror X:{(mirrorX ? "ON" : "OFF")}  |  rect {mappedRect.x:0.00},{mappedRect.y:0.00},{mappedRect.width:0.00},{mappedRect.height:0.00}  frame {mappedFrameWidth}x{mappedFrameHeight}");

            if (receiver.HasFreshPose)
            {
                GUI.Label(new Rect(18f, 162f, 820f, 28f), $"Fit center {lastFitTargetCenter}  target {lastFitTargetWidth:0.00} x {lastFitTargetHeight:0.00}  offset {fitOffset}  scale {fitScaleMultiplier:0.00}");
            }
            else
            {
                GUI.Label(new Rect(18f, 162f, 820f, 28f), $"No fresh pose. Packet age {receiver.PacketAgeSeconds:0.00}s; stale skeleton is hidden.");
            }
        }
    }
}
