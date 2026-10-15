using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 沿著折線行走的工具：用「離起點的距離」查詢位置、方向。
    /// </summary>
    public class RoutePath
    {
        private readonly Vector3[] _points;
        // 每個點離起點的累計距離
        private readonly float[] _distances;

        public float Length => _distances[_distances.Length - 1];

        public RoutePath(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
                throw new ArgumentException("路徑至少需要兩個點", nameof(points));

            _points = new Vector3[points.Count];
            _distances = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                _points[i] = points[i];
                _distances[i] = i == 0 ? 0f : _distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
        }

        /// <summary>
        /// 從路線資料建立路徑（y = 0）。
        /// </summary>
        public static RoutePath FromRoadLine(RoadLine line)
        {
            List<Vector3> points = new List<Vector3>(line.points.Count);
            foreach (RoutePoint point in line.points)
            {
                points.Add(new Vector3(point.x, 0f, point.z));
            }

            return new RoutePath(points);
        }

        /// <summary>
        /// 取得離起點 distance 公尺的位置，超出範圍時停在端點。
        /// </summary>
        public Vector3 GetPoint(float distance)
        {
            int segment = FindSegment(distance);
            float segmentLength = _distances[segment + 1] - _distances[segment];
            float t = segmentLength > 0f ? Mathf.Clamp01((distance - _distances[segment]) / segmentLength) : 0f;

            return Vector3.Lerp(_points[segment], _points[segment + 1], t);
        }

        /// <summary>
        /// 取得前進方向（水平、長度 1）。
        /// </summary>
        public Vector3 GetTangent(float distance)
        {
            int segment = FindSegment(distance);
            Vector3 direction = _points[segment + 1] - _points[segment];
            direction.y = 0f;

            return direction.normalized;
        }

        /// <summary>
        /// 取得前進方向的右手邊（水平、長度 1）。
        /// </summary>
        public Vector3 GetRight(float distance)
        {
            Vector3 tangent = GetTangent(distance);

            return new Vector3(tangent.z, 0f, -tangent.x);
        }

        /// <summary>
        /// 找出路徑上離 position 最近的點，回傳它離起點的距離。
        /// </summary>
        public float GetClosestDistance(Vector3 position)
        {
            float bestDistance = 0f;
            float bestSquared = float.MaxValue;
            for (int i = 0; i < _points.Length - 1; i++)
            {
                Vector3 a = _points[i];
                Vector3 segment = _points[i + 1] - a;
                float segmentLength = segment.magnitude;
                if (segmentLength <= 0f)
                    continue;

                float along = Mathf.Clamp(Vector3.Dot(position - a, segment / segmentLength), 0f, segmentLength);
                float squared = (a + segment / segmentLength * along - position).sqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    bestDistance = _distances[i] + along;
                }
            }

            return bestDistance;
        }

        private int FindSegment(float distance)
        {
            // 二分搜尋：找出 distance 落在哪一段
            int low = 0;
            int high = _distances.Length - 2;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (_distances[middle] <= distance)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }
    }
}
