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
        [SerializeField] private float smoothing = 0.34f;
        [SerializeField] private float overlayZ = 0.0f;

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
        [SerializeField] private bool fitGarmentByRendererBounds = true;
        [SerializeField] private bool clampGarmentTargetToCamera = true;
        [SerializeField] private float restShoulderWidth = 0.92f;
        [SerializeField] private float minGarmentScale = 0.12f;
        [SerializeField] private float maxGarmentScale = 8.0f;
        [SerializeField] private float minFitVisibility = 0.24f;
        [SerializeField] private float normalizedOverscan = 0.08f;
        [SerializeField] private float fitScaleMultiplier = 1.0f;
        [SerializeField] private Vector2 fitOffset = Vector2.zero;
        [SerializeField] private float keyboardOffsetStep = 0.04f;
        [SerializeField] private float keyboardScaleStep = 0.04f;
        [SerializeField] private float upperWidthPadding = 1.18f;
        [SerializeField] private float lowerWidthPadding = 1.22f;
        [SerializeField] private float onePieceWidthPadding = 1.18f;
        [SerializeField] private float outerwearWidthPadding = 1.28f;

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
        private readonly bool[] hasPoint = new bool[33];
        private int lastSequence = -1;
        private float mappedWidth;
        private float mappedHeight;
        private int displayRotationDegrees;
        private int mappedFrameWidth;
        private int mappedFrameHeight;
        private Rect mappedRect = new Rect(-1.5f, -1.5f, 3.0f, 3.0f);
        private Vector3 lastFitTargetCenter;
        private float lastFitTargetWidth;
        private float lastFitTargetHeight;

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
                SetDynamicSleevesVisible(false);
                SetDebugRigVisible(false);
                ClearSmoothedPoints();
                return false;
            }

            if (packet.landmarks == null || packet.landmarks.Length < 33)
            {
                SetGarmentVisibility(!hideGarmentWhenPoseLost);
                ResetRuntimeGarmentRig();
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
            float alpha = newPacket ? Mathf.Clamp01(smoothing) : 1f;
            UpdateViewportMapping(packet);
            bool swapLeftRight = keepVisualLeftRightOrder && ShouldSwapLeftRight(packet);

            for (int i = 0; i < smoothedPoints.Length; i++)
            {
                MediaPipePoseLandmark landmark = packet.landmarks[i];
                bool visible = landmark.visibility >= minVisibility || landmark.presence >= minVisibility;

                if (!visible)
                {
                    continue;
                }

                int targetIndex = swapLeftRight ? SwappedPoseIndex(i) : i;
                Vector3 target = ToRigPoint(landmark, packet);

                if (!hasPoint[targetIndex])
                {
                    smoothedPoints[targetIndex] = target;
                    hasPoint[targetIndex] = true;
                }
                else
                {
                    smoothedPoints[targetIndex] = Vector3.Lerp(smoothedPoints[targetIndex], target, alpha);
                }
            }
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
            if (garmentAnchor == null || !HasPoint(LeftShoulder) || !HasPoint(RightShoulder))
            {
                return;
            }

            BodyFit bodyFit = BuildBodyFit();
            GarmentSlot slot = fittingController != null ? fittingController.CurrentSlot : GarmentSlot.Upper;

            if (!HasReliableFitInput(packet, slot))
            {
                SetGarmentVisibility(!hideGarmentWhenPoseLost);
                ResetRuntimeGarmentRig();
                SetDynamicSleevesVisible(false);
                return;
            }

            SetDebugRigVisible(showDebugRig);
            SetGarmentVisibility(true);
            GetGarmentTarget(slot, bodyFit, out Vector3 targetCenter, out float targetWidth, out float targetHeight, out float heightBlend);
            Vector3 targetAnchor = GetGarmentAnchorTarget(slot, bodyFit);
            Vector3 offset = new Vector3(fitOffset.x, fitOffset.y, 0f);
            targetCenter += offset;
            targetAnchor += offset;
            targetWidth *= fitScaleMultiplier;
            targetHeight *= fitScaleMultiplier;
            Quaternion targetRotation = bodyFit.Rotation;
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

            garmentAnchor.localPosition = Vector3.Lerp(garmentAnchor.localPosition, targetPosition, smoothing);
            garmentAnchor.localRotation = Quaternion.Slerp(garmentAnchor.localRotation, targetRotation, smoothing);
            garmentAnchor.localScale = Vector3.Lerp(garmentAnchor.localScale, Vector3.one * scale, smoothing);
            ApplyRuntimeGarmentRig();
            ApplyDynamicSleeves(slot);
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
            if (shoulderWidthNormalized < 0.035f)
            {
                return false;
            }

            switch (slot)
            {
                case GarmentSlot.Lower:
                    return IsReliableLandmark(packet, LeftHip) &&
                           IsReliableLandmark(packet, RightHip) &&
                           (IsReliableLandmark(packet, LeftKnee) || IsReliableLandmark(packet, RightKnee));
                case GarmentSlot.OnePiece:
                    return IsReliableLandmark(packet, LeftHip) &&
                           IsReliableLandmark(packet, RightHip);
                case GarmentSlot.Outerwear:
                case GarmentSlot.Upper:
                default:
                    return true;
            }
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

        private BodyFit BuildBodyFit()
        {
            Vector3 shoulderCenter = ShoulderCenter();
            Vector3 hipCenter = HasPoint(LeftHip) && HasPoint(RightHip)
                ? HipCenter()
                : shoulderCenter + Vector3.down * Mathf.Max(0.8f, mappedHeight * 0.18f);
            Vector3 kneeCenter = HasPoint(LeftKnee) && HasPoint(RightKnee)
                ? (smoothedPoints[LeftKnee] + smoothedPoints[RightKnee]) * 0.5f
                : hipCenter + (hipCenter - shoulderCenter);
            Vector3 ankleCenter = HasPoint(LeftAnkle) && HasPoint(RightAnkle)
                ? (smoothedPoints[LeftAnkle] + smoothedPoints[RightAnkle]) * 0.5f
                : hipCenter + (hipCenter - shoulderCenter) * 1.85f;

            float shoulderWidth = Vector3.Distance(smoothedPoints[LeftShoulder], smoothedPoints[RightShoulder]);
            float hipWidth = HasPoint(LeftHip) && HasPoint(RightHip)
                ? Vector3.Distance(smoothedPoints[LeftHip], smoothedPoints[RightHip])
                : shoulderWidth * 0.78f;
            float torsoHeight = Mathf.Max(0.001f, Vector3.Distance(shoulderCenter, hipCenter));
            float legHeight = Mathf.Max(0.001f, Vector3.Distance(hipCenter, ankleCenter));
            float dressHeight = Mathf.Max(torsoHeight * 1.55f, Vector3.Distance(shoulderCenter, kneeCenter));

            Vector3 shoulderVector = smoothedPoints[RightShoulder] - smoothedPoints[LeftShoulder];
            float tilt = Mathf.Atan2(shoulderVector.y, shoulderVector.x) * Mathf.Rad2Deg;

            return new BodyFit
            {
                ShoulderCenter = shoulderCenter,
                HipCenter = hipCenter,
                KneeCenter = kneeCenter,
                AnkleCenter = ankleCenter,
                ShoulderWidth = shoulderWidth,
                HipWidth = hipWidth,
                TorsoHeight = torsoHeight,
                LegHeight = legHeight,
                DressHeight = dressHeight,
                Rotation = Quaternion.AngleAxis(Mathf.Clamp(tilt, -22f, 22f), Vector3.forward),
            };
        }

        private void GetGarmentTarget(
            GarmentSlot slot,
            BodyFit bodyFit,
            out Vector3 targetCenter,
            out float targetWidth,
            out float targetHeight,
            out float heightBlend)
        {
            switch (slot)
            {
                case GarmentSlot.Lower:
                    targetCenter = Vector3.Lerp(bodyFit.HipCenter, bodyFit.AnkleCenter, 0.48f);
                    targetWidth = Mathf.Max(bodyFit.HipWidth, bodyFit.ShoulderWidth * 0.58f) * lowerWidthPadding;
                    targetHeight = bodyFit.LegHeight * 1.04f;
                    heightBlend = 0.35f;
                    break;
                case GarmentSlot.OnePiece:
                    targetCenter = Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.KneeCenter, 0.52f);
                    targetWidth = Mathf.Max(bodyFit.ShoulderWidth, bodyFit.HipWidth) * onePieceWidthPadding;
                    targetHeight = bodyFit.DressHeight * 1.05f;
                    heightBlend = 0.45f;
                    break;
                case GarmentSlot.Outerwear:
                    targetCenter = Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.HipCenter, 0.54f);
                    targetWidth = Mathf.Max(bodyFit.ShoulderWidth, bodyFit.HipWidth * 0.92f) * outerwearWidthPadding;
                    targetHeight = bodyFit.TorsoHeight * 1.22f;
                    heightBlend = 0.2f;
                    break;
                case GarmentSlot.Upper:
                default:
                    targetCenter = Vector3.Lerp(bodyFit.ShoulderCenter, bodyFit.HipCenter, 0.50f);
                    targetWidth = Mathf.Max(bodyFit.ShoulderWidth, bodyFit.HipWidth * 0.86f) * upperWidthPadding;
                    targetHeight = bodyFit.TorsoHeight * 1.16f;
                    heightBlend = 0.18f;
                    break;
            }

            targetCenter.z = overlayZ;
        }

        private Vector3 GetGarmentAnchorTarget(GarmentSlot slot, BodyFit bodyFit)
        {
            Vector3 target = slot == GarmentSlot.Lower
                ? bodyFit.HipCenter
                : bodyFit.ShoulderCenter;
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
            public float TorsoHeight;
            public float LegHeight;
            public float DressHeight;
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
            }
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
