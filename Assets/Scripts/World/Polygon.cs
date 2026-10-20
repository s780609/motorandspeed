using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 多邊形工具（重劃區範圍判斷、公園地面三角化）。
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

        /// <summary>
        /// 把簡單多邊形（可以凹、不能自我交叉）切成三角形（剪耳法），回傳頂點索引，每三個一組。
        /// </summary>
        public static List<int> Triangulate(IReadOnlyList<Vector2> polygon)
        {
            List<int> triangles = new List<int>();
            List<int> remaining = new List<int>();
            for (int i = 0; i < polygon.Count; i++)
            {
                remaining.Add(i);
            }

            // 統一成逆時針，凸角判斷才一致
            if (SignedArea(polygon) < 0f)
                remaining.Reverse();

            int guard = polygon.Count * polygon.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int current = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    if (!IsEar(polygon, remaining, previous, current, next))
                        continue;

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                // 資料有瑕疵（重複點、微小自我交叉）時找不到耳朵，直接切掉一個角繼續
                if (!clipped)
                {
                    triangles.Add(remaining[remaining.Count - 1]);
                    triangles.Add(remaining[0]);
                    triangles.Add(remaining[1]);
                    remaining.RemoveAt(0);
                }
            }

            if (remaining.Count == 3)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[1]);
                triangles.Add(remaining[2]);
            }

            return triangles;
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                area += polygon[j].x * polygon[i].y - polygon[i].x * polygon[j].y;
            }

            return area / 2f;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static bool IsEar(IReadOnlyList<Vector2> polygon, List<int> remaining, int previous, int current, int next)
        {
            Vector2 a = polygon[previous];
            Vector2 b = polygon[current];
            Vector2 c = polygon[next];

            // 逆時針排列下，凸角的外積為正
            if (Cross(a, b, c) <= 1e-6f)
                return false;

            foreach (int index in remaining)
            {
                if (index == previous || index == current || index == next)
                    continue;

                Vector2 p = polygon[index];
                if (Cross(a, b, p) >= 0f && Cross(b, c, p) >= 0f && Cross(c, a, p) >= 0f)
                    return false;
            }

            return true;
        }
    }
}
