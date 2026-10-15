using System.Collections.Generic;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class HatchPatternTests
    {
        private const float EPSILON = 0.001f;

        [Test]
        public void Diagonals_AllEndpointsInsideRectangle()
        {
            List<Vector4> lines = HatchPattern.Diagonals(7f, 10f, 2.5f);

            Assert.Greater(lines.Count, 0);
            foreach (Vector4 line in lines)
            {
                Assert.LessOrEqual(Mathf.Abs(line.x), 7f + EPSILON);
                Assert.LessOrEqual(Mathf.Abs(line.z), 7f + EPSILON);
                Assert.LessOrEqual(Mathf.Abs(line.y), 10f + EPSILON);
                Assert.LessOrEqual(Mathf.Abs(line.w), 10f + EPSILON);
            }
        }

        [Test]
        public void Diagonals_AreFortyFiveDegreesInBothDirections()
        {
            List<Vector4> lines = HatchPattern.Diagonals(7f, 10f, 2.5f);
            bool rising = false;
            bool falling = false;

            foreach (Vector4 line in lines)
            {
                float dx = line.z - line.x;
                float dy = line.w - line.y;
                Assert.AreEqual(Mathf.Abs(dx), Mathf.Abs(dy), EPSILON, "斜率應為 ±1");
                rising |= dx * dy > 0f;
                falling |= dx * dy < 0f;
            }

            Assert.IsTrue(rising && falling, "兩個方向的斜線都要有");
        }

        [Test]
        public void Diagonals_CenterLinePassesThroughCorners()
        {
            // 正方形時，通過中心的斜線剛好連接對角
            List<Vector4> lines = HatchPattern.Diagonals(5f, 5f, 2f);

            Assert.IsTrue(lines.Exists(l => Mathf.Abs(l.x + 5f) < EPSILON && Mathf.Abs(l.y + 5f) < EPSILON && Mathf.Abs(l.z - 5f) < EPSILON && Mathf.Abs(l.w - 5f) < EPSILON));
        }

        [Test]
        public void Diagonals_EmptyRectangle_ReturnsNothing()
        {
            Assert.AreEqual(0, HatchPattern.Diagonals(0f, 5f, 2f).Count);
        }
    }
}
