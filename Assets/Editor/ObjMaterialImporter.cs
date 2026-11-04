using System;
using System.Collections.Generic;
using System.IO;
using MotorAndSpeed.World;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 匯入 OBJ 時改用 URP Lit 材質（避免變成粉紅色），並依同資料夾的 palette.mtl 設定顏色、光滑度、金屬度與自發光。
    /// Unity 讀 MTL 時不一定提供 Ns／Pm／Ke，所以自己解析；palette.mtl 改了會自動重新匯入。
    /// </summary>
    public class ObjMaterialImporter : AssetPostprocessor
    {
        private const string URP_LIT_SHADER = "Universal Render Pipeline/Lit";
        private const string PALETTE_FILE = "palette.mtl";

        public override uint GetVersion()
        {
            return 2;
        }

        public override int GetPostprocessOrder()
        {
            return 100;
        }

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
        {
            if (!assetPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                return;

            Shader lit = Shader.Find(URP_LIT_SHADER);
            if (lit == null)
                return;

            material.shader = lit;
            material.SetFloat("_Smoothness", MtlPalette.DEFAULT_SMOOTHNESS);

            string palettePath = Path.Combine(Path.GetDirectoryName(assetPath) ?? string.Empty, PALETTE_FILE).Replace('\\', '/');
            if (File.Exists(palettePath))
            {
                context.DependsOnSourceAsset(palettePath);
                Dictionary<string, MtlPalette.Entry> palette = MtlPalette.Parse(File.ReadAllText(palettePath));
                if (palette.TryGetValue(description.materialName, out MtlPalette.Entry entry))
                {
                    Apply(material, entry);

                    return;
                }
            }

            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
            {
                material.SetColor("_BaseColor", new Color(diffuse.x, diffuse.y, diffuse.z, 1f));

                return;
            }

            Debug.LogWarning($"[ObjMaterialImporter] {assetPath} 的材質 {material.name} 找不到 DiffuseColor");
        }

        private static void Apply(Material material, MtlPalette.Entry entry)
        {
            material.SetColor("_BaseColor", entry.BaseColor);
            material.SetFloat("_Smoothness", entry.Smoothness);
            material.SetFloat("_Metallic", entry.Metallic);
            if (entry.Emission.maxColorComponent <= 0f)
                return;

            // 車燈、霓虹、LED 自發光，開了後製的泛光（Bloom）會亮起來
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", entry.Emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
    }
}
