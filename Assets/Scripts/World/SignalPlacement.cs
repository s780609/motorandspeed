using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 紅綠燈燈桿的位置：立在路口旁的路邊，不能插在任何道路或路口的路面上。
    /// 雙向分隔道路的路口是好幾個節點擠在一起，原本「路口對面」的位置常常落在另一條車道上。
    /// </summary>
    public class SignalPlacement
    {
        // 燈桿離路面邊緣至少要留多少（公尺）
        private const float CLEARANCE = 0.6f;
        private const float SEARCH_STEP = 0.5f;
        private const float SEARCH_LIMIT = 12f;
        // 只檢查路口附近的道路
        private const float NEARBY = 80f;

        private readonly WorldData _data;
        private readonly float[] _radius;

        public SignalPlacement(WorldData data)
        {
            _data = data;
            _radius = new float[data.junctions.Count];
            foreach (EdgeData edge in data.edges)
            {
                foreach (int id in new[] { edge.start, edge.end })
                {
                    _radius[id] = Mathf.Max(_radius[id], edge.width / 2f + 1f);
                }
            }
        }

        /// <summary>
        /// 路口半徑：接進來最寬的道路一半再加 1 公尺（與場景建置相同）。
        /// </summary>
        public float JunctionRadius(int junctionId)
        {
            return _radius[junctionId];
        }

        /// <summary>
        /// 點是否落在道路或路口的路面上（含 clearance 的緩衝）。
        /// </summary>
        public bool IsOnRoad(Vector2 point, float clearance)
        {
            return IsOnRoad(point, clearance, _data.edges, _data.junctions);
        }

        /// <summary>
        /// 一個進路方向的燈桿位置：travel 是開進路口的方向，halfWidth 是那條路的半寬。
        /// 先試路口對面的右側（台灣常見），不行就試停止線旁的右側，再不行就往路外推。
        /// </summary>
        public Vector2 PolePosition(JunctionData junction, Vector2 travel, float halfWidth)
        {
            Vector2 center = new Vector2(junction.x, junction.z);
            travel = travel.normalized;
            Vector2 right = new Vector2(travel.y, -travel.x);
            float radius = _radius[junction.id];
            Vector2 farSide = center + travel * (radius + 1f);
            Vector2 nearSide = center - travel * (radius + 1f);
            Vector2 left = -right;

            // 依序嘗試：對面右側、停止線右側、對面左側（分隔島上）、停止線左側，以及再往外一點的位置
            (Vector2 Base, Vector2 Side)[] options =
            {
                (farSide, right), (nearSide, right), (farSide, left), (nearSide, left),
                (farSide + travel * 5f, right), (nearSide - travel * 5f, right),
            };

            List<EdgeData> edges = new List<EdgeData>();
            foreach (EdgeData edge in _data.edges)
            {
                if (IsNear(edge, center))
                    edges.Add(edge);
            }

            List<JunctionData> junctions = _data.junctions.FindAll(j => Vector2.Distance(new Vector2(j.x, j.z), center) < NEARBY);
            for (float push = 0f; push <= SEARCH_LIMIT; push += SEARCH_STEP)
            {
                foreach ((Vector2 basePoint, Vector2 side) in options)
                {
                    Vector2 candidate = basePoint + side * (halfWidth + CLEARANCE + push);
                    if (!IsOnRoad(candidate, CLEARANCE, edges, junctions))
                        return candidate;
                }
            }

            return farSide + right * (halfWidth + CLEARANCE);
        }

        private static bool IsNear(EdgeData edge, Vector2 center)
        {
            foreach (RoutePoint point in edge.points)
            {
                if (Vector2.Distance(new Vector2(point.x, point.z), center) < NEARBY)
                    return true;
            }

            return false;
        }

        private bool IsOnRoad(Vector2 point, float clearance, IReadOnlyList<EdgeData> edges, IReadOnlyList<JunctionData> junctions)
        {
            // 路口補面的半徑是路口半徑減 1（見 WuguWorldBuilder.BuildRoads）
            foreach (JunctionData junction in junctions)
            {
                if (junction.edges.Count >= 2 && Vector2.Distance(point, new Vector2(junction.x, junction.z)) < _radius[junction.id] - 1f + clearance)
                    return true;
            }

            foreach (EdgeData edge in edges)
            {
                float limit = edge.width / 2f + clearance;
                for (int i = 0; i < edge.points.Count - 1; i++)
                {
                    Vector2 a = new Vector2(edge.points[i].x, edge.points[i].z);
                    Vector2 b = new Vector2(edge.points[i + 1].x, edge.points[i + 1].z);
                    if (DistanceToSegment(point, a, b) < limit)
                        return true;
                }
            }

            return false;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared) : 0f;

            return Vector2.Distance(point, a + segment * t);
        }
    }
}
