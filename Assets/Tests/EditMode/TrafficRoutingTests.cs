using System.Collections.Generic;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class TrafficRoutingTests
    {
        // 路口 0 是十字路口：路段 0（從西邊來）、1（往東）、2（往北，巷弄）、3（往南，單行道只能開進路口）
        private WorldData _data;

        [SetUp]
        public void SetUp()
        {
            _data = new WorldData
            {
                junctions = new List<JunctionData>
                {
                    new JunctionData { id = 0, edges = new List<int> { 0, 1, 2, 3 } },
                    new JunctionData { id = 1, edges = new List<int> { 0 } },
                    new JunctionData { id = 2, edges = new List<int> { 1 } },
                    new JunctionData { id = 3, edges = new List<int> { 2 } },
                    new JunctionData { id = 4, edges = new List<int> { 3 } },
                },
                edges = new List<EdgeData>
                {
                    new EdgeData { id = 0, start = 1, end = 0, main = true },
                    new EdgeData { id = 1, start = 0, end = 2, main = true },
                    new EdgeData { id = 2, start = 0, end = 3, main = false },
                    new EdgeData { id = 3, start = 4, end = 0, main = true, oneway = 1 },
                },
            };
        }

        [Test]
        public void ChooseNextEdge_MainOnly_NeverUTurnsOrEntersOneWayAgainstTraffic()
        {
            System.Random random = new System.Random(1);
            for (int i = 0; i < 50; i++)
            {
                int next = TrafficRouting.ChooseNextEdge(_data, 0, 0, true, random, out bool forward);

                // 只剩往東的主要道路可走：巷弄不能走、單行道是逆向
                Assert.AreEqual(1, next);
                Assert.IsTrue(forward);
            }
        }

        [Test]
        public void ChooseNextEdge_AllRoads_CanTurnIntoLane()
        {
            System.Random random = new System.Random(2);
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < 100; i++)
            {
                seen.Add(TrafficRouting.ChooseNextEdge(_data, 0, 0, false, random, out bool _));
            }

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, seen);
        }

        [Test]
        public void ChooseNextEdge_DeadEnd_UTurnsOnSameEdge()
        {
            int next = TrafficRouting.ChooseNextEdge(_data, 2, 1, true, new System.Random(3), out bool forward);

            Assert.AreEqual(1, next);
            Assert.IsFalse(forward, "從東邊盡頭迴轉後要往 start（西）方向開");
        }

        [Test]
        public void CanEnter_RespectsOneWay()
        {
            Assert.IsTrue(TrafficRouting.CanEnter(_data.edges[3], 4));
            Assert.IsFalse(TrafficRouting.CanEnter(_data.edges[3], 0));
            Assert.IsTrue(TrafficRouting.CanEnter(_data.edges[1], 2));
        }

        [Test]
        public void Bezier_EndpointsAndMidpoint()
        {
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 c = new Vector3(10f, 0f, 0f);
            Vector3 b = new Vector3(10f, 0f, 10f);

            Assert.AreEqual(a, TrafficRouting.Bezier(a, c, b, 0f));
            Assert.AreEqual(b, TrafficRouting.Bezier(a, c, b, 1f));
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(7.5f, 0f, 2.5f), TrafficRouting.Bezier(a, c, b, 0.5f)), 0.001f);
        }

        [Test]
        public void BezierLength_StraightLine_EqualsDistance_CurveIsLonger()
        {
            Assert.AreEqual(20f, TrafficRouting.BezierLength(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f)), 0.01f);

            float curve = TrafficRouting.BezierLength(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f));
            Assert.Greater(curve, Mathf.Sqrt(200f));
            Assert.Less(curve, 20f);
        }
    }
}
