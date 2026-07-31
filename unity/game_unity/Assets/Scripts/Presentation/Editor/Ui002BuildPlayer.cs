using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VictoriaGame.Presentation.Editor
{
    /// <summary>
    /// Construit le player Windows Development ui_002 (preuve visuelle standalone).
    /// Batch : -executeMethod VictoriaGame.Presentation.Editor.Ui002BuildPlayer.BuildFromCommandLine
    /// </summary>
    public static class Ui002BuildPlayer
    {
        const string OutputDir = "Builds/ui_002";
        const string ExeName = "VictoriaGame.exe";

        [MenuItem("Victoria/ui_002 Build Standalone Capture Player")]
        public static void BuildFromMenu()
        {
            var code = Build();
            Debug.Log(code == 0
                ? "Ui002BuildPlayer: DONE"
                : "Ui002BuildPlayer: FAILED exit=" + code);
        }

        public static void BuildFromCommandLine()
        {
            var code = 1;
            try
            {
                code = Build();
            }
            catch (Exception ex)
            {
                Debug.LogError("Ui002BuildPlayer FAILED: " + ex);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static int Build()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outDir = Path.Combine(projectRoot, OutputDir);
            Directory.CreateDirectory(outDir);
            var exePath = Path.Combine(outDir, ExeName);

            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError("Ui002BuildPlayer: aucune scène dans EditorBuildSettings");
                return 2;
            }

            var scenePaths = new string[scenes.Length];
            var enabled = 0;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled)
                    continue;
                scenePaths[enabled++] = scenes[i].path;
            }

            if (enabled == 0)
            {
                Debug.LogError("Ui002BuildPlayer: aucune scène enabled");
                return 3;
            }

            Array.Resize(ref scenePaths, enabled);

            var prevMode = PlayerSettings.fullScreenMode;
            var prevW = PlayerSettings.defaultScreenWidth;
            var prevH = PlayerSettings.defaultScreenHeight;
            var prevBg = PlayerSettings.runInBackground;

            try
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = GameViewCapture.Width;
                PlayerSettings.defaultScreenHeight = GameViewCapture.Height;
                PlayerSettings.runInBackground = true;

                var options = new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                };

                Debug.Log($"Ui002BuildPlayer: building → {exePath}");
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError("Ui002BuildPlayer: BuildResult=" + report.summary.result);
                    return 4;
                }

                Debug.Log(
                    $"Ui002BuildPlayer: OK size={report.summary.totalSize} " +
                    $"time={report.summary.totalTime} → {exePath}");
                return 0;
            }
            finally
            {
                PlayerSettings.fullScreenMode = prevMode;
                PlayerSettings.defaultScreenWidth = prevW;
                PlayerSettings.defaultScreenHeight = prevH;
                PlayerSettings.runInBackground = prevBg;
            }
        }
    }
}
