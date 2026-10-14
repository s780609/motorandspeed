using MotorAndSpeed.Scooter;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    public class ScooterPhysicsTests
    {
        private const float DELTA_TIME = 0.02f;

        private ScooterSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new ScooterSettings();
        }

        [Test]
        public void UpdateSpeed_Throttle_IncreasesSpeed()
        {
            float speed = ScooterPhysics.UpdateSpeed(0f, 1f, 0f, DELTA_TIME, _settings);

            Assert.Greater(speed, 0f);
        }

        [Test]
        public void UpdateSpeed_FullThrottleForLongTime_StopsAtMaxSpeed()
        {
            float speed = Simulate(0f, 1f, 0f, 30f);

            Assert.AreEqual(_settings.MaxSpeed, speed, 0.01f);
        }

        [Test]
        public void UpdateSpeed_NoInput_CoastsToStopWithoutReversing()
        {
            float speed = Simulate(10f, 0f, 0f, 10f);

            Assert.AreEqual(0f, speed, 0.0001f);
        }

        [Test]
        public void UpdateSpeed_Brake_SlowsFasterThanCoasting()
        {
            float braking = ScooterPhysics.UpdateSpeed(10f, 0f, 1f, DELTA_TIME, _settings);
            float coasting = ScooterPhysics.UpdateSpeed(10f, 0f, 0f, DELTA_TIME, _settings);

            Assert.Less(braking, coasting);
        }

        [Test]
        public void UpdateSpeed_BrakeWhileMoving_DoesNotReverseInSameStep()
        {
            float speed = ScooterPhysics.UpdateSpeed(0.1f, 0f, 1f, DELTA_TIME, _settings);

            Assert.AreEqual(0f, speed, 0.0001f);
        }

        [Test]
        public void UpdateSpeed_BrakeWhenStopped_ReversesUpToMaxReverseSpeed()
        {
            float speed = Simulate(0f, 0f, 1f, 30f);

            Assert.AreEqual(-_settings.MaxReverseSpeed, speed, 0.01f);
        }

        [Test]
        public void ComputeYawRate_WhenStopped_IsZero()
        {
            float yawRate = ScooterPhysics.ComputeYawRate(1f, 0f, _settings);

            Assert.AreEqual(0f, yawRate, 0.0001f);
        }

        [Test]
        public void ComputeYawRate_SteerRightWhileMoving_TurnsRight()
        {
            float yawRate = ScooterPhysics.ComputeYawRate(1f, 15f, _settings);

            Assert.Greater(yawRate, 0f);
        }

        [Test]
        public void ComputeYawRate_InReverse_IsInverted()
        {
            float yawRate = ScooterPhysics.ComputeYawRate(1f, -4f, _settings);

            Assert.Less(yawRate, 0f);
        }

        [Test]
        public void ComputeYawRate_AtTopSpeed_TurnsSlowerThanAtMidSpeed()
        {
            float midSpeed = ScooterPhysics.ComputeYawRate(1f, _settings.TurnFullSpeed, _settings);
            float topSpeed = ScooterPhysics.ComputeYawRate(1f, _settings.MaxSpeed, _settings);

            Assert.Less(topSpeed, midSpeed);
        }

        [Test]
        public void ComputeLeanAngle_SteerRight_LeansRight()
        {
            float lean = ScooterPhysics.ComputeLeanAngle(1f, 20f, _settings);

            Assert.Less(lean, 0f);
        }

        [Test]
        public void ComputeLeanAngle_WhenStopped_IsZero()
        {
            float lean = ScooterPhysics.ComputeLeanAngle(1f, 0f, _settings);

            Assert.AreEqual(0f, lean, 0.0001f);
        }

        [Test]
        public void ComputeLeanAngle_NeverExceedsMaxLean()
        {
            float lean = ScooterPhysics.ComputeLeanAngle(-5f, 100f, _settings);

            Assert.AreEqual(_settings.MaxLean, lean, 0.0001f);
        }

        private float Simulate(float speed, float throttle, float brake, float seconds)
        {
            int steps = (int)(seconds / DELTA_TIME);
            for (int i = 0; i < steps; i++)
            {
                speed = ScooterPhysics.UpdateSpeed(speed, throttle, brake, DELTA_TIME, _settings);
            }

            return speed;
        }
    }
}
