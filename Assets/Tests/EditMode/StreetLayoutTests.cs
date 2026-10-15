using System;
using System.Collections.Generic;
using System.Linq;
using MotorAndSpeed.World;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    public class StreetLayoutTests
    {
        private const float MIN_WIDTH = 4.5f;
        private const float MAX_WIDTH = 9f;

        [TestCase(184f, 1)]
        [TestCase(184f, 42)]
        [TestCase(9.5f, 7)]
        [TestCase(4.5f, 3)]
        public void SplitFrontage_WidthsSumToLength(float length, int seed)
        {
            List<float> widths = StreetLayout.SplitFrontage(length, MIN_WIDTH, MAX_WIDTH, new Random(seed));

            Assert.AreEqual(length, widths.Sum(), 0.001f);
        }

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(2026)]
        public void SplitFrontage_EveryWidthWithinRange(int seed)
        {
            List<float> widths = StreetLayout.SplitFrontage(184f, MIN_WIDTH, MAX_WIDTH, new Random(seed));

            foreach (float width in widths)
            {
                Assert.GreaterOrEqual(width, MIN_WIDTH - 0.001f);
                Assert.LessOrEqual(width, MAX_WIDTH + 0.001f);
            }
        }

        [Test]
        public void SplitFrontage_SameSeed_SameLayout()
        {
            List<float> first = StreetLayout.SplitFrontage(184f, MIN_WIDTH, MAX_WIDTH, new Random(7));
            List<float> second = StreetLayout.SplitFrontage(184f, MIN_WIDTH, MAX_WIDTH, new Random(7));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void SplitFrontage_LengthShorterThanMinWidth_Throws()
        {
            Assert.Throws<ArgumentException>(() => StreetLayout.SplitFrontage(3f, MIN_WIDTH, MAX_WIDTH, new Random(1)));
        }
    }
}
