using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 解析 tools/generate_models.py 輸出的 MTL 材質檔：顏色（Kd）、光滑度（Ns ÷ 1000）、金屬度（Pm）、自發光（Ke）。
    /// 匯入 OBJ 時由 ObjMaterialImporter 套用到 URP Lit 材質。
    /// </summary>
    public static class MtlPalette
    {
        /// <summary>MTL 沒寫 Ns 時的光滑度。</summary>
        public const float DEFAULT_SMOOTHNESS = 0.15f;

        /// <summary>一種材質的參數。</summary>
        public struct Entry
        {
            public Color BaseColor;
            public float Smoothness;
            public float Metallic;
            public Color Emission;
        }

        /// <summary>
        /// 解析整份 MTL，回傳 材質名稱 → 參數。
        /// </summary>
        public static Dictionary<string, Entry> Parse(string text)
        {
            Dictionary<string, Entry> palette = new Dictionary<string, Entry>();
            string current = null;
            Entry entry = default;
            foreach (string rawLine in text.Split('\n'))
            {
                string[] parts = rawLine.Trim().Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || parts[0].StartsWith("#"))
                    continue;

                if (parts[0] == "newmtl" && parts.Length > 1)
                {
                    if (current != null)
                        palette[current] = entry;

                    current = parts[1];
                    entry = new Entry { BaseColor = Color.white, Smoothness = DEFAULT_SMOOTHNESS, Metallic = 0f, Emission = Color.black };
                    continue;
                }

                if (current == null)
                    continue;

                switch (parts[0])
                {
                    case "Kd" when parts.Length >= 4:
                        entry.BaseColor = ReadColor(parts);
                        break;
                    case "Ke" when parts.Length >= 4:
                        entry.Emission = ReadColor(parts);
                        break;
                    case "Ns" when parts.Length >= 2:
                        entry.Smoothness = Mathf.Clamp01(ReadFloat(parts[1]) / 1000f);
                        break;
                    case "Pm" when parts.Length >= 2:
                        entry.Metallic = Mathf.Clamp01(ReadFloat(parts[1]));
                        break;
                }
            }

            if (current != null)
                palette[current] = entry;

            return palette;
        }

        private static Color ReadColor(string[] parts)
        {
            return new Color(ReadFloat(parts[1]), ReadFloat(parts[2]), ReadFloat(parts[3]), 1f);
        }

        private static float ReadFloat(string text)
        {
            return float.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
