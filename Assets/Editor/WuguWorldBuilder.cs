using System;
using System.Collections.Generic;
using MotorAndSpeed.Cameras;
using MotorAndSpeed.Game;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.UI;
using MotorAndSpeed.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 依照 OpenStreetMap 路網產生五股世界（約 2.2 × 3 公里）。
    /// 建築分三種細節：成泰路完整、其他主要道路中等、巷弄簡化；洲子洋重劃區蓋退縮高樓；成蘆橋蓋前半段高架。
    /// 用佔地格子避免建築互相重疊或壓到道路。
    /// 選單：MotorAndSpeed / 重建五股世界
    /// </summary>
    internal static class WuguWorldBuilder
    {
        internal const string SCENE_PATH = "Assets/Scenes/WuguWorld.unity";
        internal const string WORLD_PATH = "Assets/Data/wugu_world.json";
        private const string MESH_FOLDER = "Assets/Generated/WuguWorld";
        private const string COLLIDER_GROUP = "Colliders";
        private const int SEED = 2028;
        private const float ROAD_Y = 0.02f;
        private const float MARKING_Y = 0.035f;
        // 每 250 × 250 公尺合併成一塊，鏡頭看不到的區塊整塊略過不畫
        private const float CHUNK_SIZE = 250f;
        private const float POLE_SPACING = 30f;
        private const float NEW_TOWN_SIDEWALK = 6f;
        private const float TREE_SPACING = 9f;
        private const float SHOPHOUSE_DEPTH = 12f;
        private const float FOG_START = 150f;
        private const float FOG_END = 520f;
        private const float BRIDGE_STEP = 2f;
        private const float DECK_THICKNESS = 1.4f;
        private const float PARAPET_HEIGHT = 1.1f;
        private const float PILLAR_SPACING = 32f;
        private const float PILLAR_SIZE = 1.8f;

        private static readonly HashSet<string> HeroRoads = new HashSet<string> { "成泰路二段", "成泰路三段" };
        private static readonly string[] FacadeTints = { "#EFE4CF", "#E3D6C3", "#C99A8E", "#F2F0EA", "#CBD6DC", "#E6CCB5", "#C4CCC6" };
        private static readonly string[] TowerTints = { "#FFFFFF", "#EFE6D6", "#D9DEE3" };
        private static readonly string[] CarColors = { "#EDEDED", "#B9BEC4", "#2B2D30", "#B3262E", "#2F5D9A", "#F2C230" };
        private static readonly string[] TruckColors = { "#2F6DB5", "#2E8B57", "#EDEDED", "#C0392B" };
        private static readonly string[] BusColors = { "#2E8B57", "#2F6DB5", "#C0392B" };
        private static readonly string[] NpcScooterColors = { "#F2F2F2", "#C0392B", "#A7ADB2", "#2B2B2B", "#3B6FB6", "#F4A6B8" };
        private static readonly string[] JacketColors = { "#5B7C99", "#C94F4F", "#3D3D3D", "#E0B04A", "#6C9A5B", "#EDEDED" };
        private static readonly string[] HelmetColors = { "#F2F2F2", "#D63B3B", "#2B2B2B", "#F2C94C", "#4A90D9" };
        private const string NPC_FOLDER = "Assets/Prefabs/Npc";
        private static readonly Color FogColor = new Color(0.78f, 0.83f, 0.88f);

        private enum Level
        {
            Hero,
            Main,
            Minor,
        }

        private static WorldData _data;
        private static RoadNetwork _network;
        private static OccupancyGrid _grid;
        private static System.Random _random;
        private static Dictionary<Vector2Int, Transform> _chunks;
        private static Dictionary<string, List<Mesh>> _meshes;
        private static WorldMaterials _materials;
        private static Vector2[] _newTown;
        private static float[] _junctionRadius;
        private static Transform _world;
        private static Transform _markings;
        private static int _buildingCount;

        /// <summary>
        /// 重建五股世界場景並加入建置清單。
        /// </summary>
        [MenuItem("MotorAndSpeed/重建五股世界")]
        public static void Build()
        {
            TextAsset worldAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(WORLD_PATH);
            if (worldAsset == null)
                throw new InvalidOperationException($"找不到 {WORLD_PATH}，請先執行 python tools/build_world_data.py");

            _data = WorldData.FromJson(worldAsset.text);
            _network = new RoadNetwork(_data);
            _random = new System.Random(SEED);
            _chunks = new Dictionary<Vector2Int, Transform>();
            _meshes = new Dictionary<string, List<Mesh>>();
            _buildingCount = 0;
            TaiwanStreetBuilder.Initialize(SEED);
            _materials = new WorldMaterials();
            _newTown = _data.newTown.ConvertAll(p => new Vector2(p.x, p.z)).ToArray();
            AssetDatabase.DeleteAsset(MESH_FOLDER);

            GameObject scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeSceneBuilder.PREFAB_PATH);
            if (scooterPrefab == null)
                scooterPrefab = PrototypeSceneBuilder.BuildScooterPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            _world = new GameObject("WuguWorld").transform;
            _markings = new GameObject("RoadMarkings").transform;
            _markings.SetParent(_world, false);

            GetBounds(out Vector2 min, out Vector2 max);
            _grid = new OccupancyGrid(min, max, 1f);
            ComputeJunctionRadii();
            MarkRoads();

            // 橋墩要在標記橋下空間之前規劃，才知道哪裡是平面道路不能放
            List<Vector3> pillars = PlanBridgePillars();
            MarkBridges();

            BuildGround(min, max);
            BuildRoads();
            BuildMarkings();
            BuildSignals();
            BuildBridges(pillars);

            // 依優先順序蓋房子：成泰路、其他主要道路、巷弄，先蓋的先佔位
            BuildFrontages(Level.Hero);
            BuildFrontages(Level.Main);
            BuildFrontages(Level.Minor);
            BuildHills(min, max);

            foreach (Transform chunk in _chunks.Values)
            {
                MeshTools.Combine(chunk, mesh => Keep(chunk.name, mesh));
            }

            MeshTools.Combine(_markings, mesh => Keep("Markings", mesh));

            PlacePlayerAndCamera(scooterPrefab, worldAsset);
            BuildTraffic(worldAsset);
            BuildGameModes(worldAsset);
            ApplyAtmosphere();

            MeshTools.SaveBundles(MESH_FOLDER, _meshes);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            PrototypeSceneBuilder.AddSceneToBuild(SCENE_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WuguWorldBuilder] 完成：{SCENE_PATH}，路段 {_data.edges.Count} 條、建築 {_buildingCount} 棟、區塊 {_chunks.Count} 個");
        }

        private static void GetBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (EdgeData edge in _data.edges)
            {
                foreach (RoutePoint point in edge.points)
                {
                    min = Vector2.Min(min, new Vector2(point.x, point.z));
                    max = Vector2.Max(max, new Vector2(point.x, point.z));
                }
            }

            foreach (BridgeData bridge in _data.bridges)
            {
                foreach (RoutePoint point in bridge.points)
                {
                    min = Vector2.Min(min, new Vector2(point.x, point.z));
                    max = Vector2.Max(max, new Vector2(point.x, point.z));
                }
            }

            min -= Vector2.one * 80f;
            max += Vector2.one * 80f;
        }

        private static void ComputeJunctionRadii()
        {
            // 路口半徑：接進來最寬的道路一半再加 1 公尺
            _junctionRadius = new float[_data.junctions.Count];
            foreach (EdgeData edge in _data.edges)
            {
                _junctionRadius[edge.start] = Mathf.Max(_junctionRadius[edge.start], edge.width / 2f + 1f);
                _junctionRadius[edge.end] = Mathf.Max(_junctionRadius[edge.end], edge.width / 2f + 1f);
            }
        }

        private static void MarkRoads()
        {
            foreach (EdgeData edge in _data.edges)
            {
                float halfWidth = edge.width / 2f + (edge.main ? 1f : 0.8f);
                for (int i = 0; i < edge.points.Count - 1; i++)
                {
                    Vector2 a = Flat(edge.points[i]);
                    Vector2 b = Flat(edge.points[i + 1]);
                    float length = Vector2.Distance(a, b);
                    if (length < 0.01f)
                        continue;

                    _grid.Mark((a + b) / 2f, b - a, length / 2f + 0.6f, halfWidth);
                }
            }

            foreach (JunctionData junction in _data.junctions)
            {
                _grid.MarkCircle(new Vector2(junction.x, junction.z), _junctionRadius[junction.id] + 1f);
            }
        }

        private static void BuildGround(Vector2 min, Vector2 max)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_world, false);
            Vector2 center = (min + max) / 2f;
            Vector2 size = max - min + Vector2.one * 600f;
            ground.transform.localPosition = new Vector3(center.x, 0f, center.y);
            ground.transform.localScale = new Vector3(size.x / 10f, 1f, size.y / 10f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = _materials.Ground;
        }

        private static void BuildRoads()
        {
            foreach (EdgeData edge in _data.edges)
            {
                RoutePath path = _network.GetEdgePath(edge.id);
                Transform chunk = Chunk(path.GetPoint(path.Length / 2f));
                MeshObject("Road", chunk, MeshTools.Ribbon(path, 0f, path.Length, -edge.width / 2f, edge.width / 2f, ROAD_Y), _materials.Asphalt, true);
            }

            foreach (JunctionData junction in _data.junctions)
            {
                // 路尾（死巷或被地圖邊界切斷）不放護欄：地面本來就可以騎，護欄只會在路中間形成怪牆
                if (junction.edges.Count < 2)
                    continue;

                Vector3 center = new Vector3(junction.x, 0f, junction.z);
                MeshObject("Junction", Chunk(center), MeshTools.Disc(center, _junctionRadius[junction.id] - 1f, ROAD_Y), _materials.Asphalt, true);
            }
        }

        private static RoutePath BridgePath(BridgeData bridge)
        {
            return new RoutePath(bridge.points.ConvertAll(p => new Vector3(p.x, 0f, p.z)));
        }

        // 橋面中線，每 2 公尺一點，高度依引道曲線
        private static List<Vector3> BridgeLine(RoutePath path)
        {
            List<Vector3> line = new List<Vector3>();
            int count = Mathf.CeilToInt(path.Length / BRIDGE_STEP) + 1;
            for (int i = 0; i < count; i++)
            {
                float distance = Mathf.Min(i * BRIDGE_STEP, path.Length);
                line.Add(path.GetPoint(distance) + Vector3.up * (ROAD_Y + BridgeProfile.HeightAt(distance)));
            }

            return line;
        }

        private static List<Vector3> PlanBridgePillars()
        {
            // 橋夠高的地方每隔一段放兩根橋墩；落在平面道路上的跳過，免得擋住底下的車
            List<Vector3> pillars = new List<Vector3>();
            foreach (BridgeData bridge in _data.bridges)
            {
                RoutePath path = BridgePath(bridge);
                for (float s = BridgeProfile.RAMP_LENGTH * 0.45f; s < path.Length - 4f; s += PILLAR_SPACING)
                {
                    Vector3 forward = path.GetTangent(s);
                    foreach (float side in new[] { -bridge.width / 4f, bridge.width / 4f })
                    {
                        Vector3 position = path.GetPoint(s) + path.GetRight(s) * side;
                        if (_grid.IsFree(new Vector2(position.x, position.z), new Vector2(forward.x, forward.z), PILLAR_SIZE, PILLAR_SIZE))
                            pillars.Add(position + Vector3.up * BridgeProfile.HeightAt(s));
                    }
                }
            }

            return pillars;
        }

        private static void MarkBridges()
        {
            // 橋下不蓋房子
            foreach (BridgeData bridge in _data.bridges)
            {
                for (int i = 0; i < bridge.points.Count - 1; i++)
                {
                    Vector2 a = Flat(bridge.points[i]);
                    Vector2 b = Flat(bridge.points[i + 1]);
                    float length = Vector2.Distance(a, b);
                    if (length < 0.01f)
                        continue;

                    _grid.Mark((a + b) / 2f, b - a, length / 2f + 0.6f, bridge.width / 2f + 2f);
                }
            }
        }

        private static void BuildBridges(List<Vector3> pillars)
        {
            Transform root = new GameObject("Bridges").transform;
            root.SetParent(_world, false);
            Material concrete = _materials.Concrete;
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            foreach (BridgeData bridge in _data.bridges)
            {
                RoutePath path = BridgePath(bridge);
                List<Vector3> line = BridgeLine(path);
                float half = bridge.width / 2f;

                // 橋面：薄薄一層柏油（可以騎，碰撞體）蓋在混凝土橋身上
                MeshObject("BridgeRoad", root, MeshTools.ProfileStrip(line, -half, half, -0.05f, 0f), _materials.Asphalt, true);
                MeshObject("BridgeBody", root, MeshTools.ProfileStrip(line, -half, half, -DECK_THICKNESS, -0.05f), concrete, false);

                // 兩側護欄
                MeshObject("Parapet", root, MeshTools.ProfileStrip(line, -half, -half + 0.45f, 0f, PARAPET_HEIGHT), concrete, true);
                MeshObject("Parapet", root, MeshTools.ProfileStrip(line, half - 0.45f, half, 0f, PARAPET_HEIGHT), concrete, true);

                // 標線：中央雙黃線、兩側白色邊線
                foreach (float offset in new[] { -0.2f, 0.2f })
                {
                    MeshObject("BridgeLine", root, MeshTools.ProfileStrip(line, offset - 0.075f, offset + 0.075f, 0f, 0.015f), _materials.Yellow, false);
                }

                foreach (float offset in new[] { -half + 1.4f, half - 1.4f })
                {
                    MeshObject("BridgeLine", root, MeshTools.ProfileStrip(line, offset - 0.075f, offset + 0.075f, 0f, 0.015f), street.LaneLine, false);
                }

                // 只做一半：斷頭處用黃黑斜紋護欄擋住
                float endDistance = path.Length - 0.6f;
                GameObject barrier = MeshObject("BridgeEnd", root, MeshTools.TiledBox(new Vector3(bridge.width, 1.4f, 0.6f), new Vector2(1.4f, 1.4f)), street.PoleStripes, true);
                barrier.transform.SetPositionAndRotation(path.GetPoint(endDistance) + Vector3.up * (ROAD_Y + BridgeProfile.HeightAt(endDistance) + 0.7f), Quaternion.LookRotation(path.GetTangent(endDistance)));
            }

            foreach (Vector3 top in pillars)
            {
                float height = top.y - DECK_THICKNESS + 0.1f;
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "BridgePillar";
                pillar.transform.SetParent(root, false);
                pillar.transform.localPosition = new Vector3(top.x, height / 2f, top.z);
                pillar.transform.localScale = new Vector3(PILLAR_SIZE, height, PILLAR_SIZE);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = concrete;
            }

            MeshTools.Combine(root, mesh => Keep("Bridges", mesh));
        }

        private static void BuildMarkings()
        {
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            foreach (EdgeData edge in _data.edges)
            {
                // 巷弄沒有標線，兩側加水泥路肩，看起來才不會像直接接到草地
                if (!edge.main)
                {
                    RoutePath lane = _network.GetEdgePath(edge.id);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float inner = side * edge.width / 2f;
                        MeshObject("LaneShoulder", Chunk(lane.GetPoint(lane.Length / 2f)), MeshTools.Ribbon(lane, 0f, lane.Length, inner, inner + side * 1.2f, ROAD_Y - 0.005f), _materials.Paving, false);
                    }

                    continue;
                }

                RoutePath path = _network.GetEdgePath(edge.id);
                float half = edge.width / 2f;
                float edgeLine = Mathf.Min(3.4f, half - 2.6f);
                bool startSignal = _data.junctions[edge.start].signal;
                bool endSignal = _data.junctions[edge.end].signal;
                float a = _junctionRadius[edge.start] + (startSignal ? 5.4f : 1f);
                float b = path.Length - _junctionRadius[edge.end] - (endSignal ? 5.4f : 1f);
                bool hero = HeroRoads.Contains(edge.name);

                // 單行道（分隔島兩側的半邊道路）只畫兩側白線，不畫雙黃線與紅線
                if (edge.oneway != 0)
                {
                    if (b - a > 2f)
                    {
                        Stripe(path, a, b, half - 0.3f, 0.15f, street.LaneLine);
                        Stripe(path, a, b, -half + 0.3f, 0.15f, street.LaneLine);
                    }

                    continue;
                }

                if (b - a > 2f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Stripe(path, a, b, side * 0.15f, 0.12f, _materials.Yellow);
                        Stripe(path, a, b, side * edgeLine, 0.12f, street.LaneLine);
                        if (hero)
                        {
                            BuildCurbParking(path, a, b, side, edgeLine + 1.7f, half);
                        }
                        else
                        {
                            Stripe(path, a, b, side * (half - 0.25f), 0.15f, street.RedLine);
                        }
                    }

                    // 路面字：字頭朝向行進方向
                    for (float s = a + 15f; s < b - 10f; s += 80f)
                    {
                        Decal(path, s, edgeLine + 0.8f, path.GetTangent(s), 1.1f, 2.2f, street.ScooterOnlyText);
                        Decal(path, s, -(edgeLine + 0.8f), -path.GetTangent(s), 1.1f, 2.2f, street.ScooterOnlyText);
                    }

                    if (hero)
                    {
                        for (float s = a + 50f; s < b - 15f; s += 150f)
                        {
                            Decal(path, s, 1.7f, path.GetTangent(s), 1.4f, 4.2f, _materials.RouteNumberText);
                            Decal(path, s, -1.7f, -path.GetTangent(s), 1.4f, 4.2f, _materials.RouteNumberText);
                        }
                    }
                }

                if (startSignal)
                    BuildSignalEndMarkings(path, edge, true, half);

                if (endSignal)
                    BuildSignalEndMarkings(path, edge, false, half);
            }
        }

        private static void BuildSignalEndMarkings(RoutePath path, EdgeData edge, bool atStart, float half)
        {
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            float radius = _junctionRadius[atStart ? edge.start : edge.end];

            // 斑馬線與停止線（停止線畫在開往路口那一側的車道上）
            float crossStart = atStart ? radius : path.Length - radius - 4f;
            for (float offset = -half + 0.5f; offset < half - 0.5f; offset += 1f)
            {
                MeshObject("Crosswalk", _markings, MeshTools.Ribbon(path, crossStart, crossStart + 4f, offset, offset + 0.5f, MARKING_Y), street.LaneLine, false);
            }

            if (atStart)
            {
                MeshObject("StopLine", _markings, MeshTools.Ribbon(path, radius + 4.4f, radius + 4.8f, -half + 0.4f, -0.3f, MARKING_Y), street.LaneLine, false);
            }
            else
            {
                MeshObject("StopLine", _markings, MeshTools.Ribbon(path, path.Length - radius - 4.8f, path.Length - radius - 4.4f, 0.3f, half - 0.4f, MARKING_Y), street.LaneLine, false);
            }

            // 機車待轉區：在路口右前方，面向左轉方向
            JunctionData junction = _data.junctions[atStart ? edge.start : edge.end];
            if (junction.edges.Count < 3)
                return;

            Vector3 travel = atStart ? -path.GetTangent(0f) : path.GetTangent(path.Length);
            Vector3 right = new Vector3(travel.z, 0f, -travel.x);
            Vector3 center = new Vector3(junction.x, MARKING_Y, junction.z) + travel * (radius * 0.35f) + right * (half - 1.7f);
            Transform box = new GameObject("WaitBox").transform;
            box.SetParent(_markings, false);
            box.SetPositionAndRotation(center, Quaternion.LookRotation(travel));
            Vector3[] edges =
            {
                new Vector3(-1.2f, 0f, 0f), new Vector3(1.2f, 0f, 0f), new Vector3(0f, 0f, -1.3f), new Vector3(0f, 0f, 1.3f),
            };
            for (int i = 0; i < edges.Length; i++)
            {
                Vector3 size = i < 2 ? new Vector3(0.12f, 0.005f, 2.6f) : new Vector3(2.4f, 0.005f, 0.12f);
                PrototypeSceneBuilder.CreateBlock("WaitBoxEdge", box, edges[i], size, street.LaneLine, false);
            }

            TaiwanStreetBuilder.Quad("WaitText", box, Vector3.up * 0.003f, Quaternion.LookRotation(Vector3.down, Vector3.left), 1.2f, 2.2f, _materials.ScooterWaitText);
        }

        private static void BuildCurbParking(RoutePath path, float s0, float s1, int side, float inner, float half)
        {
            // 路邊一段一段的停車格（大多是機車格，偶爾是汽車格），中間的空檔畫紅線
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            const float segment = 12f;
            const float gap = 9f;
            float outer = half - 0.15f;
            float curbLine = half - 0.25f;
            float s = s0 + 4f;
            Stripe(path, s0, s, side * curbLine, 0.15f, street.RedLine);
            while (s + segment < s1 - 4f)
            {
                bool cars = _random.NextDouble() < 0.25;
                float slot = cars ? 6f : 1.1f;
                Stripe(path, s, s + segment, side * inner, 0.1f, street.LaneLine);
                Stripe(path, s, s + segment, side * outer, 0.1f, street.LaneLine);
                for (float t = s; t <= s + segment + 0.01f; t += slot)
                {
                    MeshObject("ParkingDivider", _markings, MeshTools.Ribbon(path, t, t + 0.08f, side * inner, side * outer, MARKING_Y), street.LaneLine, false);
                }

                for (float t = s; t + slot <= s + segment + 0.01f; t += slot)
                {
                    if (_random.NextDouble() < (cars ? 0.6 : 0.4))
                        ParkAtCurb(path, t + slot / 2f, side, (inner + outer) / 2f, cars);
                }

                Stripe(path, s + segment, Mathf.Min(s + segment + gap, s1), side * curbLine, 0.15f, street.RedLine);
                s += segment + gap;
            }

            Stripe(path, s, s1, side * curbLine, 0.15f, street.RedLine);
        }

        private static void ParkAtCurb(RoutePath path, float s, int side, float offset, bool car)
        {
            Vector3 right = path.GetRight(s);
            Vector3 position = path.GetPoint(s) + right * (side * offset);
            Transform parent = Chunk(position);
            if (car)
            {
                // 汽車順著車流方向停
                Transform parked = PrototypeSceneBuilder.AddModel("car", parent, position);
                parked.localRotation = Quaternion.LookRotation(path.GetTangent(s) * side);
                int color = _random.Next(CarColors.Length);
                Recolor(parked, "car_paint", PrototypeSceneBuilder.GetMaterial($"NpcCarPaint{color}", CarColors[color]));
                BoxCollider body = parked.gameObject.AddComponent<BoxCollider>();
                body.center = new Vector3(0f, 0.75f, 0f);
                body.size = new Vector3(1.8f, 1.5f, 4.5f);

                return;
            }

            // 機車垂直路邊停，車頭朝人行道
            Transform scooter = TaiwanStreetBuilder.ParkScooter(parent, position, Quaternion.LookRotation(right * side));
            BoxCollider collider = scooter.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.6f, 0f);
            collider.size = new Vector3(0.6f, 1.2f, 1.8f);
        }

        private static void BuildSignals()
        {
            Transform signals = new GameObject("TrafficSignals").transform;
            signals.SetParent(_world, false);
            float cycleLength = new TrafficSignalCycle(22f, 12f, 3f, 1.5f).CycleLength;
            foreach (JunctionData junction in _data.junctions)
            {
                if (!junction.signal)
                    continue;

                Vector3 center = new Vector3(junction.x, 0f, junction.z);
                Transform root = new GameObject($"Signal_{junction.id}").transform;
                root.SetParent(signals, false);
                root.localPosition = center;
                float radius = _junctionRadius[junction.id];

                // 每條進路：從路口往外的方向，依軸線分成兩組燈號
                int count = junction.edges.Count;
                Vector2[] outward = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    EdgeData edge = _data.edges[junction.edges[i]];
                    RoutePath path = _network.GetEdgePath(edge.id);
                    Vector3 direction = edge.start == junction.id ? path.GetTangent(0f) : -path.GetTangent(path.Length);
                    outward[i] = new Vector2(direction.x, direction.z);
                }

                int[] phases = SignalPhasing.Group(outward);
                List<SignalHead> mainHeads = new List<SignalHead>();
                List<SignalHead> crossHeads = new List<SignalHead>();
                for (int i = 0; i < count; i++)
                {
                    // 燈頭立在路口對面、開進來的車輛右前方
                    EdgeData edge = _data.edges[junction.edges[i]];
                    Vector3 travel = -new Vector3(outward[i].x, 0f, outward[i].y);
                    Vector3 right = new Vector3(travel.z, 0f, -travel.x);
                    Vector3 poleBase = travel * (radius + 1f) + right * (edge.width / 2f + 0.5f);
                    TaiwanStreetBuilder.Cylinder("SignalPole", root, poleBase + Vector3.up * 2.2f, 0.2f, 4.4f, _materials.SignalPole, true);
                    SignalHead head = BuildHead(root, poleBase + Vector3.up * 4.1f, travel);
                    (phases[i] == 0 ? mainHeads : crossHeads).Add(head);
                }

                TrafficSignal signal = root.gameObject.AddComponent<TrafficSignal>();
                signal.Configure(0f, radius + 5f, (float)_random.NextDouble() * cycleLength, mainHeads.ToArray(), crossHeads.ToArray(),
                    _materials.LampOff, _materials.RedOn, _materials.YellowOn, _materials.GreenOn);
                signal.ConfigureJunction(junction.id, junction.edges.ToArray(), phases);
            }
        }

        private static SignalHead BuildHead(Transform parent, Vector3 position, Vector3 driverForward)
        {
            // 台灣常見的橫式燈頭：從駕駛看過去由左到右是紅、黃、綠，右邊再加倒數秒數
            Quaternion rotation = Quaternion.LookRotation(driverForward);
            PrototypeSceneBuilder.CreateBlock("SignalHousing", parent, position, new Vector3(1.35f, 0.5f, 0.35f), _materials.SignalHousing, false, rotation);
            PrototypeSceneBuilder.CreateBlock("CountdownHousing", parent, position + rotation * new Vector3(0.95f, 0f, 0f), new Vector3(0.55f, 0.5f, 0.35f), _materials.SignalHousing, false, rotation);
            SignalHead head = new SignalHead
            {
                Red = Lamp(parent, position + rotation * new Vector3(-0.42f, 0f, -0.2f)),
                Yellow = Lamp(parent, position + rotation * new Vector3(0f, 0f, -0.2f)),
                Green = Lamp(parent, position + rotation * new Vector3(0.42f, 0f, -0.2f)),
            };

            // 兩位數七段顯示器，每段 (x, y, 寬, 高)，順序 a~g
            Vector4[] layout =
            {
                new Vector4(0f, 0.15f, 0.13f, 0.035f), new Vector4(0.075f, 0.075f, 0.035f, 0.13f),
                new Vector4(0.075f, -0.075f, 0.035f, 0.13f), new Vector4(0f, -0.15f, 0.13f, 0.035f),
                new Vector4(-0.075f, -0.075f, 0.035f, 0.13f), new Vector4(-0.075f, 0.075f, 0.035f, 0.13f),
                new Vector4(0f, 0f, 0.13f, 0.035f),
            };
            Vector3 digits = position + rotation * new Vector3(0.95f, 0f, -0.18f);
            List<Renderer> segments = new List<Renderer>();
            foreach (float digitX in new[] { -0.11f, 0.11f })
            {
                foreach (Vector4 segment in layout)
                {
                    GameObject quad = TaiwanStreetBuilder.Quad("Segment", parent, digits + rotation * new Vector3(digitX + segment.x, segment.y, 0f), rotation, segment.z, segment.w, _materials.LampOff);
                    segments.Add(quad.GetComponent<MeshRenderer>());
                }
            }

            head.Segments = segments.ToArray();

            return head;
        }

        private static Renderer Lamp(Transform parent, Vector3 position)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Lamp";
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = position;
            lamp.transform.localScale = Vector3.one * 0.3f;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            MeshRenderer renderer = lamp.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _materials.LampOff;

            return renderer;
        }

        private static void BuildFrontages(Level level)
        {
            foreach (EdgeData edge in _data.edges)
            {
                if (LevelOf(edge) != level)
                    continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    BuildEdgeFrontage(edge, side, level);
                }
            }
        }

        private static void BuildEdgeFrontage(EdgeData edge, int side, Level level)
        {
            RoutePath path = _network.GetEdgePath(edge.id);
            float s0 = _junctionRadius[edge.start] + 1.5f;
            float s1 = path.Length - _junctionRadius[edge.end] - 1.5f;
            if (s1 - s0 < 5f)
                return;

            float half = edge.width / 2f;
            float facade = half + 1f;
            StreetProfile profile = new StreetProfile(half, half + 0.35f, facade);

            // 主要道路的洲子洋重劃區路段：退縮高樓、寬人行道、行道樹
            if (level != Level.Minor)
                BuildNewTownTowers(path, s0, s1, side, half);

            List<float> poles = new List<float>();
            if (level != Level.Minor)
            {
                for (float s = s0 + 5f; s < s1; s += POLE_SPACING)
                {
                    if (!InNewTown(path, s, side, facade + 8f))
                        poles.Add(s);
                }

                BuildUtilityPoles(path, poles, side, profile.PoleX);
            }

            float minWidth = level == Level.Minor ? 6f : 4.5f;
            float maxWidth = level == Level.Minor ? 12f : 9f;

            // 太短的巷段連一間房子都放不下
            if (s1 - s0 < minWidth)
                return;

            float z = s0;
            foreach (float width in StreetLayout.SplitFrontage(s1 - s0, minWidth, Mathf.Max(maxWidth, minWidth * 2f), _random))
            {
                float center = z + width / 2f;
                z += width;
                bool newTown = InNewTown(path, center, side, facade + 8f);
                if (newTown && level != Level.Minor)
                    continue;

                float depth = level == Level.Minor ? 9f : SHOPHOUSE_DEPTH;
                Vector3 forward3 = path.GetTangent(center);
                Vector2 forward = new Vector2(forward3.x, forward3.z);
                Vector3 right = path.GetRight(center);
                Vector3 point = path.GetPoint(center);
                Vector2 footprint = Flat(point + right * (side * (facade + depth / 2f)));
                if (!_grid.IsFree(footprint, forward, width / 2f - 0.05f, depth / 2f))
                {
                    // 巷弄空間不夠時試試看淺一點的房子
                    if (level != Level.Minor)
                        continue;

                    depth = 6f;
                    footprint = Flat(point + right * (side * (facade + depth / 2f)));
                    if (!_grid.IsFree(footprint, forward, width / 2f - 0.05f, depth / 2f))
                        continue;
                }

                _grid.Mark(footprint, forward, width / 2f, depth / 2f);
                Transform lot = new GameObject("Lot").transform;
                lot.SetParent(Chunk(point), false);
                lot.SetPositionAndRotation(point, Quaternion.LookRotation(forward3));
                float z0 = -width / 2f;
                float z1 = width / 2f;
                switch (level)
                {
                    case Level.Hero:
                        TaiwanStreetBuilder.BuildShophouse(lot, side, z0, z1, profile, pillarZ => poles.Exists(pole => Mathf.Abs(pole - (center + pillarZ)) < 1.5f));
                        PrototypeSceneBuilder.CreateBlock("ArcadeFloor", lot, new Vector3(side * (half + facade + TaiwanStreetBuilder.ARCADE_DEPTH) / 2f, TaiwanStreetBuilder.SIDEWALK_TOP / 2f, 0f),
                            new Vector3(facade + TaiwanStreetBuilder.ARCADE_DEPTH - half, TaiwanStreetBuilder.SIDEWALK_TOP, width), TaiwanStreetBuilder.Materials.ArcadeFloor, true);
                        break;
                    case Level.Main:
                        BuildMediumShophouse(lot, side, z0, z1, half, facade);
                        break;
                    default:
                        BuildSimpleBuilding(lot, side, width, facade, depth, newTown);
                        break;
                }

                _buildingCount++;
            }
        }

        private static void BuildMediumShophouse(Transform lot, int side, float z0, float z1, float curb, float facade)
        {
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            const float arcadeHeight = 3.6f;
            int floors = _random.Next(3, 6);
            float upperHeight = (floors - 1) * 3.2f;
            float width = z1 - z0;
            Quaternion facing = Quaternion.Euler(0f, 90f * side, 0f);

            // 樓上：整面重複窗戶貼圖的外牆，一個方塊取代幾十個零件
            Vector3 upperSize = new Vector3(SHOPHOUSE_DEPTH, upperHeight, width);
            GameObject upper = MeshObject("UpperFloors", lot, MeshTools.TiledBox(upperSize, new Vector2(3f, 3.2f)), TaiwanStreetBuilder.Pick(_materials.FacadeTiles), false);
            upper.transform.localPosition = new Vector3(side * (facade + SHOPHOUSE_DEPTH / 2f), arcadeHeight + upperHeight / 2f, 0f);
            upper.AddComponent<BoxCollider>().size = upperSize;

            float groundDepth = SHOPHOUSE_DEPTH - TaiwanStreetBuilder.ARCADE_DEPTH;
            PrototypeSceneBuilder.CreateBlock("GroundFloor", lot, new Vector3(side * (facade + TaiwanStreetBuilder.ARCADE_DEPTH + groundDepth / 2f), (TaiwanStreetBuilder.SIDEWALK_TOP + arcadeHeight) / 2f, 0f),
                new Vector3(groundDepth, arcadeHeight - TaiwanStreetBuilder.SIDEWALK_TOP, width), street.GroundWall, true);
            PrototypeSceneBuilder.CreateBlock("Pillar", lot, new Vector3(side * (facade + 0.3f), arcadeHeight / 2f, z0 + 0.3f), new Vector3(0.6f, arcadeHeight, 0.6f), street.Facades[0], true);
            PrototypeSceneBuilder.CreateBlock("ArcadeFloor", lot, new Vector3(side * (curb + facade + TaiwanStreetBuilder.ARCADE_DEPTH) / 2f, TaiwanStreetBuilder.SIDEWALK_TOP / 2f, 0f),
                new Vector3(facade + TaiwanStreetBuilder.ARCADE_DEPTH - curb, TaiwanStreetBuilder.SIDEWALK_TOP, width), street.ArcadeFloor, true);
            TaiwanStreetBuilder.Quad("Storefront", lot, new Vector3(side * (facade + TaiwanStreetBuilder.ARCADE_DEPTH - 0.02f), TaiwanStreetBuilder.SIDEWALK_TOP + 1.55f, 0.3f), facing, width - 1.2f, 3f, TaiwanStreetBuilder.Pick(street.Storefronts));

            // 騎樓上方的橫式招牌
            PrototypeSceneBuilder.CreateBlock("SignBoard", lot, new Vector3(side * (facade - 0.15f), arcadeHeight - 0.28f, 0f), new Vector3(0.3f, 1.25f, width - 0.4f), street.SignFrame, false);
            TaiwanStreetBuilder.Quad("SignFace", lot, new Vector3(side * (facade - 0.31f), arcadeHeight - 0.28f, 0f), facing, width - 0.5f, 1.15f, TaiwanStreetBuilder.Pick(street.HorizontalSigns));

            if (_random.NextDouble() < 0.4)
            {
                float roof = arcadeHeight + upperHeight;
                PrototypeSceneBuilder.CreateBlock("RooftopShed", lot, new Vector3(side * (facade + SHOPHOUSE_DEPTH / 2f + 0.5f), roof + 1.2f, 0f), new Vector3(SHOPHOUSE_DEPTH - 2f, 2.4f, width - 0.6f), TaiwanStreetBuilder.Pick(street.Sheds), false);
            }
        }

        private static void BuildSimpleBuilding(Transform lot, int side, float width, float facade, float depth, bool newTown)
        {
            // 巷弄公寓：一塊重複窗戶貼圖的方塊，一樓有些是店面
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            int floors = newTown ? _random.Next(7, 13) : _random.Next(3, 6);
            float height = floors * 3.1f;
            Vector3 size = new Vector3(depth, height, width - 0.3f);
            GameObject block = MeshObject("Apartment", lot, MeshTools.TiledBox(size, new Vector2(3f, 3.1f)), TaiwanStreetBuilder.Pick(_materials.ApartmentTiles), false);
            block.transform.localPosition = new Vector3(side * (facade + depth / 2f), height / 2f, 0f);
            block.AddComponent<BoxCollider>().size = size;

            Quaternion facing = Quaternion.Euler(0f, 90f * side, 0f);
            if (_random.NextDouble() < 0.5)
            {
                float shopWidth = Mathf.Min(width - 1f, 5f);
                TaiwanStreetBuilder.Quad("Storefront", lot, new Vector3(side * (facade - 0.02f), 1.5f, 0f), facing, shopWidth, 2.8f, TaiwanStreetBuilder.Pick(street.Storefronts));
                if (_random.NextDouble() < 0.6)
                    TaiwanStreetBuilder.Quad("ShopSign", lot, new Vector3(side * (facade - 0.03f), 3.4f, 0f), facing, Mathf.Min(width - 0.8f, 4f), 0.8f, TaiwanStreetBuilder.Pick(street.HorizontalSigns));
            }
        }

        private static void BuildNewTownTowers(RoutePath path, float s0, float s1, int side, float half)
        {
            float front = half + NEW_TOWN_SIDEWALK;
            float z = s0 + 2f;
            while (z < s1 - 15f)
            {
                float width = Mathf.Min(20f + (float)_random.NextDouble() * 14f, s1 - 2f - z);
                if (width < 15f)
                    break;

                float center = z + width / 2f;
                z += width + 6f + (float)_random.NextDouble() * 6f;
                if (!InNewTown(path, center, side, front + 9f))
                    continue;

                const float depth = 18f;
                Vector3 forward3 = path.GetTangent(center);
                Vector3 point = path.GetPoint(center);
                Vector2 footprint = Flat(point + path.GetRight(center) * (side * (front + depth / 2f)));
                Vector2 forward = new Vector2(forward3.x, forward3.z);
                if (!_grid.IsFree(footprint, forward, width / 2f, depth / 2f))
                    continue;

                _grid.Mark(footprint, forward, width / 2f, depth / 2f);
                Transform lot = new GameObject("TowerLot").transform;
                lot.SetParent(Chunk(point), false);
                lot.SetPositionAndRotation(point, Quaternion.LookRotation(forward3));
                BuildTower(lot, side, -width / 2f, width / 2f, front);

                // 寬人行道與行道樹
                PrototypeSceneBuilder.CreateBlock("NewTownSidewalk", lot, new Vector3(side * (half + NEW_TOWN_SIDEWALK / 2f), TaiwanStreetBuilder.SIDEWALK_TOP / 2f, 0f),
                    new Vector3(NEW_TOWN_SIDEWALK, TaiwanStreetBuilder.SIDEWALK_TOP, width + 6f), _materials.Paving, true);
                for (float t = -width / 2f; t <= width / 2f + 3f; t += TREE_SPACING)
                {
                    Transform tree = PrototypeSceneBuilder.AddModel("tree", lot, new Vector3(side * (half + 1.3f), TaiwanStreetBuilder.SIDEWALK_TOP, t));
                    tree.localRotation = Quaternion.Euler(0f, (float)_random.NextDouble() * 360f, 0f);
                    CapsuleCollider trunk = tree.gameObject.AddComponent<CapsuleCollider>();
                    trunk.center = new Vector3(0f, 1.2f, 0f);
                    trunk.radius = 0.2f;
                    trunk.height = 2.4f;
                }

                _buildingCount++;
            }
        }

        private static void BuildTower(Transform lot, int side, float z0, float z1, float front)
        {
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            const float podiumHeight = 5f;
            const float podiumDepth = 18f;
            PrototypeSceneBuilder.CreateBlock("Podium", lot, new Vector3(side * (front + podiumDepth / 2f), podiumHeight / 2f, (z0 + z1) / 2f), new Vector3(podiumDepth, podiumHeight, z1 - z0), _materials.Podium, true);

            // 基座的店面與招牌
            Quaternion facing = Quaternion.Euler(0f, 90f * side, 0f);
            for (float z = z0 + 3.5f; z < z1 - 3f; z += 6f)
            {
                TaiwanStreetBuilder.Quad("Storefront", lot, new Vector3(side * (front - 0.02f), TaiwanStreetBuilder.SIDEWALK_TOP + 1.7f, z), facing, 5f, 3.2f, TaiwanStreetBuilder.Pick(street.Storefronts));
                TaiwanStreetBuilder.Quad("ShopSign", lot, new Vector3(side * (front - 0.03f), 4.25f, z), facing, 4.4f, 0.9f, TaiwanStreetBuilder.Pick(street.HorizontalSigns));
            }

            // 住宅大樓本體：外牆貼圖依公尺重複，窗戶大小才會一致
            int floors = _random.Next(12, 21);
            float height = floors * 3.1f;
            const float towerDepth = 15f;
            Vector3 towerSize = new Vector3(towerDepth, height, z1 - z0 - 2f);
            Vector3 towerCenter = new Vector3(side * (front + 3f + towerDepth / 2f), podiumHeight + height / 2f, (z0 + z1) / 2f);
            GameObject tower = MeshObject("Tower", lot, MeshTools.TiledBox(towerSize, new Vector2(3f, 3.1f)), TaiwanStreetBuilder.Pick(_materials.TowerFacades), false);
            tower.transform.localPosition = towerCenter;
            tower.AddComponent<BoxCollider>().size = towerSize;

            float roof = podiumHeight + height;
            PrototypeSceneBuilder.CreateBlock("RoofCap", lot, new Vector3(towerCenter.x, roof + 0.15f, towerCenter.z), new Vector3(towerDepth + 0.4f, 0.3f, towerSize.z + 0.4f), _materials.RoofCap, false);
            PrototypeSceneBuilder.CreateBlock("RoofRoom", lot, new Vector3(towerCenter.x, roof + 1.8f, towerCenter.z), new Vector3(5f, 3f, 6f), _materials.Podium, false);
        }

        private static void BuildUtilityPoles(RoutePath path, List<float> poles, int side, float poleOffset)
        {
            TaiwanStreetBuilder.Palette street = TaiwanStreetBuilder.Materials;
            Vector3? previousTop = null;
            float previousS = float.NegativeInfinity;
            foreach (float s in poles)
            {
                Vector3 basePoint = path.GetPoint(s) + path.GetRight(s) * (side * poleOffset);
                Transform parent = Chunk(basePoint);
                float y = TaiwanStreetBuilder.SIDEWALK_TOP;
                TaiwanStreetBuilder.Cylinder("Pole", parent, basePoint + Vector3.up * (y + 4.5f), 0.28f, 9f, street.Concrete, true);
                TaiwanStreetBuilder.Cylinder("WarningSleeve", parent, basePoint + Vector3.up * (y + 1f), 0.32f, 2f, street.PoleStripes, false);

                // 電線接到前一根電線桿（中間被重劃區隔開時不接）
                Vector3 top = basePoint + Vector3.up * 8.4f;
                if (previousTop.HasValue && s - previousS < POLE_SPACING * 1.5f)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 drop = Vector3.down * (i * 0.35f);
                        TaiwanStreetBuilder.Line("Wire", parent, previousTop.Value + drop, top + drop, street.Wire);
                    }
                }

                previousTop = top;
                previousS = s;
            }
        }

        private static void BuildHills(Vector2 min, Vector2 max)
        {
            // 西側的五股山區：沒有道路的空地密集放置互相重疊的山丘，連成一片山
            // 每座山丘的半徑不超過它自己那塊空地，所以不會壓到道路或建築
            Transform hills = new GameObject("Hills").transform;
            hills.SetParent(_world, false);
            const float spacing = 70f;
            const float radius = 70f;
            for (float x = min.x; x < max.x; x += spacing)
            {
                for (float z = min.y; z < max.y; z += spacing)
                {
                    Vector2 center = new Vector2(x + (float)(_random.NextDouble() - 0.5) * 20f, z + (float)(_random.NextDouble() - 0.5) * 20f);
                    if (center.x > -300f || !_grid.IsFree(center, Vector2.right, radius, radius))
                        continue;

                    GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    hill.name = "Hill";
                    hill.transform.SetParent(hills, false);
                    float height = 50f + (float)_random.NextDouble() * 70f;
                    hill.transform.localPosition = new Vector3(center.x, -height * 0.15f, center.y);
                    hill.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
                    hill.GetComponent<MeshRenderer>().sharedMaterial = TaiwanStreetBuilder.Pick(_materials.Hills);
                }
            }

            MeshTools.Combine(hills, mesh => Keep("Hills", mesh));
        }

        private static void PlacePlayerAndCamera(GameObject scooterPrefab, TextAsset worldAsset)
        {
            Vector3 start = new Vector3(_data.start.x, 0.05f, _data.start.z);
            Vector3 direction = new Vector3(_data.start.dirX, 0f, _data.start.dirZ).normalized;
            GameObject scooter = (GameObject)PrefabUtility.InstantiatePrefab(scooterPrefab);
            scooter.transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction));
            ScooterController controller = scooter.GetComponent<ScooterController>();

            Camera camera = Camera.main;
            camera.transform.position = start - direction * 4.5f + Vector3.up * 1.8f;
            camera.transform.LookAt(start + Vector3.up * 0.9f);
            camera.farClipPlane = FOG_END + 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
            PrototypeSceneBuilder.SetReference(camera.gameObject.AddComponent<ChaseCamera>(), "_target", controller);
            PrototypeSceneBuilder.SetReference(camera.gameObject.AddComponent<SpeedHud>(), "_target", controller);
            camera.gameObject.AddComponent<MapAttribution>();
            PrototypeSceneBuilder.SetReference(camera.gameObject.AddComponent<MiniMap>(), "_worldJson", worldAsset);
        }

        private static void BuildTraffic(TextAsset worldAsset)
        {
            WorldVehicle scooter = BuildNpcPrefab("NpcScooter", new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 1.5f, 1.9f), 12f, 2.5f, 3.5f, 5f, 1.9f, 0.6f,
                new PaintSpec("body", "NpcScooterBody", NpcScooterColors),
                new PaintSpec("rider_jacket", "NpcJacket", JacketColors),
                new PaintSpec("rider_helmet", "NpcHelmet", HelmetColors));
            // 8+9：騎得比一般機車快、起步猛；改裝車、破爛車兩種
            WorldVehicle bajiuTuned = BuildNpcPrefab("NpcScooterBajiu", new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 1.5f, 1.9f), 15f, 3f, 4.5f, 5f, 1.9f, 0.6f,
                new PaintSpec("body", "NpcScooterBody", NpcScooterColors));
            WorldVehicle bajiuJunk = BuildNpcPrefab("NpcScooterBajiuJunk", new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 1.5f, 1.9f), 14f, 3f, 4f, 5f, 1.9f, 0.6f,
                new PaintSpec("body", "NpcScooterBody", NpcScooterColors));
            WorldVehicle car = BuildNpcPrefab("NpcCar", new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 1.5f, 4.5f), 13f, 3f, 2.5f, 5f, 4.5f, 0.7f,
                new PaintSpec("car_paint", "NpcCarPaint", CarColors));
            WorldVehicle truck = BuildNpcPrefab("NpcTruck", new Vector3(0f, 1.7f, 0f), new Vector3(2.5f, 3.4f, 8.6f), 10f, 1.5f, 1.5f, 4f, 8.6f, 1.2f,
                new PaintSpec("truck_cab", "NpcTruckCab", TruckColors));
            WorldVehicle bus = BuildNpcPrefab("NpcBus", new Vector3(0f, 1.6f, 0f), new Vector3(2.5f, 3.2f, 10.5f), 9f, 1f, 1.2f, 3.5f, 10.5f, 1.2f,
                new PaintSpec("bus_paint", "NpcBusPaint", BusColors));

            WorldTraffic traffic = new GameObject("Traffic").AddComponent<WorldTraffic>();
            PrototypeSceneBuilder.SetReference(traffic, "_worldJson", worldAsset);
            PrototypeSceneBuilder.SetReference(traffic, "_scooterPrefab", scooter);
            SerializedObject trafficSettings = new SerializedObject(traffic);
            SerializedProperty bajiuPrefabs = trafficSettings.FindProperty("_bajiuScooterPrefabs");
            bajiuPrefabs.arraySize = 2;
            bajiuPrefabs.GetArrayElementAtIndex(0).objectReferenceValue = bajiuTuned;
            bajiuPrefabs.GetArrayElementAtIndex(1).objectReferenceValue = bajiuJunk;
            trafficSettings.ApplyModifiedPropertiesWithoutUndo();
            PrototypeSceneBuilder.SetReference(traffic, "_carPrefab", car);
            PrototypeSceneBuilder.SetReference(traffic, "_truckPrefab", truck);
            PrototypeSceneBuilder.SetReference(traffic, "_busPrefab", bus);
        }

        private static void BuildGameModes(TextAsset worldAsset)
        {
            // 三位 AI 選手：(車身, 外套, 安全帽, 最高速)
            (string Body, string Jacket, string Helmet, float MaxSpeed)[] racers =
            {
                ("#C0392B", "#2B2B2B", "#F2F2F2", 26f),
                ("#2F6DB5", "#F2C94C", "#2F6DB5", 25.5f),
                ("#2E8B57", "#EDEDED", "#D63B3B", 26.5f),
            };
            PrototypeSceneBuilder.EnsureFolder("Assets/Prefabs/Racers");
            GameModeController controller = new GameObject("GameModes").AddComponent<GameModeController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("_worldJson").objectReferenceValue = worldAsset;
            serialized.FindProperty("_deliveryBeacon").objectReferenceValue = WorldMaterials.Emissive("BeaconDelivery", "#FF4040");
            serialized.FindProperty("_pickupBeacon").objectReferenceValue = WorldMaterials.Emissive("BeaconPickup", "#40E070");
            serialized.FindProperty("_fenceSign").objectReferenceValue = TaiwanStreetBuilder.Textured("RaceFenceSign", "race_chevron");
            serialized.FindProperty("_fenceFrame").objectReferenceValue = PrototypeSceneBuilder.GetMaterial("RaceFenceFrame", "#E8E8E2");
            SerializedProperty prefabs = serialized.FindProperty("_racerPrefabs");
            prefabs.arraySize = racers.Length;
            for (int i = 0; i < racers.Length; i++)
            {
                GameObject root = new GameObject($"Racer{i}");
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.75f, 0f);
                collider.size = new Vector3(0.6f, 1.5f, 1.9f);
                Transform scooterBody = PrototypeSceneBuilder.AddModel("scooter_body", root.transform, Vector3.zero);
                Transform rider = PrototypeSceneBuilder.AddModel("npc_rider", root.transform, Vector3.zero);
                PrototypeSceneBuilder.AddModel("scooter_handlebar", root.transform, new Vector3(0f, 0.98f, 0.52f));
                PrototypeSceneBuilder.AddModel("scooter_wheel", root.transform, new Vector3(0f, 0.24f, 0.65f));
                PrototypeSceneBuilder.AddModel("scooter_wheel", root.transform, new Vector3(0f, 0.24f, -0.65f));
                Recolor(scooterBody, "body", PrototypeSceneBuilder.GetMaterial($"Racer{i}Body", racers[i].Body));
                Recolor(rider, "rider_jacket", PrototypeSceneBuilder.GetMaterial($"Racer{i}Jacket", racers[i].Jacket));
                Recolor(rider, "rider_helmet", PrototypeSceneBuilder.GetMaterial($"Racer{i}Helmet", racers[i].Helmet));
                RacerAI racer = root.AddComponent<RacerAI>();
                SerializedObject racerSettings = new SerializedObject(racer);
                racerSettings.FindProperty("_maxSpeed").floatValue = racers[i].MaxSpeed;
                racerSettings.ApplyModifiedPropertiesWithoutUndo();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Prefabs/Racers/Racer{i}.prefab");
                Object.DestroyImmediate(root);
                prefabs.GetArrayElementAtIndex(i).objectReferenceValue = prefab.GetComponent<RacerAI>();
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static WorldVehicle BuildNpcPrefab(string name, Vector3 colliderCenter, Vector3 colliderSize, float cruise, float variance, float acceleration, float deceleration, float length, float sensorHeight, params PaintSpec[] paints)
        {
            PrototypeSceneBuilder.EnsureFolder(NPC_FOLDER);
            GameObject root = new GameObject(name);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = colliderCenter;
            collider.size = colliderSize;

            if (name.StartsWith("NpcScooter"))
            {
                string scooterBody = name switch
                {
                    "NpcScooterBajiu" => "bajiu_scooter_tuned",
                    "NpcScooterBajiuJunk" => "bajiu_scooter_junk",
                    _ => "scooter_body",
                };
                PrototypeSceneBuilder.AddModel(scooterBody, root.transform, Vector3.zero);
                PrototypeSceneBuilder.AddModel(name.StartsWith("NpcScooterBajiu") ? "bajiu_rider" : "npc_rider", root.transform, Vector3.zero);
                PrototypeSceneBuilder.AddModel("scooter_handlebar", root.transform, new Vector3(0f, 0.98f, 0.52f));
                PrototypeSceneBuilder.AddModel("scooter_wheel", root.transform, new Vector3(0f, 0.24f, 0.65f));
                PrototypeSceneBuilder.AddModel("scooter_wheel", root.transform, new Vector3(0f, 0.24f, -0.65f));
            }
            else
            {
                string model = name switch
                {
                    "NpcCar" => "car",
                    "NpcTruck" => "truck",
                    _ => "bus",
                };
                PrototypeSceneBuilder.AddModel(model, root.transform, Vector3.zero);
            }

            WorldVehicle vehicle = root.AddComponent<WorldVehicle>();
            SerializedObject serialized = new SerializedObject(vehicle);
            serialized.FindProperty("_cruiseSpeed").floatValue = cruise;
            serialized.FindProperty("_speedVariance").floatValue = variance;
            serialized.FindProperty("_acceleration").floatValue = acceleration;
            serialized.FindProperty("_deceleration").floatValue = deceleration;
            serialized.FindProperty("_length").floatValue = length;
            serialized.FindProperty("_sensorHeight").floatValue = sensorHeight;
            SerializedProperty slots = serialized.FindProperty("_paintSlots");
            slots.arraySize = paints.Length;
            for (int i = 0; i < paints.Length; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("MaterialName").stringValue = paints[i].MaterialName;
                SerializedProperty options = slot.FindPropertyRelative("Options");
                options.arraySize = paints[i].Colors.Length;
                for (int j = 0; j < paints[i].Colors.Length; j++)
                {
                    options.GetArrayElementAtIndex(j).objectReferenceValue = PrototypeSceneBuilder.GetMaterial($"{paints[i].Prefix}{j}", paints[i].Colors[j]);
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{NPC_FOLDER}/{name}.prefab");
            Object.DestroyImmediate(root);

            return prefab.GetComponent<WorldVehicle>();
        }

        private static void ApplyAtmosphere()
        {
            // 霧可以讓遠處自然淡出，也讓相機不用畫太遠，減輕顯示卡負擔
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = FOG_START;
            RenderSettings.fogEndDistance = FOG_END;
            RenderSettings.fogColor = FogColor;
        }

        private static Level LevelOf(EdgeData edge)
        {
            if (HeroRoads.Contains(edge.name))
                return Level.Hero;

            return edge.main ? Level.Main : Level.Minor;
        }

        private static bool InNewTown(RoutePath path, float s, int side, float offset)
        {
            return Polygon.Contains(_newTown, Flat(path.GetPoint(s) + path.GetRight(s) * (side * offset)));
        }

        private static Transform Chunk(Vector3 position)
        {
            Vector2Int key = new Vector2Int(Mathf.FloorToInt(position.x / CHUNK_SIZE), Mathf.FloorToInt(position.z / CHUNK_SIZE));
            if (!_chunks.TryGetValue(key, out Transform chunk))
            {
                chunk = new GameObject($"Chunk_{key.x}_{key.y}").transform;
                chunk.SetParent(_world, false);
                _chunks[key] = chunk;
            }

            return chunk;
        }

        private static void Keep(string group, Mesh mesh)
        {
            if (!_meshes.TryGetValue(group, out List<Mesh> list))
            {
                list = new List<Mesh>();
                _meshes[group] = list;
            }

            list.Add(mesh);
        }

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material, bool withCollider)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (withCollider)
            {
                // 碰撞體會一直參考這個網格，要存進資源檔
                Keep(COLLIDER_GROUP, mesh);
                item.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            return item;
        }

        private static void Stripe(RoutePath path, float s0, float s1, float offset, float width, Material material)
        {
            if (s1 - s0 < 0.1f)
                return;

            MeshObject("Stripe", _markings, MeshTools.Ribbon(path, s0, s1, offset - width / 2f, offset + width / 2f, MARKING_Y), material, false);
        }

        private static void Decal(RoutePath path, float s, float offset, Vector3 textUp, float width, float height, Material material)
        {
            Vector3 position = path.GetPoint(s) + path.GetRight(s) * offset + Vector3.up * (MARKING_Y + 0.003f);
            TaiwanStreetBuilder.Quad("RoadText", _markings, position, Quaternion.LookRotation(Vector3.down, textUp), width, height, material);
        }

        private static void Recolor(Transform model, string materialName, Material color)
        {
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name == materialName)
                        materials[i] = color;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Vector2 Flat(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }

        private static Vector2 Flat(RoutePoint point)
        {
            return new Vector2(point.x, point.z);
        }

        private class PaintSpec
        {
            public readonly string MaterialName;
            public readonly string Prefix;
            public readonly string[] Colors;

            public PaintSpec(string materialName, string prefix, string[] colors)
            {
                MaterialName = materialName;
                Prefix = prefix;
                Colors = colors;
            }
        }

        private class WorldMaterials
        {
            public readonly Material Asphalt = PrototypeSceneBuilder.GetMaterial("Asphalt", "#4A4D52");
            public readonly Material Ground = PrototypeSceneBuilder.GetMaterial("WorldGround", "#8B8F82");
            public readonly Material Yellow = PrototypeSceneBuilder.GetMaterial("CenterLine", "#F2C230");
            public readonly Material Curb = PrototypeSceneBuilder.GetMaterial("CurbStone", "#B9B6AE");
            public readonly Material Concrete = PrototypeSceneBuilder.GetMaterial("BridgeConcrete", "#A9A9A3");
            public readonly Material SignalPole = PrototypeSceneBuilder.GetMaterial("SignalPole", "#8C9196");
            public readonly Material SignalHousing = PrototypeSceneBuilder.GetMaterial("SignalHousing", "#1E1F21");
            public readonly Material LampOff = PrototypeSceneBuilder.GetMaterial("LampOff", "#2A2C2E");
            public readonly Material RedOn = Emissive("LampRed", "#FF3B30");
            public readonly Material YellowOn = Emissive("LampYellow", "#FFC21A");
            public readonly Material GreenOn = Emissive("LampGreen", "#2EE87A");
            public readonly Material ScooterWaitText = TaiwanStreetBuilder.Textured("Street_road_scooter_wait", "road_scooter_wait", true);
            public readonly Material RouteNumberText = TaiwanStreetBuilder.Textured("Street_road_route_107", "road_route_107", true);
            public readonly Material Paving = PrototypeSceneBuilder.GetMaterial("NewTownPaving", "#C9C2B6");
            public readonly Material Podium = PrototypeSceneBuilder.GetMaterial("TowerPodium", "#D5D3CE");
            public readonly Material RoofCap = PrototypeSceneBuilder.GetMaterial("TowerRoof", "#5A6168");
            public readonly List<Material> Hills = new List<Material>
            {
                PrototypeSceneBuilder.GetMaterial("Hill0", "#5F7D4C"),
                PrototypeSceneBuilder.GetMaterial("Hill1", "#6B8A56"),
                PrototypeSceneBuilder.GetMaterial("Hill2", "#577246"),
            };
            public readonly List<Material> TowerFacades = TintedSet("TowerFacade", "tower_window", TowerTints);
            public readonly List<Material> FacadeTiles = Combined(TintedSet("FacadeGrill", "facade_tile_grill", FacadeTints), TintedSet("FacadeCurtain", "facade_tile_curtain", FacadeTints));
            public readonly List<Material> ApartmentTiles = TintedSet("FacadeApartment", "facade_tile_apartment", FacadeTints);

            private static List<Material> Combined(List<Material> first, List<Material> second)
            {
                first.AddRange(second);

                return first;
            }

            private static List<Material> TintedSet(string prefix, string textureName, string[] tints)
            {
                // 同一張窗戶貼圖，用不同底色做出不同外牆
                List<Material> materials = new List<Material>();
                for (int i = 0; i < tints.Length; i++)
                {
                    Material material = TaiwanStreetBuilder.Textured($"{prefix}{i}", textureName);
                    ColorUtility.TryParseHtmlString(tints[i], out Color tint);
                    material.SetColor("_BaseColor", tint);
                    EditorUtility.SetDirty(material);
                    materials.Add(material);
                }

                return materials;
            }

            public static Material Emissive(string name, string hexColor)
            {
                Material material = PrototypeSceneBuilder.GetMaterial(name, hexColor);
                ColorUtility.TryParseHtmlString(hexColor, out Color color);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.5f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(material);

                return material;
            }
        }
    }
}
