using UnityEngine;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 小地圖的座標換算（純數學，方便單元測試）。
    /// 地圖範圍是一個正方形，以世界座標範圍的中心為中心、最長邊為邊長；貼圖的 x 朝東、y 朝北。
    /// </summary>
    public class MapProjection
    {
        // 正方形範圍的西南角
        private readonly Vector2 _origin;
        private readonly float _worldSize;
        private readonly int _textureSize;

        /// <summary>每個貼圖像素代表幾公尺。</summary>
        public float MetersPerPixel => _worldSize / _textureSize;

        /// <summary>地圖涵蓋的邊長（公尺）。</summary>
        public float WorldSize => _worldSize;

        public MapProjection(Vector2 worldMin, Vector2 worldMax, int textureSize)
        {
            Vector2 center = (worldMin + worldMax) / 2f;
            _worldSize = Mathf.Max(worldMax.x - worldMin.x, worldMax.y - worldMin.y);
            _origin = center - Vector2.one * (_worldSize / 2f);
            _textureSize = textureSize;
        }

        /// <summary>
        /// 世界座標（x 東、z 北）轉成貼圖 UV（0~1）。
        /// </summary>
        public Vector2 WorldToUv(Vector3 world)
        {
            return new Vector2((world.x - _origin.x) / _worldSize, (world.z - _origin.y) / _worldSize);
        }

        /// <summary>
        /// 世界座標轉成貼圖像素座標。
        /// </summary>
        public Vector2 WorldToPixel(Vector3 world)
        {
            return WorldToUv(world) * _textureSize;
        }

        /// <summary>
        /// 把世界座標轉成「以玩家為中心、面向方向朝上」的地圖位移（公尺）：x 為右、y 為前。
        /// </summary>
        /// <param name="player">玩家位置</param>
        /// <param name="headingDegrees">玩家面向（Unity 的 Y 軸角度，0 為北、90 為東）</param>
        /// <param name="point">要換算的世界座標</param>
        public static Vector2 ToHeadingUp(Vector3 player, float headingDegrees, Vector3 point)
        {
            float radians = headingDegrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            Vector2 right = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
            Vector2 relative = new Vector2(point.x - player.x, point.z - player.z);

            return new Vector2(Vector2.Dot(relative, right), Vector2.Dot(relative, forward));
        }

        /// <summary>
        /// 縮放：steps 為正放大（每格公尺數減半）、為負縮小，結果限制在 min~max 之間。
        /// </summary>
        public static float Zoom(float metersPerPixel, int steps, float min, float max)
        {
            return Mathf.Clamp(metersPerPixel * Mathf.Pow(0.5f, steps), min, max);
        }
    }
}
