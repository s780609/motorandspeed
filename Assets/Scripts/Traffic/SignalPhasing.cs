using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// 路口號誌分組：方向相近或相反的進路同時綠燈（第 0 組），其他方向是第 1 組。
    /// </summary>
    public static class SignalPhasing
    {
        /// <summary>
        /// 依每條進路的方向分組，第一條進路所在的軸線為第 0 組。
        /// </summary>
        /// <param name="approaches">每條進路從路口往外的方向（x, z）</param>
        /// <returns>每條進路的組別（0 或 1）</returns>
        public static int[] Group(IReadOnlyList<Vector2> approaches)
        {
            int[] phases = new int[approaches.Count];
            if (approaches.Count == 0)
                return phases;

            Vector2 axis = approaches[0].normalized;
            for (int i = 0; i < approaches.Count; i++)
            {
                // 跟軸線夾角小於 45 度（同向或反向）就是第 0 組
                float alignment = Mathf.Abs(Vector2.Dot(approaches[i].normalized, axis));
                phases[i] = alignment >= Mathf.Cos(45f * Mathf.Deg2Rad) ? 0 : 1;
            }

            return phases;
        }
    }
}
