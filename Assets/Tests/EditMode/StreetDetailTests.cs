using System.Collections.Generic;
using System.Linq;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 城市美化的純計算：位置種子、建築細節零件、屋頂不貼窗戶的方塊網格。
    /// </summary>
    public class StreetDetailTests
    {
        private static readonly DetailKind[] RoofKinds = { DetailKind.Parapet, DetailKind.WaterTank, DetailKind.SolarPanel, DetailKind.Shed, DetailKind.Antenna };
        private static readonly DetailKind[] WallKinds = { DetailKind.Cage, DetailKind.CageRoof, DetailKind.AirConditioner };

        [Test]
        public void LotSeed_SamePositionSameSeed_SaltChangesIt()
        {
            Assert.AreEqual(LotSeed.For(12.345f, -67.89f, 1), LotSeed.For(12.345f, -67.89f, 1));
            Assert.AreNotEqual(LotSeed.For(12.345f, -67.89f, 1), LotSeed.For(12.345f, -67.89f, 2));
            Assert.AreNotEqual(LotSeed.For(0f, 0f, 1), LotSeed.For(0.01f, 0f, 1));
        }

        [Test]
        public void LotSeed_IsWellDistributed()
        {
            double sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                sum += LotSeed.Random(i * 7.3f, i * -3.1f, 5).NextDouble();
            }

            Assert.That(sum / 1000, Is.InRange(0.45, 0.55));
        }

        [Test]
        public void MainShophouse_IsDeterministicAndWithinLimits()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                List<DetailPart> a = FacadeDetailPlan.MainShophouse(6f, 12f, -3f, 4f, 3.6f, 3.6f + 3 * 3.2f, new System.Random(seed));
                List<DetailPart> b = FacadeDetailPlan.MainShophouse(6f, 12f, -3f, 4f, 3.6f, 3.6f + 3 * 3.2f, new System.Random(seed));
                CollectionAssert.AreEqual(a.Select(p => p.Center).ToList(), b.Select(p => p.Center).ToList());
                Assert.LessOrEqual(a.Count, FacadeDetailPlan.MAX_MAIN_PARTS);
                AssertWithin(a, 6f, 12f, -3f, 4f, 3.6f + 3 * 3.2f, 1.0f);
            }
        }

        [Test]
        public void SimpleBuilding_IsWithinLimits()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                float height = (3 + seed % 10) * 3.1f;
                List<DetailPart> parts = FacadeDetailPlan.SimpleBuilding(2f, 9f, -4f, 4f, height, new System.Random(seed));
                Assert.LessOrEqual(parts.Count, FacadeDetailPlan.MAX_MINOR_PARTS);
                AssertWithin(parts, 2f, 9f, -4f, 4f, height, 1.0f);
            }
        }

        [Test]
        public void Tower_IsWithinLimits()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                List<DetailPart> parts = FacadeDetailPlan.Tower(10f, 13f, 28f, -10f, 10f, 60f, 5f, new System.Random(seed));
                Assert.LessOrEqual(parts.Count, FacadeDetailPlan.MAX_TOWER_PARTS);
                foreach (DetailPart part in parts)
                {
                    Assert.GreaterOrEqual(part.Center.x - part.Size.x / 2f, 10f - 1.6f, part.Kind.ToString());
                    Assert.LessOrEqual(part.Center.x + part.Size.x / 2f, 28f + 0.01f, part.Kind.ToString());
                    if (RoofKinds.Contains(part.Kind))
                        Assert.GreaterOrEqual(part.Center.y - part.Size.y / 2f, 60f - 0.01f, part.Kind.ToString());
                }
            }
        }

        [Test]
        public void TiledBox_PlainCaps_UseCapUv()
        {
            BoxMeshes.Tiled(new Vector3(12f, 9f, 6f), new Vector2(3f, 3.2f), true, out _, out Vector3[] normals, out Vector2[] uv, out _);
            for (int i = 0; i < uv.Length; i++)
            {
                if (Mathf.Abs(normals[i].y) > 0.5f)
                    Assert.AreEqual(BoxMeshes.CapUV, uv[i], "屋頂與底面不能貼窗戶");
            }

            Assert.Less(BoxMeshes.CapUV.y, 0.06f, "要取樣立面貼圖最下面的水泥色飾帶");

            // 側面照樣依公尺重複窗戶
            BoxMeshes.Tiled(new Vector3(12f, 9f, 6f), new Vector2(3f, 3.2f), true, out _, out normals, out uv, out _);
            Assert.IsTrue(uv.Where((_, i) => Mathf.Abs(normals[i].y) < 0.5f).Any(u => u.x > 1f));
        }

        private static void AssertWithin(List<DetailPart> parts, float facade, float depth, float z0, float z1, float roof, float frontReach)
        {
            foreach (DetailPart part in parts)
            {
                Vector3 min = part.Center - part.Size / 2f;
                Vector3 max = part.Center + part.Size / 2f;
                string name = part.Kind.ToString();
                Assert.GreaterOrEqual(min.x, facade - frontReach, $"{name} 伸出人行道");
                Assert.LessOrEqual(max.x, facade + depth + 0.5f, name);
                Assert.GreaterOrEqual(min.z, z0 - 0.01f, name);
                Assert.LessOrEqual(max.z, z1 + 0.01f, name);
                if (WallKinds.Contains(part.Kind))
                {
                    Assert.GreaterOrEqual(min.y, FacadeDetailPlan.MIN_WALL_DETAIL_HEIGHT, $"{name} 太低會擋到騎士");
                    Assert.LessOrEqual(max.y, roof + 0.01f, $"{name} 超出屋頂");
                }

                if (RoofKinds.Contains(part.Kind))
                    Assert.GreaterOrEqual(min.y, roof - 0.01f, $"{name} 要在屋頂上");
            }
        }
    }
}
