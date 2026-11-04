using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// NPC 車輛與 AI 選手在遠處（霧裡）要被剔除，減少網頁版的 draw call。
    /// </summary>
    public class NpcPrefabTests
    {
        [Test]
        public void NpcAndRacerPrefabs_HaveCullingLod()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Npc", "Assets/Prefabs/Racers" });
            Assert.Greater(guids.Length, 0);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                LODGroup group = prefab.GetComponent<LODGroup>();
                Assert.IsNotNull(group, $"{path} 沒有 LODGroup");

                LOD[] lods = group.GetLODs();
                Assert.AreEqual(1, lods.Length, path);
                Assert.That(lods[0].screenRelativeTransitionHeight, Is.InRange(0.01f, 0.04f), path);
                Assert.AreEqual(prefab.GetComponentsInChildren<Renderer>(true).Length, lods[0].renderers.Count(r => r != null), $"{path} 有 Renderer 沒被 LOD 涵蓋");
            }
        }
    }
}
