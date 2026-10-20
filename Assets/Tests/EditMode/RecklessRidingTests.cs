using MotorAndSpeed.Traffic;
using MotorAndSpeed.World;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    public class RecklessRidingTests
    {
        [Test]
        public void Weave_StaysWithinRange_AndSwingsAcrossLanes()
        {
            float min = 1f;
            float max = 0f;
            for (float t = 0f; t < 60f; t += 0.05f)
            {
                float weave = RecklessRiding.Weave(t, 0.7f);
                Assert.That(weave, Is.InRange(0f, 1f));
                min = UnityEngine.Mathf.Min(min, weave);
                max = UnityEngine.Mathf.Max(max, weave);
            }

            Assert.Less(min, 0.1f, "切西瓜要切回路邊");
            Assert.Greater(max, 0.9f, "切西瓜要切到靠中線");
        }

        [Test]
        public void Weave_DifferentPhases_AreNotInSync()
        {
            float difference = 0f;
            for (float t = 0f; t < 20f; t += 0.1f)
            {
                difference = UnityEngine.Mathf.Max(difference, UnityEngine.Mathf.Abs(RecklessRiding.Weave(t, 0f) - RecklessRiding.Weave(t, 2.5f)));
            }

            Assert.Greater(difference, 0.5f, "每台 8+9 要各切各的");
        }

        [Test]
        public void Surge_StaysInRange_AndBurstsNowAndThen()
        {
            int steps = 0;
            int calm = 0;
            float max = 0f;
            for (float t = 0f; t < 120f; t += 0.05f)
            {
                float surge = RecklessRiding.Surge(t, 1.3f);
                Assert.That(surge, Is.InRange(1f, RecklessRiding.MAX_SURGE));
                max = UnityEngine.Mathf.Max(max, surge);
                steps++;
                if (surge < 1.05f)
                    calm++;
            }

            Assert.Greater(max, 1.4f, "飆車時要暴衝");
            Assert.Greater(calm, steps / 4, "不是一直暴衝，要忽快忽慢");
        }

        [Test]
        public void WeaveOffset_TwoWayRoad_CutsFromScooterAreaToCenterLine()
        {
            EdgeData edge = new EdgeData { width = 14f, oneway = 0 };
            float lane = WorldTraffic.LaneOffset(edge, true);

            Assert.AreEqual(lane, RecklessRiding.WeaveOffset(edge, lane, 0f), 0.001f);
            Assert.AreEqual(RecklessRiding.TWO_WAY_INNER_OFFSET, RecklessRiding.WeaveOffset(edge, lane, 1f), 0.001f);
            Assert.Less(RecklessRiding.TWO_WAY_INNER_OFFSET, WorldTraffic.LaneOffset(edge, false), "要切進汽車道");
        }

        [Test]
        public void WeaveOffset_OneWayRoad_CutsIntoCarLane()
        {
            EdgeData edge = new EdgeData { width = 10f, oneway = 1 };
            float lane = WorldTraffic.LaneOffset(edge, true);

            Assert.AreEqual(WorldTraffic.LaneOffset(edge, false), RecklessRiding.WeaveOffset(edge, lane, 1f), 0.001f);
        }
    }
}
