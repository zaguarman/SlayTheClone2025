#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// The browser (WebGL) build (the user, 2026-10-17: "build the game to run in the browser", to try the changes while the
    /// editor is busy or before a long AI run). Tools/web_build.ps1 runs it from a separate copy of the project (a git
    /// worktree), in a Unity without a window, so the editor in use stays free:
    ///   Unity -batchmode -quit -projectPath <copy> -buildTarget WebGL -executeMethod SlayTheClone.WebBuild.Build -out <folder>
    /// Uncompressed, so any static file server can serve it (Tools/web_build.ps1 starts one).
    /// </summary>
    public static class WebBuild {
        [MenuItem("Debug/Build for the Browser (WebGL)")]
        public static void BuildFromMenu() => Build(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "WebGL"));

        /// <summary>
        /// The first run in the copy: the project uses URP's Compatibility Mode (Render Graph off), which Unity 6.3 only builds with
        /// URP_COMPATIBILITY_MODE defined; it's set for WebGL here, and the next run compiles with it (the copy's settings only).
        /// </summary>
        public static void Prepare() {
            var target = UnityEditor.Build.NamedBuildTarget.WebGL;
            var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0).ToList();
            if (!defines.Contains("URP_COMPATIBILITY_MODE")) {
                defines.Add("URP_COMPATIBILITY_MODE");
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
                AssetDatabase.SaveAssets();
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>The batch-mode entry point: the output folder comes after -out.</summary>
        public static void Build() {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-out");
            string output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "WebGL");
            bool ok = Build(output);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(string output) {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // Served as plain files, no headers needed
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log($"Web build: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss}, in {output}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
#endif
