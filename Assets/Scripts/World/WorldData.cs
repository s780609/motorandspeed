using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    // 對應 tools/build_world_data.py 產生的 JSON，欄位名稱需與 JSON 完全相同

    /// <summary>
    /// 五股世界路網：路段、路口、高架橋、重劃區範圍、送貨地點、賽道。
    /// </summary>
    [Serializable]
    public class WorldData
    {
        public string attribution;
        public List<JunctionData> junctions;
        public List<EdgeData> edges;
        // 高架橋不在平面路網裡，另外蓋成可騎的橋面
        public List<BridgeData> bridges = new List<BridgeData>();
        public List<RoutePoint> newTown;
        public List<DestinationData> destinations;
        public PathData sprint;
        public PathData circuit;
        public StartData start;

        /// <summary>
        /// 從 JSON 文字讀取世界資料。
        /// </summary>
        public static WorldData FromJson(string json)
        {
            return JsonUtility.FromJson<WorldData>(json);
        }
    }

    [Serializable]
    public class JunctionData
    {
        public int id;
        public float x;
        public float z;
        public bool signal;
        public List<int> edges;
    }

    [Serializable]
    public class EdgeData
    {
        public int id;
        // 路段兩端的路口編號
        public int start;
        public int end;
        public string name;
        // 主要道路（有標線、騎樓街屋）或巷弄
        public bool main;
        public float width;
        // 1：只能從 start 往 end；-1：只能反向；0：雙向
        public int oneway;
        public float length;
        public List<RoutePoint> points;
    }

    [Serializable]
    public class BridgeData
    {
        public string name;
        // 整座橋面寬度（雙向合在一起）
        public float width;
        // 橋面中線，從接地的那一端開始
        public List<RoutePoint> points;
    }

    [Serializable]
    public class DestinationData
    {
        public string name;
        public float x;
        public float z;
        public int edge;
    }

    [Serializable]
    public class PathData
    {
        public List<RoutePoint> points;
    }
}
