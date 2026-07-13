using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ARCloset
{
    public sealed class PoseFitStabilityMonitor : MonoBehaviour
    {
        [SerializeField] private MediaPipePoseReceiver receiver;
        [SerializeField] private MediaPipePoseRigDriver rigDriver;
        [SerializeField] private bool monitorOnStart;
        [SerializeField] private KeyCode toggleMonitorKey = KeyCode.V;
        [SerializeField, Range(0.5f, 30f)] private float reportWindowSeconds = 5f;
        [SerializeField, Range(0.1f, 30f)] private float minPoseFps = 1f;
        [SerializeField, Range(0.001f, 1f)] private float maxAnchorStd = 0.08f;
        [SerializeField, Range(0.001f, 2f)] private float maxAnchorStep = 0.25f;
        [SerializeField, Range(0.001f, 0.5f)] private float maxScaleStd = 0.05f;
        [SerializeField, Range(0.001f, 1f)] private float maxTargetCenterStd = 0.10f;
        [SerializeField] private bool writeCsv = true;
        [SerializeField] private string outputDirectory = "PoseValidation";
        [SerializeField] private bool logPassingWindows = true;
        [SerializeField] private bool showOverlay;

        private bool isMonitoring;
        private StreamWriter csvWriter;
        private string csvPath;
        private WindowStats stats;
        private string lastSummary = "Pose fit monitor idle";
        private bool lastWindowPassed = true;

        public bool IsMonitoring => isMonitoring;
        public string LastSummary => lastSummary;
        public bool LastWindowPassed => lastWindowPassed;
        public string CsvPath => csvPath;

        private void Reset()
        {
            receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            rigDriver = FindAnyObjectByType<MediaPipePoseRigDriver>();
        }

        private void Start()
        {
            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            if (rigDriver == null)
            {
                rigDriver = FindAnyObjectByType<MediaPipePoseRigDriver>();
            }

            if (monitorOnStart)
            {
                StartMonitoring();
            }
        }

        private void Update()
        {
            if (toggleMonitorKey != KeyCode.None && Input.GetKeyDown(toggleMonitorKey))
            {
                if (isMonitoring)
                {
                    StopMonitoring();
                }
                else
                {
                    StartMonitoring();
                }
            }

            if (!isMonitoring)
            {
                return;
            }

            Sample();
            if (Time.realtimeSinceStartup - stats.StartedAt >= reportWindowSeconds)
            {
                ReportWindow();
                ResetWindow();
            }
        }

        private void OnDisable()
        {
            StopMonitoring();
        }

        private void OnDestroy()
        {
            StopMonitoring();
        }

        public void StartMonitoring()
        {
            if (isMonitoring)
            {
                return;
            }

            if (receiver == null)
            {
                receiver = FindAnyObjectByType<MediaPipePoseReceiver>();
            }

            if (rigDriver == null)
            {
                rigDriver = FindAnyObjectByType<MediaPipePoseRigDriver>();
            }

            if (writeCsv)
            {
                string directory = ResolveDirectory(outputDirectory);
                Directory.CreateDirectory(directory);
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                csvPath = Path.Combine(directory, $"pose-fit-validation-{timestamp}.csv");
                csvWriter = new StreamWriter(csvPath, false);
                csvWriter.WriteLine("time,elapsed,samples,poseFps,staleSamples,missingTargetSamples,anchorStd,anchorMaxStep,scaleStd,targetCenterStd,passed,source");
            }

            isMonitoring = true;
            ResetWindow();
            lastSummary = "Pose fit monitor started";
            Debug.Log(lastSummary);
        }

        public void StopMonitoring()
        {
            if (!isMonitoring && csvWriter == null)
            {
                return;
            }

            isMonitoring = false;
            if (csvWriter != null)
            {
                csvWriter.Flush();
                csvWriter.Dispose();
                csvWriter = null;
            }

            lastSummary = string.IsNullOrEmpty(csvPath)
                ? "Pose fit monitor stopped"
                : $"Pose fit monitor stopped: {csvPath}";
            Debug.Log(lastSummary);
        }

        private void Sample()
        {
            if (receiver == null || rigDriver == null)
            {
                stats.MissingTargetSamples++;
                stats.Samples++;
                return;
            }

            if (!receiver.HasFreshPose)
            {
                stats.StaleSamples++;
            }

            if (rigDriver.HasFitTarget)
            {
                stats.AddTarget(rigDriver.LastFitTargetCenter);
            }
            else
            {
                stats.MissingTargetSamples++;
            }

            Transform anchor = rigDriver.GarmentAnchorTransform;
            if (anchor != null && rigDriver.HasGarmentAnchorFilter)
            {
                stats.AddAnchor(anchor.localPosition, Mathf.Max(0.0001f, anchor.localScale.x));
            }
            else
            {
                stats.MissingAnchorSamples++;
            }

            stats.Samples++;
        }

        private void ReportWindow()
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = Mathf.Max(0.001f, now - stats.StartedAt);
            int sequence = receiver != null ? receiver.LatestSequence : stats.StartSequence;
            float poseFps = Mathf.Max(0, sequence - stats.StartSequence) / elapsed;
            float anchorStd = stats.AnchorStdMagnitude();
            float scaleStd = stats.ScaleStd();
            float targetStd = stats.TargetStdMagnitude();
            bool hasEnoughSamples = stats.Samples > 2 && stats.AnchorSamples > 1 && stats.TargetSamples > 1;
            bool passed = hasEnoughSamples &&
                          poseFps >= minPoseFps &&
                          anchorStd <= maxAnchorStd &&
                          stats.AnchorMaxStep <= maxAnchorStep &&
                          scaleStd <= maxScaleStd &&
                          targetStd <= maxTargetCenterStd &&
                          stats.StaleSamples == 0 &&
                          stats.MissingTargetSamples == 0;

            lastWindowPassed = passed;
            string source = receiver != null ? receiver.LatestSource : "none";
            lastSummary =
                $"Pose fit {(passed ? "PASS" : "FAIL")} " +
                $"fps={poseFps:0.00} anchorStd={anchorStd:0.000} step={stats.AnchorMaxStep:0.000} " +
                $"scaleStd={scaleStd:0.000} targetStd={targetStd:0.000} stale={stats.StaleSamples} missing={stats.MissingTargetSamples}";

            if (!passed)
            {
                Debug.LogWarning(lastSummary);
            }
            else if (logPassingWindows)
            {
                Debug.Log(lastSummary);
            }

            if (csvWriter != null)
            {
                csvWriter.WriteLine(string.Join(
                    ",",
                    now.ToString("F3", CultureInfo.InvariantCulture),
                    elapsed.ToString("F3", CultureInfo.InvariantCulture),
                    stats.Samples.ToString(CultureInfo.InvariantCulture),
                    poseFps.ToString("F3", CultureInfo.InvariantCulture),
                    stats.StaleSamples.ToString(CultureInfo.InvariantCulture),
                    stats.MissingTargetSamples.ToString(CultureInfo.InvariantCulture),
                    anchorStd.ToString("F5", CultureInfo.InvariantCulture),
                    stats.AnchorMaxStep.ToString("F5", CultureInfo.InvariantCulture),
                    scaleStd.ToString("F5", CultureInfo.InvariantCulture),
                    targetStd.ToString("F5", CultureInfo.InvariantCulture),
                    passed ? "1" : "0",
                    source));
                csvWriter.Flush();
            }
        }

        private void ResetWindow()
        {
            stats = new WindowStats
            {
                StartedAt = Time.realtimeSinceStartup,
                StartSequence = receiver != null ? receiver.LatestSequence : 0,
            };
        }

        private static string ResolveDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = "PoseValidation";
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

            string state = isMonitoring
                ? lastSummary
                : $"Pose fit monitor idle ({toggleMonitorKey} to start)";
            GUI.Label(new Rect(18f, 268f, 980f, 24f), state);
        }

        private struct WindowStats
        {
            public float StartedAt;
            public int StartSequence;
            public int Samples;
            public int StaleSamples;
            public int MissingTargetSamples;
            public int MissingAnchorSamples;
            public int AnchorSamples;
            public int TargetSamples;
            public Vector3 AnchorSum;
            public Vector3 AnchorSumSquares;
            public Vector3 TargetSum;
            public Vector3 TargetSumSquares;
            public float ScaleSum;
            public float ScaleSumSquares;
            public float AnchorMaxStep;
            public Vector3 LastAnchor;
            public bool HasLastAnchor;

            public void AddAnchor(Vector3 position, float scale)
            {
                if (HasLastAnchor)
                {
                    AnchorMaxStep = Mathf.Max(AnchorMaxStep, Vector3.Distance(LastAnchor, position));
                }

                LastAnchor = position;
                HasLastAnchor = true;
                AnchorSamples++;
                AnchorSum += position;
                AnchorSumSquares += Vector3.Scale(position, position);
                ScaleSum += scale;
                ScaleSumSquares += scale * scale;
            }

            public void AddTarget(Vector3 center)
            {
                TargetSamples++;
                TargetSum += center;
                TargetSumSquares += Vector3.Scale(center, center);
            }

            public float AnchorStdMagnitude()
            {
                return StdMagnitude(AnchorSum, AnchorSumSquares, AnchorSamples);
            }

            public float TargetStdMagnitude()
            {
                return StdMagnitude(TargetSum, TargetSumSquares, TargetSamples);
            }

            public float ScaleStd()
            {
                if (AnchorSamples <= 1)
                {
                    return 0f;
                }

                float mean = ScaleSum / AnchorSamples;
                float variance = Mathf.Max(0f, ScaleSumSquares / AnchorSamples - mean * mean);
                return Mathf.Sqrt(variance);
            }

            private static float StdMagnitude(Vector3 sum, Vector3 sumSquares, int count)
            {
                if (count <= 1)
                {
                    return 0f;
                }

                Vector3 mean = sum / count;
                Vector3 variance = sumSquares / count - Vector3.Scale(mean, mean);
                variance.x = Mathf.Max(0f, variance.x);
                variance.y = Mathf.Max(0f, variance.y);
                variance.z = Mathf.Max(0f, variance.z);
                return Mathf.Sqrt(variance.x + variance.y + variance.z);
            }
        }
    }
}
