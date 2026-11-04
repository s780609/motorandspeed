using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 打包 WebGL 網頁版到 Builds/WebGL，並在 Builds/webgl_size_report.txt 寫出大小報告。
    /// 設定以 GitHub Pages 為目標：Pages 不送 Content-Encoding，所以用 Gzip 加 decompression fallback（由 loader 用 JS 解壓）。
    /// 選單：MotorAndSpeed / 打包 WebGL 網頁版；batchmode 要帶 -buildTarget WebGL，加 -development 可打開發版。
    /// </summary>
    internal static class WebGLBuild
    {
        private const string OUTPUT_PATH = "Builds/WebGL";
        private const string REPORT_PATH = "Builds/webgl_size_report.txt";
        // GitHub Pages 單檔上限 100MB，留一點餘裕
        private const long MAX_FILE_BYTES = 90L * 1024 * 1024;

        [MenuItem("MotorAndSpeed/打包 WebGL 網頁版")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("作用中平台不是 WebGL：batchmode 請加 -buildTarget WebGL，編輯器請先在 Build Profiles 切換到 Web");

            bool development = Environment.GetCommandLineArgs().Contains("-development");
            PlayerSettings.productName = "MotorAndSpeed";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            // 檔名帶雜湊：Pages 的 CDN 會快取舊檔，換檔名才不會抓到舊版
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.showDiagnostics = development;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);

            if (Directory.Exists(OUTPUT_PATH))
                Directory.Delete(OUTPUT_PATH, true);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = WindowsBuild.Scenes,
                locationPathName = OUTPUT_PATH,
                target = BuildTarget.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"打包失敗：{summary.result}，錯誤 {summary.totalErrors} 個");

            long largest = WriteSizeReport(report);
            if (largest > MAX_FILE_BYTES)
                throw new InvalidOperationException($"有檔案超過 {MAX_FILE_BYTES / (1024 * 1024)} MB（{largest / (1024 * 1024)} MB），放不上 GitHub Pages，詳見 {REPORT_PATH}");

            Debug.Log($"[WebGLBuild] 完成：{OUTPUT_PATH}，耗時 {summary.totalTime.TotalSeconds:F0} 秒，報告 {REPORT_PATH}");
        }

        /// <summary>
        /// 寫出輸出檔大小與最大的 30 個資源，回傳最大單檔的大小。
        /// </summary>
        private static long WriteSizeReport(BuildReport report)
        {
            StringBuilder text = new StringBuilder();
            long largest = 0;
            long total = 0;
            text.AppendLine("== 輸出檔案（壓縮後） ==");
            foreach (string file in Directory.GetFiles(OUTPUT_PATH, "*", SearchOption.AllDirectories).OrderByDescending(f => new FileInfo(f).Length))
            {
                long length = new FileInfo(file).Length;
                largest = Math.Max(largest, length);
                total += length;
                text.AppendLine($"{length / 1024.0 / 1024.0,8:F2} MB  {file.Replace('\\', '/')}");
            }

            text.AppendLine($"{total / 1024.0 / 1024.0,8:F2} MB  合計");
            text.AppendLine();
            text.AppendLine("== 最大的 30 個資源（未壓縮） ==");
            Dictionary<string, ulong> assets = new Dictionary<string, ulong>();
            foreach (PackedAssets packed in report.packedAssets)
            {
                foreach (PackedAssetInfo info in packed.contents)
                {
                    string path = string.IsNullOrEmpty(info.sourceAssetPath) ? "(內建)" : info.sourceAssetPath;
                    assets.TryGetValue(path, out ulong size);
                    assets[path] = size + info.packedSize;
                }
            }

            foreach (KeyValuePair<string, ulong> asset in assets.OrderByDescending(a => a.Value).Take(30))
            {
                text.AppendLine($"{asset.Value / 1024.0 / 1024.0,8:F2} MB  {asset.Key}");
            }

            File.WriteAllText(REPORT_PATH, text.ToString(), Encoding.UTF8);
            Debug.Log(text.ToString());

            return largest;
        }
    }
}
