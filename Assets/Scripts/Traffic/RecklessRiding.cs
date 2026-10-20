using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// 8+9 的混亂騎法：切西瓜（在機車區與汽車道之間左右亂切）、飆車（不定時暴衝）。
    /// 純計算，方便測試；由 WorldVehicle 套用。
    /// </summary>
    public static class RecklessRiding
    {
        /// <summary>暴衝時的最高速度倍率。</summary>
        public const float MAX_SURGE = 1.5f;

        /// <summary>雙向道切到最內側時離道路中心的距離（貼著中線）。</summary>
        public const float TWO_WAY_INNER_OFFSET = 0.8f;

        /// <summary>
        /// 切西瓜的程度：0 = 待在機車區，1 = 切到最內側。兩組不同頻率的波疊加，忽左忽右、會在兩邊停一下。
        /// </summary>
        public static float Weave(float time, float phase)
        {
            float wave = 0.75f * Mathf.Sin(1.1f * time + phase) + 0.45f * Mathf.Sin(2.3f * time + 1.9f * phase);

            return Mathf.Clamp01(0.5f + 0.5f * wave);
        }

        /// <summary>
        /// 飆車的速度倍率：平常是 1，兩組波同時走高時暴衝到 MAX_SURGE。
        /// </summary>
        public static float Surge(float time, float phase)
        {
            float wave = Mathf.Sin(0.45f * time + phase) + 0.5f * Mathf.Sin(1.7f * time + 2.3f * phase);

            return 1f + (MAX_SURGE - 1f) * Mathf.Clamp01((wave - 0.6f) / 0.6f);
        }

        /// <summary>
        /// 切西瓜時離道路中心的距離：從原本的機車道位置往內切，單行道切進汽車道、雙向道切到中線旁。
        /// </summary>
        public static float WeaveOffset(EdgeData edge, float laneOffset, float weave)
        {
            float inner = edge.oneway != 0 ? WorldTraffic.LaneOffset(edge, false) : TWO_WAY_INNER_OFFSET;

            return Mathf.Lerp(laneOffset, inner, weave);
        }
    }
}
