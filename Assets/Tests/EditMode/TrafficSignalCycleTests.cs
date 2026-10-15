using MotorAndSpeed.Traffic;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    public class TrafficSignalCycleTests
    {
        // 主線綠 20 秒、橫向綠 12 秒、黃燈 3 秒、全紅 1.5 秒，一個週期 41 秒
        private TrafficSignalCycle _cycle;

        [SetUp]
        public void SetUp()
        {
            _cycle = new TrafficSignalCycle(20f, 12f, 3f, 1.5f);
        }

        [Test]
        public void CycleLength_IsSumOfAllPhases()
        {
            Assert.AreEqual(41f, _cycle.CycleLength, 0.001f);
        }

        [Test]
        public void Start_MainGreenCrossRed()
        {
            SignalPhase phase = _cycle.Evaluate(0f);

            Assert.AreEqual(SignalState.Green, phase.Main);
            Assert.AreEqual(SignalState.Red, phase.Cross);
            Assert.AreEqual(20f, phase.MainRemaining, 0.001f);
        }

        [Test]
        public void AfterMainGreen_MainYellow()
        {
            SignalPhase phase = _cycle.Evaluate(21f);

            Assert.AreEqual(SignalState.Yellow, phase.Main);
            Assert.AreEqual(SignalState.Red, phase.Cross);
        }

        [Test]
        public void AfterMainYellow_AllRed()
        {
            SignalPhase phase = _cycle.Evaluate(23.5f);

            Assert.AreEqual(SignalState.Red, phase.Main);
            Assert.AreEqual(SignalState.Red, phase.Cross);
        }

        [Test]
        public void CrossGreen_CountsDownItsRemainingTime()
        {
            SignalPhase phase = _cycle.Evaluate(25f);

            Assert.AreEqual(SignalState.Red, phase.Main);
            Assert.AreEqual(SignalState.Green, phase.Cross);
            Assert.AreEqual(11.5f, phase.CrossRemaining, 0.001f);
            Assert.AreEqual(16f, phase.MainRemaining, 0.001f);
        }

        [Test]
        public void AfterCrossGreen_CrossYellow()
        {
            SignalPhase phase = _cycle.Evaluate(37f);

            Assert.AreEqual(SignalState.Red, phase.Main);
            Assert.AreEqual(SignalState.Yellow, phase.Cross);
        }

        [Test]
        public void AfterOneCycle_StartsOver()
        {
            SignalPhase phase = _cycle.Evaluate(46f);

            Assert.AreEqual(SignalState.Green, phase.Main);
            Assert.AreEqual(15f, phase.MainRemaining, 0.001f);
        }
    }
}
