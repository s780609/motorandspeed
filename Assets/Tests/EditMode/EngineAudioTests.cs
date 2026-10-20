using MotorAndSpeed.Audio;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    public class EngineAudioTests
    {
        private EngineSoundProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = new EngineSoundProfile();
        }

        [Test]
        public void ComputePitch_StoppedWithoutThrottle_IsIdle()
        {
            Assert.AreEqual(_profile.IdlePitch, EngineAudio.ComputePitch(0f, 0f, _profile), 0.0001f);
        }

        [Test]
        public void ComputePitch_RisesWithSpeed()
        {
            float previous = EngineAudio.ComputePitch(0f, 0f, _profile);
            for (float speed = 2f; speed <= _profile.TopSpeed; speed += 2f)
            {
                float pitch = EngineAudio.ComputePitch(speed, 0f, _profile);
                Assert.Greater(pitch, previous, $"{speed} m/s 的音高應該比較高");
                previous = pitch;
            }
        }

        [Test]
        public void ComputePitch_AtTopSpeed_ReachesMaxAndStopsThere()
        {
            Assert.AreEqual(_profile.MaxPitch, EngineAudio.ComputePitch(_profile.TopSpeed, 0f, _profile), 0.0001f);
            Assert.AreEqual(_profile.MaxPitch, EngineAudio.ComputePitch(_profile.TopSpeed * 2f, 0f, _profile), 0.0001f);
        }

        [Test]
        public void ComputePitch_ClimbsFastAtLowSpeed()
        {
            // 無段變速：一半極速時音高已經超過一半的變化量
            float half = EngineAudio.ComputePitch(_profile.TopSpeed * 0.5f, 0f, _profile);

            Assert.Greater(half, (_profile.IdlePitch + _profile.MaxPitch) / 2f);
        }

        [Test]
        public void ComputePitch_ThrottleRaisesRevs()
        {
            float coasting = EngineAudio.ComputePitch(10f, 0f, _profile);
            float full = EngineAudio.ComputePitch(10f, 1f, _profile);

            Assert.AreEqual(_profile.ThrottlePitch, full - coasting, 0.0001f);
        }

        [Test]
        public void ComputePitch_ReverseSoundsLikeForward()
        {
            Assert.AreEqual(EngineAudio.ComputePitch(4f, 0.5f, _profile), EngineAudio.ComputePitch(-4f, 0.5f, _profile), 0.0001f);
        }

        [Test]
        public void ComputePitch_ClampsThrottle()
        {
            Assert.AreEqual(EngineAudio.ComputePitch(5f, 1f, _profile), EngineAudio.ComputePitch(5f, 3f, _profile), 0.0001f);
            Assert.AreEqual(EngineAudio.ComputePitch(5f, 0f, _profile), EngineAudio.ComputePitch(5f, -1f, _profile), 0.0001f);
        }

        [Test]
        public void ComputeVolume_IdleAndFullOut()
        {
            Assert.AreEqual(_profile.IdleVolume, EngineAudio.ComputeVolume(0f, 0f, _profile), 0.0001f);
            Assert.AreEqual(_profile.MaxVolume, EngineAudio.ComputeVolume(_profile.TopSpeed, 1f, _profile), 0.0001f);
        }

        [Test]
        public void ComputeVolume_ThrottleAndSpeedBothMakeItLouder()
        {
            float idle = EngineAudio.ComputeVolume(0f, 0f, _profile);
            float revving = EngineAudio.ComputeVolume(0f, 1f, _profile);
            float cruising = EngineAudio.ComputeVolume(15f, 0f, _profile);
            float accelerating = EngineAudio.ComputeVolume(15f, 1f, _profile);

            Assert.Greater(revving, idle, "停住催油門要比怠速大聲");
            Assert.Greater(cruising, idle, "行進中要比怠速大聲");
            Assert.Greater(accelerating, cruising, "加速要比滑行大聲");
        }

        [Test]
        public void ComputeVolume_StaysWithinProfileRange([Values(-10f, 0f, 7f, 28f, 60f)] float speed, [Values(-1f, 0f, 0.5f, 1f, 2f)] float throttle)
        {
            float volume = EngineAudio.ComputeVolume(speed, throttle, _profile);

            Assert.GreaterOrEqual(volume, _profile.IdleVolume - 0.0001f);
            Assert.LessOrEqual(volume, _profile.MaxVolume + 0.0001f);
        }

        [Test]
        public void EstimateThrottle_FromAcceleration()
        {
            Assert.AreEqual(0f, EngineAudio.EstimateThrottle(-5f, 3f), 0.0001f, "減速時收油");
            Assert.AreEqual(0f, EngineAudio.EstimateThrottle(0f, 3f), 0.0001f, "等速時收油");
            Assert.AreEqual(0.5f, EngineAudio.EstimateThrottle(1.5f, 3f), 0.0001f);
            Assert.AreEqual(1f, EngineAudio.EstimateThrottle(9f, 3f), 0.0001f, "加速度再大也只到滿油門");
        }

        [Test]
        public void Smooth_MovesTowardTargetWithoutOvershoot()
        {
            float value = 0f;
            for (int i = 0; i < 200; i++)
            {
                float next = EngineAudio.Smooth(value, 1f, 8f, 0.02f);
                Assert.GreaterOrEqual(next, value);
                Assert.LessOrEqual(next, 1f);
                value = next;
            }

            Assert.AreEqual(1f, value, 0.001f);
        }
    }
}
