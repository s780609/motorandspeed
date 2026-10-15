using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 高架橋的高度曲線：從接地端沿著引道平順爬升到橋面高度，之後維持水平。
    /// </summary>
    public static class BridgeProfile
    {
        /// <summary>橋面離地高度（公尺），要能跨過底下的平面道路。</summary>
        public const float DECK_HEIGHT = 8f;

        /// <summary>引道長度（公尺）。</summary>
        public const float RAMP_LENGTH = 140f;

        /// <summary>
        /// 距離接地端 distance 公尺處的橋面高度。
        /// </summary>
        public static float HeightAt(float distance)
        {
            // smoothstep：起點和到頂都是水平切入，不會一下子撞上坡
            float t = Mathf.Clamp01(distance / RAMP_LENGTH);

            return DECK_HEIGHT * t * t * (3f - 2f * t);
        }
    }
}
