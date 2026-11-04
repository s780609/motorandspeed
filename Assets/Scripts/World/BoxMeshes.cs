using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 方塊網格的幾何（純計算）：每面的 UV 依公尺重複，窗戶大小才會一致。
    /// plainCaps 時頂面與底面固定取樣貼圖底部（立面貼圖最下面一條是水泥色飾帶），屋頂就不會出現窗戶。
    /// </summary>
    public static class BoxMeshes
    {
        /// <summary>屋頂、底面取樣的 UV：立面貼圖最下面那條水泥色飾帶。</summary>
        public static readonly Vector2 CapUV = new Vector2(0.5f, 0.03f);

        public static void Tiled(Vector3 size, Vector2 tile, bool plainCaps, out Vector3[] vertices, out Vector3[] normals, out Vector2[] uv, out int[] triangles)
        {
            Vector3 half = size / 2f;
            // 每一面：法線與「從外面看過去的上方」；最後兩面是頂面與底面
            Vector3[,] faces =
            {
                { Vector3.right, Vector3.up },
                { Vector3.left, Vector3.up },
                { Vector3.forward, Vector3.up },
                { Vector3.back, Vector3.up },
                { Vector3.up, Vector3.forward },
                { Vector3.down, Vector3.forward },
            };
            vertices = new Vector3[24];
            normals = new Vector3[24];
            uv = new Vector2[24];
            triangles = new int[36];
            for (int f = 0; f < 6; f++)
            {
                Vector3 normal = faces[f, 0];
                Vector3 up = faces[f, 1];
                Vector3 right = Vector3.Cross(normal, up);
                float halfRight = Mathf.Abs(Vector3.Dot(half, right));
                float halfUp = Mathf.Abs(Vector3.Dot(half, up));
                Vector3 center = normal * Mathf.Abs(Vector3.Dot(half, normal));
                Vector2 repeat = new Vector2(halfRight * 2f / tile.x, halfUp * 2f / tile.y);
                int v = f * 4;

                // 左下、右下、左上、右上
                vertices[v] = center - right * halfRight - up * halfUp;
                vertices[v + 1] = center + right * halfRight - up * halfUp;
                vertices[v + 2] = center - right * halfRight + up * halfUp;
                vertices[v + 3] = center + right * halfRight + up * halfUp;
                bool cap = plainCaps && f >= 4;
                uv[v] = cap ? CapUV : Vector2.zero;
                uv[v + 1] = cap ? CapUV : new Vector2(repeat.x, 0f);
                uv[v + 2] = cap ? CapUV : new Vector2(0f, repeat.y);
                uv[v + 3] = cap ? CapUV : repeat;
                for (int i = 0; i < 4; i++)
                {
                    normals[v + i] = normal;
                }

                // 從外面看是順時針：左下 → 左上 → 右上、左下 → 右上 → 右下
                int t = f * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 3;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 1;
            }
        }
    }
}
