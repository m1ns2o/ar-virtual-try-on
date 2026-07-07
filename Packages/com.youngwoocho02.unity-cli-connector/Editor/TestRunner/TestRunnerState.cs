using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityCliConnector.TestRunner
{
    /// <summary>
    /// Survives domain reloads via [InitializeOnLoad].
    /// Re-registers TestRunnerApi callbacks after PlayMode domain reload
    /// so RunFinished still fires and results are written to file.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunnerState
    {
        static TestRunnerState()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        public static void MarkPending(string runId, string filter)
        {
            var pending = new { runId, filter = filter ?? "" };
            try
            {
                Directory.CreateDirectory(RunTests.StatusDir);
                File.WriteAllText(PendingFilePath(runId), JsonConvert.SerializeObject(pending));
            }
            catch { }
        }

        public static void ClearPending(string runId)
        {
            try
            {
                var path = PendingFilePath(runId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        static void OnAfterAssemblyReload()
        {
            try
            {
                Directory.CreateDirectory(RunTests.StatusDir);
                foreach (var file in Directory.GetFiles(RunTests.StatusDir, "test-pending-*.json"))
                {
                    var json = File.ReadAllText(file);
                    var pending = JObject.Parse(json);
                    var runId  = pending["runId"]?.Value<string>();
                    var filter = pending["filter"]?.Value<string>();

                    if (string.IsNullOrEmpty(runId)) continue;

                    ReattachCallbacks(runId, filter);
                }
            }
            catch { }
        }

        static void ReattachCallbacks(string runId, string filter)
        {
            var passed  = new List<string>();
            var failed  = new List<string>();
            var skipped = new List<string>();

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new RunTests.TestCallbacks(
                onResult: r => RunTests.CollectResult(r, passed, failed, skipped),
                onFinished: _ =>
                {
                    Object.DestroyImmediate(api);
                    ClearPending(runId);
                    RunTests.WriteResultsFile(runId, passed, failed, skipped);
                }
            );

            api.RegisterCallbacks(callbacks);
        }

        static string PendingFilePath(string runId) =>
            Path.Combine(RunTests.StatusDir, $"test-pending-{runId}.json");
    }
}
