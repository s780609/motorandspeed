using System;
using System.Collections.Generic;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 街道配置計算（純數學，方便單元測試）。
    /// </summary>
    public static class StreetLayout
    {
        /// <summary>
        /// 把一段街道切成多棟寬度隨機的建築，寬度總和剛好等於街道長度。
        /// </summary>
        /// <param name="length">街道長度（公尺）</param>
        /// <param name="minWidth">最小建築寬度</param>
        /// <param name="maxWidth">最大建築寬度，需至少是最小寬度的兩倍</param>
        /// <param name="random">亂數來源，同一個種子會得到同樣的配置</param>
        /// <returns>每棟建築的寬度，依序排列</returns>
        public static List<float> SplitFrontage(float length, float minWidth, float maxWidth, Random random)
        {
            if (length < minWidth)
                throw new ArgumentException($"街道長度 {length} 小於最小建築寬度 {minWidth}", nameof(length));

            List<float> widths = new List<float>();
            float remaining = length;
            while (remaining > maxWidth)
            {
                // 保證切完後剩下的長度至少還能放一棟最小寬度的建築
                float upper = Math.Min(maxWidth, remaining - minWidth);
                float width = minWidth + (float)random.NextDouble() * (upper - minWidth);
                widths.Add(width);
                remaining -= width;
            }

            widths.Add(remaining);

            return widths;
        }
    }
}
