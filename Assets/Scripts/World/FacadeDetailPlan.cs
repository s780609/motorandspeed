using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>建築外觀細節的種類（對應建置時用的材質）。</summary>
    public enum DetailKind
    {
        Parapet,
        Pillar,
        Cage,
        CageRoof,
        AirConditioner,
        WaterTank,
        SolarPanel,
        Shed,
        Antenna,
        Awning,
    }

    /// <summary>一個細節零件：中心與尺寸（建築的標準座標，見 FacadeDetailPlan）。</summary>
    public struct DetailPart
    {
        public DetailKind Kind;
        public Vector3 Center;
        public Vector3 Size;

        public DetailPart(DetailKind kind, Vector3 center, Vector3 size)
        {
            Kind = kind;
            Center = center;
            Size = size;
        }
    }

    /// <summary>
    /// 計算建築外觀細節的零件清單（純計算，方便測試）。
    /// 標準座標：外牆在 x = facade，建築往 +x 延伸 depth 公尺，寬度在 z ∈ [z0, z1]，y 朝上；
    /// 呼叫端依建築在道路哪一側把 x 乘上 side。零件不加碰撞體，數量有上限以控制世界資料大小。
    /// </summary>
    public static class FacadeDetailPlan
    {
        /// <summary>冷氣、鐵窗的最低高度：不會擋到騎樓或人行道上騎士的頭。</summary>
        public const float MIN_WALL_DETAIL_HEIGHT = 2.5f;
        public const int MAX_MAIN_PARTS = 12;
        public const int MAX_MINOR_PARTS = 5;
        public const int MAX_TOWER_PARTS = 8;

        private const float FLOOR_HEIGHT = 3.2f;

        /// <summary>
        /// 主要道路街屋：女兒牆、另一側騎樓柱、樓上鐵窗與冷氣、屋頂水塔或太陽能熱水器。
        /// arcadeHeight 是騎樓高度（樓上從這裡開始），roof 是屋頂高度。
        /// </summary>
        public static List<DetailPart> MainShophouse(float facade, float depth, float z0, float z1, float arcadeHeight, float roof, System.Random random)
        {
            List<DetailPart> parts = new List<DetailPart>
            {
                new DetailPart(DetailKind.Parapet, new Vector3(facade + 0.125f, roof + 0.5f, (z0 + z1) / 2f), new Vector3(0.25f, 1f, z1 - z0)),
                new DetailPart(DetailKind.Pillar, new Vector3(facade + 0.3f, arcadeHeight / 2f, z1 - 0.3f), new Vector3(0.6f, arcadeHeight, 0.6f)),
            };

            int upperFloors = Mathf.Max(1, Mathf.FloorToInt((roof - arcadeHeight) / FLOOR_HEIGHT));
            AddWallDetails(parts, facade, z0, z1, arcadeHeight, upperFloors, 2, 2, random);

            if (random.NextDouble() < 0.3)
                parts.Add(new DetailPart(DetailKind.WaterTank, new Vector3(facade + depth * 0.7f, roof + 0.8f, z0 + (z1 - z0) * 0.3f), new Vector3(1.4f, 1.6f, 1.4f)));

            if (random.NextDouble() < 0.15)
                parts.Add(new DetailPart(DetailKind.SolarPanel, new Vector3(facade + depth * 0.45f, roof + 0.9f, (z0 + z1) / 2f), new Vector3(1.8f, 0.08f, Mathf.Min(2.4f, z1 - z0 - 0.6f))));

            return parts;
        }

        /// <summary>
        /// 巷弄公寓：女兒牆、冷氣、屋頂水塔或鐵皮加蓋。height 是屋頂高度。
        /// </summary>
        public static List<DetailPart> SimpleBuilding(float facade, float depth, float z0, float z1, float height, System.Random random)
        {
            List<DetailPart> parts = new List<DetailPart>
            {
                new DetailPart(DetailKind.Parapet, new Vector3(facade + 0.125f, height + 0.45f, (z0 + z1) / 2f), new Vector3(0.25f, 0.9f, z1 - z0)),
            };

            // 一樓窗戶從 2.1 公尺起，最上層的鐵窗（含雨遮約 2.4 公尺）不能超出屋頂
            int floors = Mathf.Max(1, Mathf.FloorToInt((height - 4.5f) / FLOOR_HEIGHT) + 1);
            AddWallDetails(parts, facade, z0, z1, 0f, floors, 0, 1, random);

            double roll = random.NextDouble();
            if (roll < 0.4)
                parts.Add(new DetailPart(DetailKind.WaterTank, new Vector3(facade + depth * 0.6f, height + 0.8f, z0 + (z1 - z0) * 0.7f), new Vector3(1.4f, 1.6f, 1.4f)));
            else if (roll < 0.6)
                parts.Add(new DetailPart(DetailKind.Shed, new Vector3(facade + depth * 0.65f, height + 1.2f, (z0 + z1) / 2f), new Vector3(depth * 0.5f, 2.4f, (z1 - z0) * 0.7f)));

            return parts;
        }

        /// <summary>
        /// 重劃區大樓：屋頂女兒牆一圈、天線或水塔、基座雨遮。
        /// front 是基座外牆，tower 的範圍是 x ∈ [towerX0, towerX1]、z ∈ [z0, z1]，roof 是屋頂高度，podiumHeight 是基座高度。
        /// </summary>
        public static List<DetailPart> Tower(float front, float towerX0, float towerX1, float z0, float z1, float roof, float podiumHeight, System.Random random)
        {
            float centerX = (towerX0 + towerX1) / 2f;
            float centerZ = (z0 + z1) / 2f;
            float depth = towerX1 - towerX0;
            float width = z1 - z0;
            List<DetailPart> parts = new List<DetailPart>
            {
                new DetailPart(DetailKind.Parapet, new Vector3(towerX0 + 0.15f, roof + 0.6f, centerZ), new Vector3(0.3f, 1.2f, width)),
                new DetailPart(DetailKind.Parapet, new Vector3(towerX1 - 0.15f, roof + 0.6f, centerZ), new Vector3(0.3f, 1.2f, width)),
                new DetailPart(DetailKind.Parapet, new Vector3(centerX, roof + 0.6f, z0 + 0.15f), new Vector3(depth, 1.2f, 0.3f)),
                new DetailPart(DetailKind.Parapet, new Vector3(centerX, roof + 0.6f, z1 - 0.15f), new Vector3(depth, 1.2f, 0.3f)),
                new DetailPart(DetailKind.Awning, new Vector3(front - 0.75f, podiumHeight - 1.45f, centerZ), new Vector3(1.5f, 0.12f, width)),
            };

            if (random.NextDouble() < 0.5)
                parts.Add(new DetailPart(DetailKind.Antenna, new Vector3(centerX + depth * 0.25f, roof + 3.5f, centerZ - width * 0.25f), new Vector3(0.12f, 7f, 0.12f)));
            else
                parts.Add(new DetailPart(DetailKind.WaterTank, new Vector3(centerX - depth * 0.25f, roof + 1.2f, centerZ + width * 0.25f), new Vector3(2.4f, 2.4f, 2.4f)));

            return parts;
        }

        private static void AddWallDetails(List<DetailPart> parts, float facade, float z0, float z1, float baseHeight, int floors, int maxCages, int maxAirConditioners, System.Random random)
        {
            // 窗戶位置：每 3 公尺一格，鐵窗與冷氣隨機挑格子和樓層，不重疊
            int slots = Mathf.Max(1, Mathf.FloorToInt((z1 - z0) / 3f));
            HashSet<int> used = new HashSet<int>();
            int cages = random.Next(0, maxCages + 1);
            int airConditioners = random.Next(maxAirConditioners > 0 ? 1 : 0, maxAirConditioners + 1);
            for (int i = 0; i < cages + airConditioners; i++)
            {
                int floor = random.Next(floors);
                int slot = random.Next(slots);
                if (!used.Add(floor * 100 + slot))
                    continue;

                float z = z0 + (slot + 0.5f) * (z1 - z0) / slots;
                float floorY = Mathf.Max(baseHeight, MIN_WALL_DETAIL_HEIGHT - 0.4f) + floor * FLOOR_HEIGHT;
                if (i < cages)
                {
                    parts.Add(new DetailPart(DetailKind.Cage, new Vector3(facade - 0.25f, floorY + 1.6f, z), new Vector3(0.5f, 1.4f, 1.4f)));
                    parts.Add(new DetailPart(DetailKind.CageRoof, new Vector3(facade - 0.3f, floorY + 2.34f, z), new Vector3(0.6f, 0.08f, 1.6f)));
                }
                else
                {
                    parts.Add(new DetailPart(DetailKind.AirConditioner, new Vector3(facade - 0.22f, floorY + 0.7f, z), new Vector3(0.44f, 0.6f, 0.8f)));
                }
            }
        }
    }
}
