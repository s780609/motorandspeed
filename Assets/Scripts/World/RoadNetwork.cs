using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 路網：用路口與路段組成的圖，提供最短路徑（導航）與最近路口查詢。
    /// </summary>
    public class RoadNetwork
    {
        // adjacency[路口] = (相鄰路口, 路段編號, 長度)
        private readonly List<(int Neighbor, int Edge, float Length)>[] _adjacency;
        private readonly RoutePath[] _paths;

        public WorldData Data { get; }

        public RoadNetwork(WorldData data)
        {
            Data = data;
            int junctionCount = 0;
            foreach (JunctionData junction in data.junctions)
            {
                junctionCount = Mathf.Max(junctionCount, junction.id + 1);
            }

            _adjacency = new List<(int, int, float)>[junctionCount];
            for (int i = 0; i < junctionCount; i++)
            {
                _adjacency[i] = new List<(int, int, float)>();
            }

            _paths = new RoutePath[data.edges.Count];
            foreach (EdgeData edge in data.edges)
            {
                _paths[edge.id] = RoutePath.FromRoadLine(new RoadLine { points = edge.points });
                float length = _paths[edge.id].Length;
                _adjacency[edge.start].Add((edge.end, edge.id, length));
                _adjacency[edge.end].Add((edge.start, edge.id, length));
            }
        }

        /// <summary>
        /// 取得路段的路徑（從 start 路口走到 end 路口）。
        /// </summary>
        public RoutePath GetEdgePath(int edgeId)
        {
            return _paths[edgeId];
        }

        /// <summary>
        /// 找出離 position 最近的路口。
        /// </summary>
        public int FindNearestJunction(Vector3 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            foreach (JunctionData junction in Data.junctions)
            {
                float dx = junction.x - position.x;
                float dz = junction.z - position.z;
                float distance = dx * dx + dz * dz;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = junction.id;
                }
            }

            return best;
        }

        /// <summary>
        /// 兩個路口之間的最短路徑，回傳依序經過的路段；起訖相同回傳空清單，走不到回傳 null。
        /// </summary>
        public List<int> FindPath(int fromJunction, int toJunction)
        {
            // Dijkstra：路口數只有幾百個，用簡單的線性找最小值就夠快
            int count = _adjacency.Length;
            float[] cost = new float[count];
            int[] previousEdge = new int[count];
            bool[] done = new bool[count];
            for (int i = 0; i < count; i++)
            {
                cost[i] = float.MaxValue;
                previousEdge[i] = -1;
            }

            cost[fromJunction] = 0f;
            while (true)
            {
                int current = -1;
                for (int i = 0; i < count; i++)
                {
                    if (!done[i] && cost[i] < float.MaxValue && (current < 0 || cost[i] < cost[current]))
                        current = i;
                }

                if (current < 0 || current == toJunction)
                    break;

                done[current] = true;
                foreach ((int neighbor, int edge, float length) in _adjacency[current])
                {
                    if (!done[neighbor] && cost[current] + length < cost[neighbor])
                    {
                        cost[neighbor] = cost[current] + length;
                        previousEdge[neighbor] = edge;
                    }
                }
            }

            if (cost[toJunction] == float.MaxValue)
                return null;

            List<int> path = new List<int>();
            int junction = toJunction;
            while (junction != fromJunction)
            {
                int edge = previousEdge[junction];
                path.Add(edge);
                junction = OtherEnd(edge, junction);
            }

            path.Reverse();

            return path;
        }

        /// <summary>
        /// 把一串路段依行進方向接成折線點（導航線）。
        /// </summary>
        public List<Vector3> ChainPoints(int fromJunction, IReadOnlyList<int> edgeIds)
        {
            List<Vector3> points = new List<Vector3>();
            int junction = fromJunction;
            foreach (int edgeId in edgeIds)
            {
                EdgeData edge = Data.edges[edgeId];
                bool forward = edge.start == junction;
                int count = edge.points.Count;
                for (int i = 0; i < count; i++)
                {
                    RoutePoint point = edge.points[forward ? i : count - 1 - i];

                    // 路段接點與上一段的終點重複，略過
                    if (points.Count > 0 && i == 0)
                        continue;

                    points.Add(new Vector3(point.x, 0f, point.z));
                }

                junction = OtherEnd(edgeId, junction);
            }

            return points;
        }

        /// <summary>
        /// 取得路段另一端的路口。
        /// </summary>
        public int OtherEnd(int edgeId, int junction)
        {
            EdgeData edge = Data.edges[edgeId];

            return edge.start == junction ? edge.end : edge.start;
        }
    }
}
