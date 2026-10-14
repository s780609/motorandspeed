using System;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 匯入 OBJ 時，把 MTL 的顏色（Kd）套到 URP Lit 材質，避免材質變成粉紅色。
    /// </summary>
    public class ObjMaterialImporter : AssetPostprocessor
    {
        private const string URP_LIT_SHADER = "Universal Render Pipeline/Lit";

        public override uint GetVersion()
        {
            return 1;
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
            material.SetFloat("_Smoothness", 0.15f);

            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
            {
                material.SetColor("_BaseColor", new Color(diffuse.x, diffuse.y, diffuse.z, 1f));

                return;
            }

            Debug.LogWarning($"[ObjMaterialImporter] {assetPath} 的材質 {material.name} 找不到 DiffuseColor");
        }
    }
}
