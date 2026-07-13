using UnityEngine;

namespace ARCloset
{
    public sealed class SyntheticPoseSource : MonoBehaviour
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

        public enum SyntheticPoseMode
        {
            Stable,
            Sway,
            Noisy,
            OutlierPulse,
            LowConfidencePulse,
            MissingLowerBodyPulse,
        }

        [SerializeField] private MediaPipePoseReceiver receiver;
        [SerializeField] private MediaPipeUnityPoseSource livePoseSource;
        [SerializeField] private SyntheticPoseMode mode = SyntheticPoseMode.Stable;
        [SerializeField] private bool playOnStart;
        [SerializeField] private KeyCode toggleSyntheticKey = KeyCode.G;
        [SerializeField, Range(1f, 60f)] private float syntheticFps = 30f;
        [SerializeField] private Vector2 center = new Vector2(0.5f, 0.49f);
        [SerializeField, Range(0.08f, 0.45f)] private float shoulderWidth = 0.26f;
        [SerializeField, Range(0.06f, 0.36f)] private float hipWidth = 0.18f;
        [SerializeField, Range(0.12f, 0.55f)] private float torsoHeight = 0.28f;
        [SerializeField, Range(0.12f, 0.65f)] private float legHeight = 0.36f;
        [SerializeField, Range(0f, 0.2f)] private float swayAmplitude = 0.08f;
        [SerializeField, Range(0.1f, 8f)] private float swayPeriodSeconds = 3.0f;
        [SerializeField, Range(0f, 0.08f)] private float noiseAmplitude = 0.018f;
        [SerializeField] private Vector2 outlierOffset = new Vector2(0.22f, -0.14f);
        [SerializeField, Range(0.4f, 6f)] private float pulsePeriodSeconds = 2.0f;
        [SerializeField, Range(0.01f, 0.4f)] private float lowConfidence = 0.08f;
        [SerializeField, Range(0.4f, 1f)] private float normalConfidence = 0.92f;
        [SerializeField] private int frameWidth = 640;
        [SerializeField] private int frameHeight = 480;
        [SerializeField] private bool inputMirrored = true;
        [SerializeField] private bool previewMirrored = true;
        [SerializeField] private bool stopLivePoseSourceWhilePlaying = true;
        [SerializeField] private bool restoreLivePoseSourceOnStop = true;
        [SerializeField] private bool showOverlay;

        private bool isPlaying;
        private float startedAt;
        private float nextPacketTime;
        private bool liveSourceStateCaptured;
        private bool liveSourceWasEnabled;
        private bool liveSourceWasRunning;

        public bool IsPlaying => isPlaying;
        public SyntheticPoseMode Mode
        {
            get => mode;
            set => mode = value;
        }

        private void Reset()
        {
            receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            livePoseSource = FindAnyObjectByType<MediaPipeUnityPoseSource>();
        }

        private void Start()
        {
            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            if (livePoseSource == null)
            {
                livePoseSource = FindAnyObjectByType<MediaPipeUnityPoseSource>();
            }

            if (playOnStart)
            {
                StartSyntheticPose();
            }
        }

        private void Update()
        {
            if (toggleSyntheticKey != KeyCode.None && Input.GetKeyDown(toggleSyntheticKey))
            {
                if (isPlaying)
                {
                    StopSyntheticPose();
                }
                else
                {
                    StartSyntheticPose();
                }
            }

            if (!isPlaying)
            {
                return;
            }

            if (Time.realtimeSinceStartup < nextPacketTime)
            {
                return;
            }

            PushSyntheticPacket();
            nextPacketTime += 1f / Mathf.Max(1f, syntheticFps);
        }

        private void OnDisable()
        {
            StopSyntheticPose();
        }

        private void OnDestroy()
        {
            StopSyntheticPose();
        }

        public void StartSyntheticPose()
        {
            if (isPlaying)
            {
                return;
            }

            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            if (receiver == null)
            {
                Debug.LogWarning("Synthetic pose cannot start without a MediaPipePoseReceiver.");
                return;
            }

            if (stopLivePoseSourceWhilePlaying)
            {
                StopLivePoseSourceForSynthetic();
            }

            startedAt = Time.realtimeSinceStartup;
            nextPacketTime = Time.realtimeSinceStartup;
            isPlaying = true;
            Debug.Log($"Synthetic pose started: {mode}");
        }

        public void StopSyntheticPose()
        {
            if (!isPlaying && !liveSourceStateCaptured)
            {
                return;
            }

            isPlaying = false;
            RestoreLivePoseSourceAfterSynthetic();
            Debug.Log("Synthetic pose stopped.");
        }

        private void PushSyntheticPacket()
        {
            float t = Time.realtimeSinceStartup - startedAt;
            MediaPipePosePacket packet = BuildPacket(t);
            receiver.PushPacket(packet, $"SyntheticPose:{mode}");
        }

        private MediaPipePosePacket BuildPacket(float t)
        {
            MediaPipePoseLandmark[] landmarks = new MediaPipePoseLandmark[33];
            for (int i = 0; i < landmarks.Length; i++)
            {
                landmarks[i] = MakeLandmark(center.x, center.y, 0f, 0.05f);
            }

            Vector2 poseCenter = center;
            if (mode == SyntheticPoseMode.Sway)
            {
                float phase = Mathf.PI * 2f * t / Mathf.Max(0.1f, swayPeriodSeconds);
                poseCenter.x += Mathf.Sin(phase) * swayAmplitude;
                poseCenter.y += Mathf.Sin(phase * 0.5f) * swayAmplitude * 0.2f;
            }

            float shoulderY = poseCenter.y - torsoHeight * 0.5f;
            float hipY = poseCenter.y + torsoHeight * 0.5f;
            float kneeY = hipY + legHeight * 0.48f;
            float ankleY = hipY + legHeight;
            float armElbowY = shoulderY + torsoHeight * 0.55f;
            float wristY = shoulderY + torsoHeight * 1.05f;

            Set(landmarks, Nose, poseCenter.x, shoulderY - torsoHeight * 0.55f, -0.02f, normalConfidence, t);
            Set(landmarks, LeftShoulder, poseCenter.x - shoulderWidth * 0.5f, shoulderY, 0f, normalConfidence, t);
            Set(landmarks, RightShoulder, poseCenter.x + shoulderWidth * 0.5f, shoulderY, 0f, normalConfidence, t);
            Set(landmarks, LeftElbow, poseCenter.x - shoulderWidth * 0.78f, armElbowY, -0.02f, normalConfidence, t);
            Set(landmarks, RightElbow, poseCenter.x + shoulderWidth * 0.78f, armElbowY, -0.02f, normalConfidence, t);
            Set(landmarks, LeftWrist, poseCenter.x - shoulderWidth * 0.85f, wristY, -0.04f, normalConfidence, t);
            Set(landmarks, RightWrist, poseCenter.x + shoulderWidth * 0.85f, wristY, -0.04f, normalConfidence, t);
            Set(landmarks, LeftHip, poseCenter.x - hipWidth * 0.5f, hipY, 0.02f, normalConfidence, t);
            Set(landmarks, RightHip, poseCenter.x + hipWidth * 0.5f, hipY, 0.02f, normalConfidence, t);
            Set(landmarks, LeftKnee, poseCenter.x - hipWidth * 0.38f, kneeY, 0.03f, normalConfidence, t);
            Set(landmarks, RightKnee, poseCenter.x + hipWidth * 0.38f, kneeY, 0.03f, normalConfidence, t);
            Set(landmarks, LeftAnkle, poseCenter.x - hipWidth * 0.34f, ankleY, 0.04f, normalConfidence, t);
            Set(landmarks, RightAnkle, poseCenter.x + hipWidth * 0.34f, ankleY, 0.04f, normalConfidence, t);

            ApplyPulseModes(landmarks, t);

            return new MediaPipePosePacket
            {
                version = 1,
                timestamp = Time.realtimeSinceStartupAsDouble,
                frameWidth = frameWidth,
                frameHeight = frameHeight,
                displayRotationDegrees = 0,
                inputMirrored = inputMirrored,
                previewMirrored = previewMirrored,
                landmarks = landmarks,
                worldLandmarks = null,
            };
        }

        private void Set(
            MediaPipePoseLandmark[] landmarks,
            int index,
            float x,
            float y,
            float z,
            float confidence,
            float t)
        {
            if (mode == SyntheticPoseMode.Noisy)
            {
                x += PseudoNoise(index, t, 0.17f) * noiseAmplitude;
                y += PseudoNoise(index, t, 0.61f) * noiseAmplitude;
            }

            landmarks[index] = MakeLandmark(x, y, z, confidence);
        }

        private void ApplyPulseModes(MediaPipePoseLandmark[] landmarks, float t)
        {
            bool inPulse = Mathf.Repeat(t, Mathf.Max(0.1f, pulsePeriodSeconds)) <= Mathf.Max(0.05f, 1.5f / syntheticFps);
            if (!inPulse)
            {
                return;
            }

            switch (mode)
            {
                case SyntheticPoseMode.OutlierPulse:
                    Offset(landmarks, LeftShoulder, outlierOffset);
                    Offset(landmarks, RightShoulder, outlierOffset);
                    Offset(landmarks, LeftHip, outlierOffset);
                    Offset(landmarks, RightHip, outlierOffset);
                    Offset(landmarks, LeftKnee, outlierOffset);
                    Offset(landmarks, RightKnee, outlierOffset);
                    break;
                case SyntheticPoseMode.LowConfidencePulse:
                    SetConfidence(landmarks, LeftShoulder, lowConfidence);
                    SetConfidence(landmarks, RightShoulder, lowConfidence);
                    SetConfidence(landmarks, LeftHip, lowConfidence);
                    SetConfidence(landmarks, RightHip, lowConfidence);
                    break;
                case SyntheticPoseMode.MissingLowerBodyPulse:
                    SetConfidence(landmarks, LeftHip, 0f);
                    SetConfidence(landmarks, RightHip, 0f);
                    SetConfidence(landmarks, LeftKnee, 0f);
                    SetConfidence(landmarks, RightKnee, 0f);
                    SetConfidence(landmarks, LeftAnkle, 0f);
                    SetConfidence(landmarks, RightAnkle, 0f);
                    break;
            }
        }

        private static MediaPipePoseLandmark MakeLandmark(float x, float y, float z, float confidence)
        {
            return new MediaPipePoseLandmark
            {
                x = Mathf.Clamp(x, -0.5f, 1.5f),
                y = Mathf.Clamp(y, -0.5f, 1.5f),
                z = z,
                visibility = Mathf.Clamp01(confidence),
                presence = Mathf.Clamp01(confidence),
            };
        }

        private static void Offset(MediaPipePoseLandmark[] landmarks, int index, Vector2 offset)
        {
            MediaPipePoseLandmark landmark = landmarks[index];
            landmark.x += offset.x;
            landmark.y += offset.y;
            landmarks[index] = landmark;
        }

        private static void SetConfidence(MediaPipePoseLandmark[] landmarks, int index, float confidence)
        {
            MediaPipePoseLandmark landmark = landmarks[index];
            landmark.visibility = Mathf.Clamp01(confidence);
            landmark.presence = Mathf.Clamp01(confidence);
            landmarks[index] = landmark;
        }

        private static float PseudoNoise(int index, float t, float salt)
        {
            float value = Mathf.Sin((index * 78.233f + t * 12.9898f + salt * 37.719f) * 12.17f) * 43758.5453f;
            return (value - Mathf.Floor(value)) * 2f - 1f;
        }

        private void StopLivePoseSourceForSynthetic()
        {
            if (livePoseSource == null)
            {
                livePoseSource = FindAnyObjectByType<MediaPipeUnityPoseSource>();
            }

            if (livePoseSource == null)
            {
                return;
            }

            liveSourceWasEnabled = livePoseSource.enabled;
            liveSourceWasRunning = livePoseSource.IsRunning;
            liveSourceStateCaptured = true;
            livePoseSource.StopSource();
            livePoseSource.enabled = false;
        }

        private void RestoreLivePoseSourceAfterSynthetic()
        {
            if (!restoreLivePoseSourceOnStop || !liveSourceStateCaptured || livePoseSource == null)
            {
                liveSourceStateCaptured = false;
                return;
            }

            livePoseSource.enabled = liveSourceWasEnabled;
            if (liveSourceWasEnabled && liveSourceWasRunning && livePoseSource.isActiveAndEnabled)
            {
                livePoseSource.StartSource();
            }

            liveSourceStateCaptured = false;
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            string state = isPlaying
                ? $"Synthetic pose {mode} {syntheticFps:0.#}fps"
                : $"Synthetic pose idle ({toggleSyntheticKey} to toggle)";
            GUI.Label(new Rect(18f, 242f, 760f, 24f), state);
        }
    }
}
