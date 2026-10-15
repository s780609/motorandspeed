using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 路口黃色網狀線：在矩形內畫兩組 45 度斜線（純數學，方便單元測試）。
    /// </summary>
    public static class HatchPattern
    {
        /// <summary>
        /// 產生裁切在矩形 [-halfWidth, halfWidth] × [-halfLength, halfLength] 內的斜線段。
        /// </summary>
        /// <returns>每段為 (x0, y0, x1, y1)：x 為橫向、y 為沿路方向</returns>
        public static List<Vector4> Diagonals(float halfWidth, float halfLength, float spacing)
        {
            List<Vector4> lines = new List<Vector4>();
            if (halfWidth <= 0f || halfLength <= 0f || spacing <= 0f)
                return lines;

            float reach = halfWidth + halfLength;
            for (float c = 0f; c <= reach; c += spacing)
            {
                foreach (float offset in c > 0f ? new[] { c, -c } : new[] { c })
                {
                    // 上升斜線 x = y + c：y 的範圍要讓 x 也落在矩形內
                    float y0 = Math.Max(-halfLength, -halfWidth - offset);
                    float y1 = Math.Min(halfLength, halfWidth - offset);
                    if (y1 - y0 > 0.001f)
                        lines.Add(new Vector4(y0 + offset, y0, y1 + offset, y1));

                    // 下降斜線 x = c - y
                    y0 = Math.Max(-halfLength, offset - halfWidth);
                    y1 = Math.Min(halfLength, offset + halfWidth);
                    if (y1 - y0 > 0.001f)
                        lines.Add(new Vector4(offset - y0, y0, offset - y1, y1));
                }
            }

            return lines;
        }
    }
}
