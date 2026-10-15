using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 佔地格子：把地面切成小方格，記錄哪些格子已經被道路或建築佔用，避免東西互相重疊。
    /// 座標使用 (x, z) 平面；格子範圍外一律視為不可用。
    /// </summary>
    public class OccupancyGrid
    {
        private readonly Vector2 _min;
        private readonly float _cellSize;
        private readonly int _width;
        private readonly int _height;
        private readonly bool[] _occupied;

        public OccupancyGrid(Vector2 min, Vector2 max, float cellSize)
        {
            _min = min;
            _cellSize = cellSize;
            _width = Mathf.CeilToInt((max.x - min.x) / cellSize);
            _height = Mathf.CeilToInt((max.y - min.y) / cellSize);
            _occupied = new bool[_width * _height];
        }

        /// <summary>
        /// 旋轉矩形（中心、沿 forward 的半長、垂直方向的半寬）內是否完全沒有被佔用。
        /// </summary>
        public bool IsFree(Vector2 center, Vector2 forward, float halfLength, float halfWidth)
        {
            return Visit(center, forward, halfLength, halfWidth, false);
        }

        /// <summary>
        /// 把旋轉矩形範圍標記為已佔用。
        /// </summary>
        public void Mark(Vector2 center, Vector2 forward, float halfLength, float halfWidth)
        {
            Visit(center, forward, halfLength, halfWidth, true);
        }

        /// <summary>
        /// 把圓形範圍標記為已佔用（路口）。
        /// </summary>
        public void MarkCircle(Vector2 center, float radius)
        {
            GetCellRange(center, radius, radius, out int x0, out int y0, out int x1, out int y1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(_height - 1, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_width - 1, x1); x++)
                {
                    if ((CellCenter(x, y) - center).sqrMagnitude <= radius * radius)
                        _occupied[y * _width + x] = true;
                }
            }
        }

        // 走訪旋轉矩形涵蓋的格子：mark 為 true 時標記，否則檢查是否全空
        private bool Visit(Vector2 center, Vector2 forward, float halfLength, float halfWidth, bool mark)
        {
            forward = forward.normalized;
            Vector2 side = new Vector2(forward.y, -forward.x);
            float extentX = Mathf.Abs(forward.x) * halfLength + Mathf.Abs(side.x) * halfWidth;
            float extentY = Mathf.Abs(forward.y) * halfLength + Mathf.Abs(side.y) * halfWidth;
            GetCellRange(center, extentX, extentY, out int x0, out int y0, out int x1, out int y1);

            if (!mark && (x0 < 0 || y0 < 0 || x1 >= _width || y1 >= _height))
                return false;

            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(_height - 1, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_width - 1, x1); x++)
                {
                    Vector2 offset = CellCenter(x, y) - center;
                    if (Mathf.Abs(Vector2.Dot(offset, forward)) > halfLength || Mathf.Abs(Vector2.Dot(offset, side)) > halfWidth)
                        continue;

                    int index = y * _width + x;
                    if (mark)
                    {
                        _occupied[index] = true;
                    }
                    else if (_occupied[index])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void GetCellRange(Vector2 center, float extentX, float extentY, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Mathf.FloorToInt((center.x - extentX - _min.x) / _cellSize);
            y0 = Mathf.FloorToInt((center.y - extentY - _min.y) / _cellSize);
            x1 = Mathf.FloorToInt((center.x + extentX - _min.x) / _cellSize);
            y1 = Mathf.FloorToInt((center.y + extentY - _min.y) / _cellSize);
        }

        private Vector2 CellCenter(int x, int y)
        {
            return _min + new Vector2((x + 0.5f) * _cellSize, (y + 0.5f) * _cellSize);
        }
    }
}
