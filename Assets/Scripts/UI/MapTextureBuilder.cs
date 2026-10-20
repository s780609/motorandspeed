using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 在像素陣列上畫粗線（道路）與填滿多邊形（公園），用來產生小地圖貼圖。
    /// </summary>
    public static class MapTextureBuilder
    {
        /// <summary>
        /// 沿著折線（像素座標）畫出半徑 radius 像素的粗線。
        /// </summary>
        public static void DrawPolyline(Color32[] pixels, int size, IReadOnlyList<Vector2> points, float radius, Color32 color)
        {
            if (points.Count == 0)
                return;

            // 沿線每隔半個半徑蓋一個圓形印章，連起來就是粗線
            float spacing = Mathf.Max(0.5f, radius * 0.5f);
            Stamp(pixels, size, points[0], radius, color);
            for (int i = 0; i < points.Count - 1; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(points[i], points[i + 1]) / spacing));
                for (int step = 1; step <= steps; step++)
                {
                    Stamp(pixels, size, Vector2.Lerp(points[i], points[i + 1], step / (float)steps), radius, color);
                }
            }
        }

        /// <summary>
        /// 填滿多邊形（像素座標）。
        /// </summary>
        public static void FillPolygon(Color32[] pixels, int size, IReadOnlyList<Vector2> polygon, Color32 color)
        {
            Vector2 min = polygon[0];
            Vector2 max = polygon[0];
            foreach (Vector2 point in polygon)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            for (int y = Mathf.Max(0, Mathf.FloorToInt(min.y)); y <= Mathf.Min(size - 1, Mathf.CeilToInt(max.y)); y++)
            {
                for (int x = Mathf.Max(0, Mathf.FloorToInt(min.x)); x <= Mathf.Min(size - 1, Mathf.CeilToInt(max.x)); x++)
                {
                    if (World.Polygon.Contains(polygon, new Vector2(x, y)))
                        pixels[y * size + x] = color;
                }
            }
        }

        private static void Stamp(Color32[] pixels, int size, Vector2 center, float radius, Color32 color)
        {
            int reach = Mathf.CeilToInt(radius);
            int centerX = Mathf.RoundToInt(center.x);
            int centerY = Mathf.RoundToInt(center.y);
            float radiusSquared = radius * radius;
            for (int y = centerY - reach; y <= centerY + reach; y++)
            {
                if (y < 0 || y >= size)
                    continue;

                for (int x = centerX - reach; x <= centerX + reach; x++)
                {
                    if (x < 0 || x >= size)
                        continue;

                    float dx = x - center.x;
                    float dy = y - center.y;
                    if (dx * dx + dy * dy <= radiusSquared)
                        pixels[y * size + x] = color;
                }
            }
        }
    }
}
