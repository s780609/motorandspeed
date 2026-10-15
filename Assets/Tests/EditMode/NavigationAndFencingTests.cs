using System.Collections.Generic;
using MotorAndSpeed.Game;
using MotorAndSpeed.UI;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class RaceFencingTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp()
        {
            // 南北向的賽道穿過路口 0，東邊有一條支線通往路口 3；路口 4 在很遠的地方
            WorldData data = new WorldData
            {
                junctions = new List<JunctionData>
                {
                    new JunctionData { id = 0, x = 0f, z = 0f, edges = new List<int> { 0, 1, 2 } },
                    new JunctionData { id = 1, x = 0f, z = 100f, edges = new List<int> { 1 } },
                    new JunctionData { id = 2, x = 0f, z = -100f, edges = new List<int> { 0 } },
                    new JunctionData { id = 3, x = 50f, z = 0f, edges = new List<int> { 2, 3 } },
                    new JunctionData { id = 4, x = 500f, z = 0f, edges = new List<int> { 3 } },
                },
                edges = new List<EdgeData>
                {
                    Edge(0, 2, 0, 0f, -100f, 0f, 0f),
                    Edge(1, 0, 1, 0f, 0f, 0f, 100f),
                    Edge(2, 0, 3, 0f, 0f, 50f, 0f),
                    Edge(3, 3, 4, 50f, 0f, 500f, 0f),
                },
            };
            _network = new RoadNetwork(data);
        }

        [Test]
        public void Plan_FencesOnlyTheSideRoadAtTrackJunctions()
        {
            RoutePath track = new RoutePath(new[] { new Vector3(0f, 0f, -100f), new Vector3(0f, 0f, 100f) });

            List<FencePlacement> fences = RaceFencing.Plan(_network, track);

            Assert.AreEqual(1, fences.Count, "只有路口 0 往東的支線需要圍起來");
            Assert.AreEqual(0, fences[0].Junction);
            Assert.AreEqual(2, fences[0].Edge);
        }

        [Test]
        public void Plan_FenceSitsJustOutsideTheJunctionFacingIt()
        {
            RoutePath track = new RoutePath(new[] { new Vector3(0f, 0f, -100f), new Vector3(0f, 0f, 100f) });

            FencePlacement fence = RaceFencing.Plan(_network, track)[0];

            // 路口半徑 = 7 / 2 + 1 = 4.5，圍欄再往外 1 公尺
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(5.5f, 0f, 0f), fence.Position), 0.01f);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.right, fence.Outward), 0.01f);
            Assert.AreEqual(8f, fence.Width, 0.01f);
        }

        [Test]
        public void Plan_ArrowsPointAlongRaceDirection()
        {
            // 往北跑：站在路口面向東邊的圍欄，北方在左手邊
            RoutePath north = new RoutePath(new[] { new Vector3(0f, 0f, -100f), new Vector3(0f, 0f, 100f) });
            RoutePath south = new RoutePath(new[] { new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, -100f) });

            Assert.IsFalse(RaceFencing.Plan(_network, north)[0].ArrowsRight);
            Assert.IsTrue(RaceFencing.Plan(_network, south)[0].ArrowsRight);
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

    public class NavigationTests
    {
        // L 形路線：往北 50 公尺再往東 50 公尺
        private readonly Vector3[] _path = { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f), new Vector3(50f, 0f, 50f) };

        [Test]
        public void LookAhead_FromStart_WalksAlongThePath()
        {
            Vector3 point = Navigation.LookAheadPoint(_path, new Vector3(0f, 0f, 0f), 30f);

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 0f, 30f), point), 0.01f);
        }

        [Test]
        public void LookAhead_AroundTheCorner()
        {
            Vector3 point = Navigation.LookAheadPoint(_path, new Vector3(1f, 0f, 40f), 30f);

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(20f, 0f, 50f), point), 0.01f);
        }

        [Test]
        public void LookAhead_PastTheEnd_StopsAtLastPoint()
        {
            Vector3 point = Navigation.LookAheadPoint(_path, new Vector3(45f, 0f, 50f), 30f);

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(50f, 0f, 50f), point), 0.01f);
        }
    }
}
