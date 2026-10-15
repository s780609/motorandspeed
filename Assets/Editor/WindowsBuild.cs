using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 打包 Windows 執行檔（64 位元）到 Builds/Windows。
    /// 選單：MotorAndSpeed / 打包 Windows EXE
    /// </summary>
    internal static class WindowsBuild
    {
        private const string OUTPUT_PATH = "Builds/Windows/MotorAndSpeed.exe";

        // 第一個場景是遊戲啟動時載入的場景
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/WuguWorld.unity",
            "Assets/Scenes/Prototype.unity",
        };

        /// <summary>
        /// 設定視窗模式並打包，失敗時丟出例外（批次模式下會回傳非 0 結束碼）。
        /// </summary>
        [MenuItem("MotorAndSpeed/打包 Windows EXE")]
        public static void Build()
        {
            PlayerSettings.productName = "MotorAndSpeed";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;

            EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[Scenes.Length];
            for (int i = 0; i < Scenes.Length; i++)
            {
                buildScenes[i] = new EditorBuildSettingsScene(Scenes[i], true);
            }

            EditorBuildSettings.scenes = buildScenes;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = OUTPUT_PATH,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"打包失敗：{summary.result}，錯誤 {summary.totalErrors} 個");

            Debug.Log($"[WindowsBuild] 完成：{OUTPUT_PATH}，{summary.totalSize / (1024 * 1024)} MB，耗時 {summary.totalTime.TotalSeconds:F0} 秒");
        }
    }
}
