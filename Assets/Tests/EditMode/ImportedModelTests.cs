using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// OBJ 匯入後的材質：依 palette.mtl 設定光滑度、金屬度與自發光，換色用的材質名稱不能消失。
    /// </summary>
    public class ImportedModelTests
    {
        private const string URP_LIT = "Universal Render Pipeline/Lit";

        [Test]
        public void ScooterLights_AreEmissive()
        {
            foreach (string name in new[] { "headlight", "taillight" })
            {
                Material material = Load("scooter_body", name);
                Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), $"{name} 要自發光");
                Assert.Greater(material.GetColor("_EmissionColor").maxColorComponent, 0.5f, $"{name} 的自發光太暗");
            }
        }

        [Test]
        public void Rim_IsMetallic_AndPaintIsShinierThanTire()
        {
            Assert.GreaterOrEqual(Load("scooter_wheel", "rim").GetFloat("_Metallic"), 0.6f);
            Assert.Greater(Load("scooter_body", "body").GetFloat("_Smoothness"), Load("scooter_wheel", "tire").GetFloat("_Smoothness"));
            Assert.LessOrEqual(Load("scooter_body", "body").GetFloat("_Metallic"), 0.01f);
        }

        [Test]
        public void AllModelMaterials_UseUrpLit()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Material material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    Assert.AreEqual(URP_LIT, material.shader.name, $"{path} 的 {material.name}");
                }
            }
        }

        [Test]
        public void RecolorMaterials_StillExist()
        {
            Assert.IsNotNull(Load("npc_rider", "rider_jacket"));
            Assert.IsNotNull(Load("npc_rider", "rider_helmet"));
            Assert.IsNotNull(Load("scooter_parked", "body"));
            Assert.IsNotNull(Load("car", "car_paint"));
        }

        private static Material Load(string model, string material)
        {
            Material found = AssetDatabase.LoadAllAssetsAtPath($"Assets/Models/{model}.obj").OfType<Material>().FirstOrDefault(m => m.name == material);
            Assert.IsNotNull(found, $"{model}.obj 沒有材質 {material}");

            return found;
        }
    }
}
