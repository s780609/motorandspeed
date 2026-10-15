using System.Collections.Generic;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class RoadNetworkTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp()
        {
            // 邊長 100 公尺的正方形加一條對角線，另外有一個沒接任何路的路口 4
            WorldData data = new WorldData
            {
                junctions = new List<JunctionData>
                {
                    Junction(0, 0f, 0f),
                    Junction(1, 100f, 0f),
                    Junction(2, 100f, 100f),
                    Junction(3, 0f, 100f),
                    Junction(4, 500f, 500f),
                },
                edges = new List<EdgeData>
                {
                    Edge(0, 0, 1, 0f, 0f, 100f, 0f),
                    Edge(1, 1, 2, 100f, 0f, 100f, 100f),
                    Edge(2, 2, 3, 100f, 100f, 0f, 100f),
                    Edge(3, 3, 0, 0f, 100f, 0f, 0f),
                    Edge(4, 0, 2, 0f, 0f, 100f, 100f),
                },
            };
            _network = new RoadNetwork(data);
        }

        [Test]
        public void FindPath_PrefersShorterDiagonal()
        {
            List<int> path = _network.FindPath(0, 2);

            CollectionAssert.AreEqual(new[] { 4 }, path);
        }

        [Test]
        public void FindPath_AroundTheSquare_TwoHundredMeters()
        {
            List<int> path = _network.FindPath(1, 3);
            float length = 0f;
            foreach (int edge in path)
            {
                length += _network.GetEdgePath(edge).Length;
            }

            Assert.AreEqual(200f, length, 0.01f);
        }

        [Test]
        public void FindPath_SameJunction_EmptyAndUnreachable_Null()
        {
            Assert.AreEqual(0, _network.FindPath(2, 2).Count);
            Assert.IsNull(_network.FindPath(0, 4));
        }

        [Test]
        public void ChainPoints_FollowsTravelDirectionWithoutDuplicates()
        {
            List<Vector3> points = _network.ChainPoints(1, new[] { 0, 3 });

            Assert.AreEqual(3, points.Count);
            Assert.AreEqual(new Vector3(100f, 0f, 0f), points[0]);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), points[1]);
            Assert.AreEqual(new Vector3(0f, 0f, 100f), points[2]);
        }

        [Test]
        public void FindNearestJunction_AndOtherEnd()
        {
            Assert.AreEqual(1, _network.FindNearestJunction(new Vector3(90f, 0f, 5f)));
            Assert.AreEqual(1, _network.OtherEnd(0, 0));
            Assert.AreEqual(0, _network.OtherEnd(0, 1));
            Assert.AreEqual(141.42f, _network.GetEdgePath(4).Length, 0.01f);
        }

        private static JunctionData Junction(int id, float x, float z)
        {
            return new JunctionData { id = id, x = x, z = z, edges = new List<int>() };
        }

        private static EdgeData Edge(int id, int start, int end, float x0, float z0, float x1, float z1)
        {
            return new EdgeData
            {
                id = id,
                start = start,
                end = end,
                width = 7f,
                points = new List<RoutePoint> { new RoutePoint { x = x0, z = z0 }, new RoutePoint { x = x1, z = z1 } },
            };
        }
    }

    public class OccupancyGridTests
    {
        private OccupancyGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new OccupancyGrid(Vector2.zero, new Vector2(100f, 100f), 1f);
        }

        [Test]
        public void MarkedRectangle_OverlapsAreNotFree()
        {
            _grid.Mark(new Vector2(50f, 50f), Vector2.right, 10f, 5f);

            Assert.IsFalse(_grid.IsFree(new Vector2(50f, 50f), Vector2.right, 2f, 2f));
            Assert.IsFalse(_grid.IsFree(new Vector2(62f, 50f), Vector2.right, 3f, 3f));
            Assert.IsTrue(_grid.IsFree(new Vector2(50f, 70f), Vector2.right, 5f, 5f));
        }

        [Test]
        public void RotatedRectangle_OnlyCoversTheDiagonalStrip()
        {
            _grid.Mark(new Vector2(20f, 20f), new Vector2(1f, 1f).normalized, 10f, 1f);

            Assert.IsFalse(_grid.IsFree(new Vector2(20f, 20f), Vector2.right, 1f, 1f));
            Assert.IsTrue(_grid.IsFree(new Vector2(27f, 13f), Vector2.right, 1f, 1f));
        }

        [Test]
        public void Circle_MarksItsArea()
        {
            _grid.MarkCircle(new Vector2(80f, 80f), 5f);

            Assert.IsFalse(_grid.IsFree(new Vector2(80f, 80f), Vector2.right, 1f, 1f));
            Assert.IsTrue(_grid.IsFree(new Vector2(80f, 88f), Vector2.right, 1f, 1f));
        }

        [Test]
        public void OutsideTheGrid_IsNotFree()
        {
            Assert.IsFalse(_grid.IsFree(new Vector2(-5f, 50f), Vector2.right, 2f, 2f));
        }
    }

    public class PolygonTests
    {
        [Test]
        public void Square_ContainsInsideOnly()
        {
            Vector2[] square = { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f), new Vector2(0f, 10f) };

            Assert.IsTrue(Polygon.Contains(square, new Vector2(5f, 5f)));
            Assert.IsFalse(Polygon.Contains(square, new Vector2(15f, 5f)));
        }

        [Test]
        public void LShape_NotchIsOutside()
        {
            Vector2[] shape =
            {
                new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 4f),
                new Vector2(4f, 4f), new Vector2(4f, 10f), new Vector2(0f, 10f),
            };

            Assert.IsTrue(Polygon.Contains(shape, new Vector2(2f, 8f)));
            Assert.IsFalse(Polygon.Contains(shape, new Vector2(8f, 8f)));
        }
    }

    public class SignalPhasingTests
    {
        [Test]
        public void FourWayCross_OppositeApproachesShareAPhase()
        {
            int[] phases = SignalPhasing.Group(new[] { new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(0f, -1f) });

            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, phases);
        }

        [Test]
        public void TJunction_SideRoadIsTheOtherPhase()
        {
            int[] phases = SignalPhasing.Group(new[] { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f) });

            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, phases);
        }

        [Test]
        public void SkewedCross_GroupsByNearestAxis()
        {
            int[] phases = SignalPhasing.Group(new[] { new Vector2(1f, 0.1f), new Vector2(-1f, 0.05f), new Vector2(0.1f, 1f), new Vector2(-0.05f, -1f) });

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, phases);
        }
    }
}
