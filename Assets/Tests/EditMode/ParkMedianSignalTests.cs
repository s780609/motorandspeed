using System.Collections.Generic;
using MotorAndSpeed.UI;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class PolygonTriangulateTests
    {
        [Test]
        public void Triangulate_Square_TwoTriangles()
        {
            Vector2[] square = { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f), new Vector2(0f, 10f) };

            List<int> triangles = Polygon.Triangulate(square);

            Assert.AreEqual(6, triangles.Count);
            Assert.AreEqual(100f, TriangleArea(square, triangles), 0.01f);
        }

        [Test]
        public void Triangulate_ConcaveShape_CoversExactArea()
        {
            // L 形，順時針與逆時針都要能處理
            Vector2[] shape = { new Vector2(0f, 0f), new Vector2(20f, 0f), new Vector2(20f, 10f), new Vector2(10f, 10f), new Vector2(10f, 20f), new Vector2(0f, 20f) };
            Vector2[] reversed = (Vector2[])shape.Clone();
            System.Array.Reverse(reversed);

            Assert.AreEqual(300f, TriangleArea(shape, Polygon.Triangulate(shape)), 0.01f);
            Assert.AreEqual(300f, TriangleArea(reversed, Polygon.Triangulate(reversed)), 0.01f);
        }

        private static float TriangleArea(IReadOnlyList<Vector2> points, List<int> triangles)
        {
            float area = 0f;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = points[triangles[i]];
                Vector2 b = points[triangles[i + 1]];
                Vector2 c = points[triangles[i + 2]];
                area += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) / 2f;
            }

            return area;
        }
    }

    public class OccupancyPolygonTests
    {
        [Test]
        public void MarkPolygon_OnlyInsideBecomesOccupied()
        {
            OccupancyGrid grid = new OccupancyGrid(Vector2.zero, new Vector2(100f, 100f), 1f);
            Vector2[] park = { new Vector2(20f, 20f), new Vector2(60f, 20f), new Vector2(60f, 60f), new Vector2(20f, 60f) };

            grid.MarkPolygon(park);

            Assert.IsFalse(grid.IsFree(new Vector2(40f, 40f), Vector2.right, 2f, 2f), "公園裡不能蓋房子");
            Assert.IsTrue(grid.IsFree(new Vector2(80f, 80f), Vector2.right, 2f, 2f), "公園外不受影響");
        }
    }

    public class MapFillTests
    {
        [Test]
        public void FillPolygon_ColorsInsideOnly()
        {
            Color32[] pixels = new Color32[20 * 20];
            Color32 green = new Color32(0, 200, 0, 255);
            Vector2[] square = { new Vector2(5f, 5f), new Vector2(15f, 5f), new Vector2(15f, 15f), new Vector2(5f, 15f) };

            MapTextureBuilder.FillPolygon(pixels, 20, square, green);

            Assert.AreEqual(green, pixels[10 * 20 + 10]);
            Assert.AreNotEqual(green, pixels[2 * 20 + 2]);
        }
    }

    public class SignalPlacementTests
    {
        [Test]
        public void PolePosition_CrossJunction_IsBesideTheRoad()
        {
            // 十字路口：南北、東西向各一條 10 公尺寬的雙向道路
            WorldData data = new WorldData
            {
                junctions = new List<JunctionData>
                {
                    new JunctionData { id = 0, x = 0f, z = 0f, edges = new List<int> { 0, 1, 2, 3 } },
                    new JunctionData { id = 1, x = 0f, z = 100f, edges = new List<int> { 0 } },
                    new JunctionData { id = 2, x = 0f, z = -100f, edges = new List<int> { 1 } },
                    new JunctionData { id = 3, x = 100f, z = 0f, edges = new List<int> { 2 } },
                    new JunctionData { id = 4, x = -100f, z = 0f, edges = new List<int> { 3 } },
                },
                edges = new List<EdgeData>
                {
                    Edge(0, 0, 1, 0f, 100f),
                    Edge(1, 0, 2, 0f, -100f),
                    Edge(2, 0, 3, 100f, 0f),
                    Edge(3, 0, 4, -100f, 0f),
                },
            };
            SignalPlacement placement = new SignalPlacement(data);

            Vector2 pole = placement.PolePosition(data.junctions[0], new Vector2(0f, 1f), 5f);

            Assert.IsFalse(placement.IsOnRoad(pole, 0.4f), $"燈桿 {pole} 不能立在路面上");
            Assert.Less(pole.magnitude, 20f, "燈桿要在路口旁邊");
        }

        [Test]
        public void AllSignalPolesInWugu_AreOffTheRoad()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/wugu_world.json");
            WorldData data = WorldData.FromJson(asset.text);
            RoadNetwork network = new RoadNetwork(data);
            SignalPlacement placement = new SignalPlacement(data);
            List<string> bad = new List<string>();
            int count = 0;
            foreach (JunctionData junction in data.junctions)
            {
                if (!junction.signal)
                    continue;

                Vector2 center = new Vector2(junction.x, junction.z);
                foreach (int edgeId in junction.edges)
                {
                    EdgeData edge = data.edges[edgeId];
                    RoutePath path = network.GetEdgePath(edgeId);
                    Vector3 outward = edge.start == junction.id ? path.GetTangent(0f) : -path.GetTangent(path.Length);
                    Vector2 pole = placement.PolePosition(junction, -new Vector2(outward.x, outward.z), edge.width / 2f);
                    count++;
                    if (placement.IsOnRoad(pole, 0.4f) || Vector2.Distance(pole, center) > placement.JunctionRadius(junction.id) + 15f)
                        bad.Add($"路口 {junction.id}（{edge.name}）燈桿 {pole} 在路面上={placement.IsOnRoad(pole, 0.4f)} 離路口={Vector2.Distance(pole, center):F1}");
                }
            }

            Assert.Greater(count, 100);
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        private static EdgeData Edge(int id, int start, int end, float x1, float z1)
        {
            return new EdgeData
            {
                id = id,
                start = start,
                end = end,
                width = 10f,
                points = new List<RoutePoint> { new RoutePoint { x = 0f, z = 0f }, new RoutePoint { x = x1, z = z1 } },
            };
        }
    }

    public class ParkMedianDataTests
    {
        private WorldData _data;

        [SetUp]
        public void SetUp()
        {
            _data = WorldData.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/wugu_world.json").text);
        }

        [Test]
        public void Parks_IncludeDisasterPreventionPark()
        {
            Assert.IsTrue(_data.parks.Exists(p => p.name == "防災體健公園"), "成德公園旁的防災體健公園要是綠地");
        }

        [Test]
        public void DividedRoads_HaveMediansAndDoNotOverlapMostly()
        {
            Assert.Greater(_data.medians.Count, 20, "雙向分隔道路要有分隔島");
            List<EdgeData> paired = _data.edges.FindAll(e => e.median != 0);
            Assert.Greater(paired.Count, 50);
            Assert.IsTrue(paired.TrueForAll(e => e.oneway != 0), "只有單行線（分隔道路的一半）會有分隔島");
            foreach (MedianData median in _data.medians)
            {
                Assert.AreEqual(median.points.Count, median.widths.Count);
            }
        }
    }
}
