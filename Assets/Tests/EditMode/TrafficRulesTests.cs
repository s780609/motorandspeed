using MotorAndSpeed.Traffic;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    public class TrafficRulesTests
    {
        private const float CRUISE = 13f;
        private const float DECELERATION = 6f;
        private const float NONE = float.PositiveInfinity;

        [Test]
        public void NoSignalNoObstacle_CruiseSpeed()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, CRUISE, DECELERATION, NONE, SignalState.Green, NONE);

            Assert.AreEqual(CRUISE, speed, 0.001f);
        }

        [Test]
        public void RedLightFarAway_KeepsCruising()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, CRUISE, DECELERATION, 200f, SignalState.Red, NONE);

            Assert.AreEqual(CRUISE, speed, 0.001f);
        }

        [Test]
        public void RedLightApproaching_SlowsDown()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, CRUISE, DECELERATION, 10f, SignalState.Red, NONE);

            Assert.Greater(speed, 0f);
            Assert.Less(speed, CRUISE);
        }

        [Test]
        public void RedLightAtStopLine_Stops()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, 2f, DECELERATION, 0.5f, SignalState.Red, NONE);

            Assert.AreEqual(0f, speed, 0.001f);
        }

        [Test]
        public void GreenLightClose_KeepsCruising()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, CRUISE, DECELERATION, 5f, SignalState.Green, NONE);

            Assert.AreEqual(CRUISE, speed, 0.001f);
        }

        [Test]
        public void YellowLightWithRoomToStop_SlowsDown()
        {
            // 10 m/s 煞停需要約 8.3 公尺，12 公尺外煞得住
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, 10f, DECELERATION, 12f, SignalState.Yellow, NONE);

            Assert.Less(speed, CRUISE);
        }

        [Test]
        public void YellowLightTooCloseToStop_GoesThrough()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, CRUISE, DECELERATION, 5f, SignalState.Yellow, NONE);

            Assert.AreEqual(CRUISE, speed, 0.001f);
        }

        [Test]
        public void VehicleRightAhead_Stops()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, 5f, DECELERATION, NONE, SignalState.Green, 2f);

            Assert.AreEqual(0f, speed, 0.001f);
        }

        [Test]
        public void VehicleAheadAtDistance_SlowsDown()
        {
            float speed = TrafficRules.ComputeTargetSpeed(CRUISE, CRUISE, DECELERATION, NONE, SignalState.Green, 8f);

            Assert.Greater(speed, 0f);
            Assert.Less(speed, CRUISE);
        }
    }
}
