using System.Collections.Generic;
using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Game
{
    /// <summary>
    /// 一道比賽圍欄：擋在賽道路口的支線入口，面向路口那一面印著指向比賽方向的箭頭。
    /// </summary>
    public struct FencePlacement
    {
        public int Junction;
        public int Edge;
        public Vector3 Position;
        // 從路口往支線外的方向
        public Vector3 Outward;
        public float Width;
        // 站在路口面向圍欄時，箭頭是否指向右邊
        public bool ArrowsRight;
    }

    /// <summary>
    /// 比賽圍欄規劃：找出賽道經過的路口，把不屬於賽道的支線入口圍起來（純邏輯，方便單元測試）。
    /// </summary>
    public static class RaceFencing
    {
        /// <summary>
        /// 規劃整條賽道需要的圍欄。
        /// </summary>
        public static List<FencePlacement> Plan(RoadNetwork network, RoutePath track)
        {
            List<FencePlacement> fences = new List<FencePlacement>();
            WorldData data = network.Data;
            foreach (JunctionData junction in data.junctions)
            {
                if (junction.edges.Count < 2)
                    continue;

                // 賽道有經過這個路口嗎？
                Vector3 center = new Vector3(junction.x, 0f, junction.z);
                float along = track.GetClosestDistance(center);
                if (Vector3.Distance(track.GetPoint(along), center) > 3f)
                    continue;

                Vector3 raceDirection = track.GetTangent(along);
                float radius = JunctionRadius(data, junction);
                foreach (int edgeId in junction.edges)
                {
                    EdgeData edge = data.edges[edgeId];
                    RoutePath path = network.GetEdgePath(edgeId);
                    bool fromStart = edge.start == junction.id;

                    // 沿著這條路往外看一段：還貼著賽道就是賽道本身，不用圍
                    float probe = Mathf.Min(radius + 8f, path.Length * 0.5f);
                    if (DistanceToTrack(track, PointFromJunction(path, fromStart, probe)) < 4f)
                        continue;

                    Vector3 position = PointFromJunction(path, fromStart, Mathf.Min(radius + 1f, path.Length * 0.5f));
                    Vector3 outward = position - center;
                    outward.y = 0f;
                    outward.Normalize();
                    Vector3 right = new Vector3(outward.z, 0f, -outward.x);
                    fences.Add(new FencePlacement
                    {
                        Junction = junction.id,
                        Edge = edgeId,
                        Position = position,
                        Outward = outward,
                        Width = edge.width + 1f,
                        ArrowsRight = Vector3.Dot(raceDirection, right) >= 0f,
                    });
                }
            }

            return fences;
        }

        // 路口半徑：接進來最寬的道路一半再加 1 公尺（與場景建置相同）
        private static float JunctionRadius(WorldData data, JunctionData junction)
        {
            float radius = 0f;
            foreach (int edgeId in junction.edges)
            {
                radius = Mathf.Max(radius, data.edges[edgeId].width / 2f + 1f);
            }

            return radius;
        }

        private static Vector3 PointFromJunction(RoutePath path, bool fromStart, float distance)
        {
            return path.GetPoint(fromStart ? distance : path.Length - distance);
        }

        private static float DistanceToTrack(RoutePath track, Vector3 point)
        {
            return Vector3.Distance(track.GetPoint(track.GetClosestDistance(point)), point);
        }
    }
}
