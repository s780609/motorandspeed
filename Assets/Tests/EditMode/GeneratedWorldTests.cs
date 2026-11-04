using System.Collections.Generic;
using System.IO;
using System.Linq;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 產生出來的世界資料大小預算：影響網頁版下載量與 GitHub 單檔上限（場景檔超過 100MB 會被拒絕推送）。
    /// </summary>
    public class GeneratedWorldTests
    {
        private const string GENERATED_FOLDER = "Assets/Generated/WuguWorld";
        private const string WORLD_SCENE = "Assets/Scenes/WuguWorld.unity";
        private const string COLLIDER_BUNDLE = "Assets/Generated/WuguWorld/Colliders.asset";
        private const long MB = 1024L * 1024L;

        [Test]
        public void GeneratedWorld_FileSizeWithinBudget()
        {
            long total = Directory.GetFiles(GENERATED_FOLDER, "*.asset").Sum(file => new FileInfo(file).Length);

            Assert.LessOrEqual(total, 130L * MB, $"Assets/Generated 共 {total / MB} MB");
        }

        [Test]
        public void WorldScene_FileSizeWithinBudget()
        {
            long size = new FileInfo(WORLD_SCENE).Length;

            Assert.LessOrEqual(size, 50L * MB, $"WuguWorld.unity 共 {size / MB} MB");
        }

        [Test]
        public void RenderMeshes_AreNotReadable()
        {
            // 不可讀的網格在執行期只留 GPU 那一份，網頁版記憶體省一半
            foreach (Mesh mesh in BundleMeshes().Where(m => m.Path != COLLIDER_BUNDLE).Select(m => m.Mesh))
            {
                Assert.IsFalse(mesh.isReadable, $"{mesh.name} 要設成不可讀");
            }
        }

        [Test]
        public void ColliderMeshes_StayReadable()
        {
            // MeshCollider 在執行期要讀網格資料，不能設成不可讀
            List<Mesh> colliders = BundleMeshes().Where(m => m.Path == COLLIDER_BUNDLE).Select(m => m.Mesh).ToList();
            Assert.Greater(colliders.Count, 0);
            foreach (Mesh mesh in colliders)
            {
                Assert.IsTrue(mesh.isReadable, $"{mesh.name} 要保持可讀");
            }
        }

        [Test]
        public void SmallMeshes_Use16BitIndex()
        {
            foreach (Mesh mesh in BundleMeshes().Select(m => m.Mesh).Where(m => m.vertexCount < 65535))
            {
                Assert.AreEqual(IndexFormat.UInt16, mesh.indexFormat, $"{mesh.name} 只有 {mesh.vertexCount} 個頂點，用 16 位元索引就夠");
            }
        }

        private static IEnumerable<(string Path, Mesh Mesh)> BundleMeshes()
        {
            foreach (string path in Directory.GetFiles(GENERATED_FOLDER, "*.asset").Select(p => p.Replace('\\', '/')))
            {
                MeshBundle bundle = AssetDatabase.LoadAssetAtPath<MeshBundle>(path);
                if (bundle == null)
                    continue;

                foreach (Mesh mesh in bundle.Meshes)
                {
                    yield return (path, mesh);
                }
            }
        }
    }
}
