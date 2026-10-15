using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 導航計算（純邏輯，方便單元測試）。
    /// </summary>
    public static class Navigation
    {
        /// <summary>
        /// 從路線上離 position 最近的點開始，沿著路線往前走 distance 公尺，回傳那個位置（導航箭頭要指的點）。
        /// </summary>
        public static Vector3 LookAheadPoint(IReadOnlyList<Vector3> path, Vector3 position, float distance)
        {
            if (path.Count == 0)
                return position;

            if (path.Count == 1)
                return path[0];

            // 找出路線上離玩家最近的位置
            int bestSegment = 0;
            Vector3 current = path[0];
            float bestSquared = float.MaxValue;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 segment = path[i + 1] - path[i];
                float lengthSquared = segment.sqrMagnitude;
                float t = lengthSquared > 0f ? Mathf.Clamp01(Vector3.Dot(position - path[i], segment) / lengthSquared) : 0f;
                Vector3 projected = path[i] + segment * t;
                float squared = (projected - position).sqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    bestSegment = i;
                    current = projected;
                }
            }

            // 從那裡沿著路線往前走
            float remaining = distance;
            for (int i = bestSegment; i < path.Count - 1; i++)
            {
                Vector3 next = path[i + 1];
                float step = Vector3.Distance(current, next);
                if (step >= remaining)
                    return current + (next - current).normalized * remaining;

                remaining -= step;
                current = next;
            }

            return path[path.Count - 1];
        }
    }
}
