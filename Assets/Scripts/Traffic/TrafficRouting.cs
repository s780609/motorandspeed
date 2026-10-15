using System.Collections.Generic;
using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// NPC 在路口選下一條路、以及過路口的轉彎曲線（純邏輯，方便單元測試）。
    /// </summary>
    public static class TrafficRouting
    {
        private const int CURVE_SEGMENTS = 16;

        /// <summary>
        /// 開到路口時隨機選下一條路段：不走回頭路、遵守單行道；沒有其他路可走時才迴轉。
        /// </summary>
        /// <param name="data">路網資料</param>
        /// <param name="junction">抵達的路口</param>
        /// <param name="arrivingEdge">開過來的路段</param>
        /// <param name="mainOnly">只走主要道路</param>
        /// <param name="random">亂數來源</param>
        /// <param name="forward">回傳是否沿路段 start → end 方向行駛</param>
        /// <returns>下一條路段編號</returns>
        public static int ChooseNextEdge(WorldData data, int junction, int arrivingEdge, bool mainOnly, System.Random random, out bool forward)
        {
            List<EdgeData> candidates = new List<EdgeData>();
            foreach (int edgeId in data.junctions[junction].edges)
            {
                EdgeData edge = data.edges[edgeId];
                if (edgeId != arrivingEdge && (!mainOnly || edge.main) && CanEnter(edge, junction))
                    candidates.Add(edge);
            }

            // 沒路可走（死路或只剩逆向單行道）就迴轉
            EdgeData chosen = candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : data.edges[arrivingEdge];
            forward = chosen.start == junction;

            return chosen.id;
        }

        /// <summary>
        /// 能不能從某個路口開進這條路段（單行道限制）。
        /// </summary>
        public static bool CanEnter(EdgeData edge, int fromJunction)
        {
            return fromJunction == edge.start ? edge.oneway != -1 : edge.oneway != 1;
        }

        /// <summary>
        /// 二次貝茲曲線上的點。
        /// </summary>
        public static Vector3 Bezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float u = 1f - t;

            return u * u * start + 2f * u * t * control + t * t * end;
        }

        /// <summary>
        /// 二次貝茲曲線的近似長度（切成數段直線相加）。
        /// </summary>
        public static float BezierLength(Vector3 start, Vector3 control, Vector3 end)
        {
            float length = 0f;
            Vector3 previous = start;
            for (int i = 1; i <= CURVE_SEGMENTS; i++)
            {
                Vector3 point = Bezier(start, control, end, i / (float)CURVE_SEGMENTS);
                length += Vector3.Distance(previous, point);
                previous = point;
            }

            return length;
        }
    }
}
