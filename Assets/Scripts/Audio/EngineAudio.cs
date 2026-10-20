using UnityEngine;

namespace MotorAndSpeed.Audio
{
    /// <summary>
    /// 引擎聲的音高、音量計算（純數學，不依賴場景，方便單元測試）。
    /// </summary>
    public static class EngineAudio
    {
        /// <summary>
        /// 依車速與油門計算音高。速克達是無段變速，起步時轉速很快拉高、之後慢慢爬升，所以用開根號的曲線。
        /// </summary>
        /// <param name="speed">速度（m/s），倒車為負數</param>
        /// <param name="throttle">油門 0~1</param>
        /// <param name="profile">引擎聲參數</param>
        public static float ComputePitch(float speed, float throttle, EngineSoundProfile profile)
        {
            float load = Mathf.Clamp01(Mathf.Abs(speed) / profile.TopSpeed);

            return Mathf.Lerp(profile.IdlePitch, profile.MaxPitch, Mathf.Sqrt(load)) + profile.ThrottlePitch * Mathf.Clamp01(throttle);
        }

        /// <summary>
        /// 依車速與油門計算音量：車速、油門各佔一半。
        /// </summary>
        public static float ComputeVolume(float speed, float throttle, EngineSoundProfile profile)
        {
            float load = Mathf.Clamp01(Mathf.Abs(speed) / profile.TopSpeed);

            return Mathf.Lerp(profile.IdleVolume, profile.MaxVolume, 0.5f * load + 0.5f * Mathf.Clamp01(throttle));
        }

        /// <summary>
        /// NPC 沒有油門輸入，用加速度估計：加速中算催油門，等速或減速算收油。
        /// </summary>
        /// <param name="acceleration">加速度（m/s²）</param>
        /// <param name="fullThrottleAcceleration">這個加速度以上算催滿油門</param>
        public static float EstimateThrottle(float acceleration, float fullThrottleAcceleration)
        {
            return Mathf.Clamp01(acceleration / fullThrottleAcceleration);
        }

        /// <summary>
        /// 指數平滑：讓音高、音量跟著目標值變化，不會一格一格跳。
        /// </summary>
        public static float Smooth(float current, float target, float sharpness, float deltaTime)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        }
    }
}
