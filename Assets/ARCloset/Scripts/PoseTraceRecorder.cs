using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ARCloset
{
    public sealed class PoseTraceRecorder : MonoBehaviour
    {
        [SerializeField] private MediaPipePoseReceiver receiver;
        [SerializeField] private bool recordOnStart;
        [SerializeField] private KeyCode toggleRecordingKey = KeyCode.R;
        [SerializeField] private string outputDirectory = "PoseTraces";
        [SerializeField] private string fileNamePrefix = "pose-trace";
        [SerializeField] private bool onlyRecordFreshPose = true;
        [SerializeField] private bool onlyRecordNewSequence = true;
        [SerializeField] private bool showOverlay = true;

        private StreamWriter writer;
        private int lastRecordedSequence = -1;
        private int framesWritten;
        private string lastOutputPath;

        public bool IsRecording => writer != null;
        public int FramesWritten => framesWritten;
        public string LastOutputPath => lastOutputPath;

        private void Reset()
        {
            receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
        }

        private void Start()
        {
            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            if (recordOnStart)
            {
                StartRecording();
            }
        }

        private void Update()
        {
            if (toggleRecordingKey != KeyCode.None && Input.GetKeyDown(toggleRecordingKey))
            {
                if (IsRecording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }

            if (!IsRecording || receiver == null)
            {
                return;
            }

            if (onlyRecordNewSequence && receiver.LatestSequence == lastRecordedSequence)
            {
                return;
            }

            if (!TryGetPacket(out MediaPipePosePacket packet))
            {
                return;
            }

            lastRecordedSequence = receiver.LatestSequence;
            writer.WriteLine(JsonUtility.ToJson(packet, false));
            framesWritten++;
        }

        private void OnDisable()
        {
            StopRecording();
        }

        private void OnDestroy()
        {
            StopRecording();
        }

        public void StartRecording()
        {
            if (IsRecording)
            {
                return;
            }

            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            string directory = ResolveDirectory(outputDirectory);
            Directory.CreateDirectory(directory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string fileName = string.IsNullOrWhiteSpace(fileNamePrefix) ? "pose-trace" : fileNamePrefix.Trim();
            lastOutputPath = Path.Combine(directory, $"{fileName}-{timestamp}.ndjson");
            writer = new StreamWriter(lastOutputPath, false);
            framesWritten = 0;
            lastRecordedSequence = onlyRecordNewSequence && receiver != null ? receiver.LatestSequence : -1;
            Debug.Log($"Pose trace recording started: {lastOutputPath}");
        }

        public void StopRecording()
        {
            if (writer == null)
            {
                return;
            }

            writer.Flush();
            writer.Dispose();
            writer = null;
            Debug.Log($"Pose trace recording stopped: {lastOutputPath} ({framesWritten} frames)");
        }

        private bool TryGetPacket(out MediaPipePosePacket packet)
        {
            packet = null;
            if (receiver == null)
            {
                return false;
            }

            if (onlyRecordFreshPose)
            {
                return receiver.TryGetLatestPacket(out packet);
            }

            receiver.TryGetLatestPacket(out packet);
            return packet?.landmarks != null && packet.landmarks.Length >= 33;
        }

        private static string ResolveDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = "PoseTraces";
            }

            if (Path.IsPathRooted(directory))
            {
                return directory;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", directory));
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            string state = IsRecording
                ? $"Pose trace REC {framesWritten} frames -> {Path.GetFileName(lastOutputPath)}"
                : $"Pose trace recorder idle ({toggleRecordingKey} to record)";
            GUI.Label(new Rect(18f, 190f, 760f, 24f), state);
        }
    }
}
