using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    // 對應 tools/build_route_data.py 產生的 JSON，欄位名稱需與 JSON 完全相同

    /// <summary>
    /// 真實路線資料（主線、巷子、路口）。
    /// </summary>
    [Serializable]
    public class RouteData
    {
        public string attribution;
        public RoadLine main;
        public List<RoadLine> lanes;
        public List<IntersectionData> intersections;
        public StartData start;

        /// <summary>
        /// 從 JSON 文字讀取路線資料。
        /// </summary>
        public static RouteData FromJson(string json)
        {
            return JsonUtility.FromJson<RouteData>(json);
        }
    }

    [Serializable]
    public class RoadLine
    {
        public string name;
        public float width;
        public List<RoutePoint> points;
        // 玩家路線在主線上的起訖距離
        public float routeStart;
        public float routeEnd;
    }

    [Serializable]
    public class RoutePoint
    {
        public float x;
        public float z;
    }

    [Serializable]
    public class IntersectionData
    {
        public string name;
        // 路口在主線上的距離
        public float s;
        public float x;
        public float z;
        public bool signal;
        public List<SideRoadData> roads;
    }

    [Serializable]
    public class SideRoadData
    {
        public string name;
        public float width;
        // 從路口往外的方向
        public float dirX;
        public float dirZ;
        // 是否為玩家路線上的巷子（已另外畫出，不需再畫支線）
        public bool route;
    }

    [Serializable]
    public class StartData
    {
        public float x;
        public float z;
        public float dirX;
        public float dirZ;
    }
}
