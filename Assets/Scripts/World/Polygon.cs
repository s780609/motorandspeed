using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 多邊形工具（重劃區範圍判斷）。
    /// </summary>
    public static class Polygon
    {
        /// <summary>
        /// 點是否在多邊形內（射線法），多邊形點依序排列、首尾不需重複。
        /// </summary>
        public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            // 從點往右射一條水平線，穿過邊界奇數次就在裡面
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }
    }
}
