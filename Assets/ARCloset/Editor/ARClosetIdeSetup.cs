using System;
using System.IO;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace ARCloset.Editor
{
    public static class ARClosetIdeSetup
    {
        private const string RiderPath =
            @"C:\Program Files\JetBrains\JetBrains Rider 2025.2\bin\rider64.exe";
        private const string ProjectGenerationFlagKey = "unity_project_generation_flag";

        [MenuItem("AR Closet/IDE/Use Rider and Regenerate Projects")]
        public static void ConfigureRiderMenu()
        {
            ConfigureRider();
        }

        public static void ConfigureRiderBatch()
        {
            ConfigureRider();
        }

        private static void ConfigureRider()
        {
            if (!File.Exists(RiderPath))
            {
                throw new FileNotFoundException("JetBrains Rider was not found.", RiderPath);
            }

            CodeEditor.SetExternalScriptEditor(RiderPath);
            EditorPrefs.SetInt(ProjectGenerationFlagKey, 0);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CodeEditor.CurrentEditor.SyncAll();

            Debug.Log($"AR Closet IDE configured: {CodeEditor.CurrentEditorInstallation}");
            Debug.Log("AR Closet IDE project generation configured for Rider and user scripts only.");

            foreach (var solutionPath in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.sln").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                Debug.Log($"AR Closet solution available: {solutionPath}");
            }
        }
    }
}
