using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ARCloset
{
    public sealed class PoseTraceReplaySource : MonoBehaviour
    {
        [Serializable]
        private sealed class PoseTracePacketList
        {
            public MediaPipePosePacket[] packets;
        }

        [SerializeField] private MediaPipePoseReceiver receiver;
        [SerializeField] private MediaPipeUnityPoseSource livePoseSource;
        [SerializeField] private TextAsset traceAsset;
        [SerializeField] private string traceFilePath;
        [SerializeField] private string traceDirectory = "PoseTraces";
        [SerializeField] private bool loadLatestTraceFromDirectory = true;
        [SerializeField] private bool reloadTraceOnStart = true;
        [SerializeField] private bool playOnStart;
        [SerializeField] private KeyCode toggleReplayKey = KeyCode.P;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool usePacketTimestamps = true;
        [SerializeField, Range(0.1f, 4f)] private float playbackSpeed = 1f;
        [SerializeField, Range(1f, 60f)] private float fallbackFps = 30f;
        [SerializeField, Range(1, 12)] private int maxCatchUpFramesPerUpdate = 4;
        [SerializeField] private bool stopLivePoseSourceWhileReplaying = true;
        [SerializeField] private bool restoreLivePoseSourceOnStop = true;
        [SerializeField] private bool showOverlay = true;

        private readonly List<MediaPipePosePacket> packets = new();
        private bool isReplaying;
        private int packetIndex;
        private float nextPacketTime;
        private string loadedTraceName;
        private bool liveSourceStateCaptured;
        private bool liveSourceWasEnabled;
        private bool liveSourceWasRunning;

        public bool IsReplaying => isReplaying;
        public int LoadedFrameCount => packets.Count;
        public int CurrentFrameIndex => packetIndex;
        public string LoadedTraceName => loadedTraceName;

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
                StartReplay();
            }
        }

        private void Update()
        {
            if (toggleReplayKey != KeyCode.None && Input.GetKeyDown(toggleReplayKey))
            {
                if (isReplaying)
                {
                    StopReplay();
                }
                else
                {
                    StartReplay();
                }
            }

            if (!isReplaying)
            {
                return;
            }

            int catchUpFrames = 0;
            while (isReplaying &&
                   Time.realtimeSinceStartup >= nextPacketTime &&
                   catchUpFrames < maxCatchUpFramesPerUpdate)
            {
                PushCurrentPacket();
                catchUpFrames++;
            }
        }

        private void OnDisable()
        {
            StopReplay();
        }

        private void OnDestroy()
        {
            StopReplay();
        }

        public void StartReplay()
        {
            if (isReplaying)
            {
                return;
            }

            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            if (receiver == null)
            {
                Debug.LogWarning("Pose trace replay cannot start without a MediaPipePoseReceiver.");
                return;
            }

            if ((reloadTraceOnStart || packets.Count == 0) && !LoadTrace())
            {
                Debug.LogWarning("Pose trace replay has no loaded packets.");
                return;
            }

            if (stopLivePoseSourceWhileReplaying)
            {
                StopLivePoseSourceForReplay();
            }

            packetIndex = 0;
            nextPacketTime = Time.realtimeSinceStartup;
            isReplaying = true;
            Debug.Log($"Pose trace replay started: {loadedTraceName} ({packets.Count} frames)");
        }

        public void StopReplay()
        {
            if (!isReplaying && !liveSourceStateCaptured)
            {
                return;
            }

            isReplaying = false;
            RestoreLivePoseSourceAfterReplay();
            Debug.Log("Pose trace replay stopped.");
        }

        public bool LoadTrace()
        {
            packets.Clear();
            loadedTraceName = string.Empty;

            string text = null;
            if (traceAsset != null)
            {
                text = traceAsset.text;
                loadedTraceName = traceAsset.name;
            }
            else
            {
                string path = ResolveTraceFilePath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return false;
                }

                text = File.ReadAllText(path);
                loadedTraceName = path;
            }

            ParseTraceText(text, packets);
            if (packets.Count == 0)
            {
                Debug.LogWarning($"Pose trace contains no valid packets: {loadedTraceName}");
                return false;
            }

            return true;
        }

        private void PushCurrentPacket()
        {
            if (packetIndex < 0 || packetIndex >= packets.Count)
            {
                if (loop)
                {
                    packetIndex = 0;
                }
                else
                {
                    StopReplay();
                    return;
                }
            }

            int pushedIndex = packetIndex;
            MediaPipePosePacket packet = ClonePacket(packets[pushedIndex]);
            packet.timestamp = Time.realtimeSinceStartupAsDouble;
            receiver.PushPacket(packet, "PoseTraceReplay");

            packetIndex++;
            if (packetIndex >= packets.Count)
            {
                if (!loop)
                {
                    StopReplay();
                    return;
                }

                packetIndex = 0;
            }

            nextPacketTime += NextDelaySeconds(pushedIndex, packetIndex);
        }

        private float NextDelaySeconds(int currentIndex, int nextIndex)
        {
            float fallbackDelay = 1f / Mathf.Max(1f, fallbackFps);
            if (!usePacketTimestamps || packets.Count <= 1 || currentIndex == nextIndex)
            {
                return fallbackDelay / Mathf.Max(0.01f, playbackSpeed);
            }

            double currentTime = packets[currentIndex].timestamp;
            double nextTime = packets[nextIndex].timestamp;
            double delta = nextTime - currentTime;
            if (delta <= 0.0001 || delta > 2.0)
            {
                return fallbackDelay / Mathf.Max(0.01f, playbackSpeed);
            }

            return Mathf.Clamp((float)(delta / Mathf.Max(0.01f, playbackSpeed)), 1f / 120f, 2f);
        }

        private string ResolveTraceFilePath()
        {
            if (!string.IsNullOrWhiteSpace(traceFilePath))
            {
                return ResolvePath(traceFilePath);
            }

            if (!loadLatestTraceFromDirectory)
            {
                return null;
            }

            string directory = ResolvePath(traceDirectory);
            if (!Directory.Exists(directory))
            {
                return null;
            }

            return Directory
                .GetFiles(directory, "*.ndjson")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        private static void ParseTraceText(string text, List<MediaPipePosePacket> target)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) &&
                trimmed.IndexOf("\"packets\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PoseTracePacketList packetList = JsonUtility.FromJson<PoseTracePacketList>(trimmed);
                if (packetList?.packets != null)
                {
                    for (int i = 0; i < packetList.packets.Length; i++)
                    {
                        AddIfValid(target, packetList.packets[i]);
                    }
                }

                return;
            }

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                try
                {
                    MediaPipePosePacket packet = JsonUtility.FromJson<MediaPipePosePacket>(lines[i]);
                    AddIfValid(target, packet);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Pose trace line {i + 1} could not be parsed: {exception.Message}");
                }
            }
        }

        private static void AddIfValid(List<MediaPipePosePacket> target, MediaPipePosePacket packet)
        {
            if (packet?.landmarks == null || packet.landmarks.Length < 33)
            {
                return;
            }

            target.Add(packet);
        }

        private static MediaPipePosePacket ClonePacket(MediaPipePosePacket packet)
        {
            return new MediaPipePosePacket
            {
                version = packet.version,
                timestamp = packet.timestamp,
                frameWidth = packet.frameWidth,
                frameHeight = packet.frameHeight,
                displayRotationDegrees = packet.displayRotationDegrees,
                inputMirrored = packet.inputMirrored,
                previewMirrored = packet.previewMirrored,
                landmarks = CloneLandmarks(packet.landmarks),
                worldLandmarks = CloneLandmarks(packet.worldLandmarks),
            };
        }

        private static MediaPipePoseLandmark[] CloneLandmarks(MediaPipePoseLandmark[] landmarks)
        {
            if (landmarks == null)
            {
                return null;
            }

            MediaPipePoseLandmark[] clone = new MediaPipePoseLandmark[landmarks.Length];
            Array.Copy(landmarks, clone, landmarks.Length);
            return clone;
        }

        private void StopLivePoseSourceForReplay()
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

        private void RestoreLivePoseSourceAfterReplay()
        {
            if (!restoreLivePoseSourceOnStop || !liveSourceStateCaptured || livePoseSource == null)
            {
                liveSourceStateCaptured = false;
                return;
            }

            livePoseSource.enabled = liveSourceWasEnabled;
            if (liveSourceWasEnabled && liveSourceWasRunning)
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

            string state = isReplaying
                ? $"Pose replay {packetIndex}/{packets.Count} {Path.GetFileName(loadedTraceName)}"
                : $"Pose replay idle ({toggleReplayKey} to play latest trace)";
            GUI.Label(new Rect(18f, 216f, 760f, 24f), state);
        }
    }
}
