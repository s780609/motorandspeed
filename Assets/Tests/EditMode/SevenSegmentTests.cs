using MotorAndSpeed.Traffic;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    public class SevenSegmentTests
    {
        // a=1, b=2, c=4, d=8, e=16, f=32, g=64
        private const int ONE = 2 | 4;
        private const int TWO = 1 | 2 | 8 | 16 | 64;
        private const int SEVEN = 1 | 2 | 4;
        private const int EIGHT = 127;
        private const int NINE = 1 | 2 | 4 | 8 | 32 | 64;

        [Test]
        public void SingleDigit_TensIsBlank()
        {
            SevenSegment.Encode(7, out int tens, out int ones);

            Assert.AreEqual(0, tens);
            Assert.AreEqual(SEVEN, ones);
        }

        [Test]
        public void TwoDigits_BothShown()
        {
            SevenSegment.Encode(12, out int tens, out int ones);

            Assert.AreEqual(ONE, tens);
            Assert.AreEqual(TWO, ones);
        }

        [Test]
        public void Eighty_ShowsAllSegmentsAndZero()
        {
            SevenSegment.Encode(80, out int tens, out int ones);

            Assert.AreEqual(EIGHT, tens);
            Assert.AreEqual(1 | 2 | 4 | 8 | 16 | 32, ones);
        }

        [Test]
        public void ZeroOrNegative_AllDark()
        {
            SevenSegment.Encode(0, out int tens, out int ones);

            Assert.AreEqual(0, tens);
            Assert.AreEqual(0, ones);
        }

        [Test]
        public void OverNinetyNine_ShowsNinetyNine()
        {
            SevenSegment.Encode(150, out int tens, out int ones);

            Assert.AreEqual(NINE, tens);
            Assert.AreEqual(NINE, ones);
        }
    }
}
