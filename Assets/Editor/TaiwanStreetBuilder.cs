using System;
using System.Collections.Generic;
using MotorAndSpeed.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 道路斷面：路緣、電線桿、建築正面（騎樓柱子外緣）離道路中心的距離。
    /// </summary>
    internal struct StreetProfile
    {
        public float CurbX;
        public float PoleX;
        public float FacadeX;

        public StreetProfile(float curbX, float poleX, float facadeX)
        {
            CurbX = curbX;
            PoleX = poleX;
            FacadeX = facadeX;
        }
    }

    /// <summary>
    /// 在起點直線兩側蓋台灣街景：騎樓、招牌、鐵窗、冷氣、電線桿、路邊停的機車、路面標字。
    /// 右側 side = 1（+X），左側 side = -1，所有 X 座標都以「離道路中心的距離」表示。
    /// 建築都用 parent 的區域座標建立，所以也能沿著彎曲道路擺放（見 WuguWorldBuilder）。
    /// </summary>
    internal static class TaiwanStreetBuilder
    {
        internal const float ARCADE_DEPTH = 3.5f;
        internal const float BUILDING_DEPTH = 12f;
        internal const float SIDEWALK_TOP = 0.15f;

        private const string TEXTURE_FOLDER = "Assets/Textures/Street";
        private const int SEED = 2026;

        private const float STREET_START_Z = -12f;
        private const float STREET_END_Z = 172f;
        private const float ROAD_TOP = 0.02f;
        private const float CURB_X = 6f;
        private const float POLE_X = 6.35f;
        private const float ARCADE_HEIGHT = 3.6f;
        private const float FLOOR_HEIGHT = 3.2f;
        private const float POLE_SPACING = 22f;

        private static readonly string[] FacadeColors =
        {
            "#D9CBB0", "#C7B9A6", "#A9746E", "#E6E2D8", "#B8C4C9", "#D8B9A0", "#9FA8A3",
        };

        private static readonly string[] ShedColors = { "#5E7FA3", "#9C5B3B", "#B0B6BA" };

        private static readonly string[] ScooterColors =
        {
            "#F2F2F2", "#C0392B", "#A7ADB2", "#2B2B2B", "#F4A6B8", "#F2C94C",
        };

        private static readonly StreetProfile LoopProfile = new StreetProfile(CURB_X, POLE_X, 7f);

        private static System.Random _random;
        private static Palette _palette;
        private static StreetProfile _profile;

        /// <summary>
        /// 準備亂數與材質，在呼叫 BuildShophouse 之前執行。
        /// </summary>
        public static void Initialize(int seed)
        {
            _random = new System.Random(seed);
            _palette = new Palette();
        }

        /// <summary>
        /// 在 parent 底下建立整條街。
        /// </summary>
        public static void Build(Transform parent)
        {
            Initialize(SEED);
            _profile = LoopProfile;
            Transform street = new GameObject("TaiwanStreet").transform;
            street.SetParent(parent, false);

            for (int side = -1; side <= 1; side += 2)
            {
                List<float> polesZ = BuildPoles(street, side);
                BuildRoadMarkings(street, side);
                Box("ArcadeFloor", street, side, CURB_X, _profile.FacadeX + ARCADE_DEPTH, 0f, SIDEWALK_TOP, STREET_START_Z, STREET_END_Z, _palette.ArcadeFloor, true);

                float z = STREET_START_Z;
                foreach (float width in StreetLayout.SplitFrontage(STREET_END_Z - STREET_START_Z, 4.5f, 9f, _random))
                {
                    BuildShophouse(street, side, z, z + width, LoopProfile, pillarZ => polesZ.Exists(poleZ => Mathf.Abs(poleZ - pillarZ) < 1.5f));
                    z += width;
                }

                // 最後一棟的另一側也要有柱子
                Box("Pillar", street, side, _profile.FacadeX, _profile.FacadeX + 0.6f, SIDEWALK_TOP, ARCADE_HEIGHT, STREET_END_Z - 0.6f, STREET_END_Z, _palette.Facades[0], true);
            }

            // 整條街都不會動，標成靜態物件讓 Unity 合併繪製，減輕顯示卡負擔
            foreach (Transform child in street.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>
        /// 在 parent 的區域座標裡蓋一棟騎樓街屋：z0~z1 為沿街範圍，side 為道路哪一側。
        /// </summary>
        /// <param name="isNearPole">傳入柱子位置，回傳附近是否有電線桿（有的話不掛直立招牌）</param>
        public static void BuildShophouse(Transform parent, int side, float z0, float z1, StreetProfile profile, Func<float, bool> isNearPole)
        {
            _profile = profile;
            Transform house = new GameObject("Shophouse").transform;
            house.SetParent(parent, false);
            int floors = _random.Next(3, 6);
            float height = ARCADE_HEIGHT + (floors - 1) * FLOOR_HEIGHT;
            float width = z1 - z0;
            float centerZ = (z0 + z1) / 2f;
            Material facade = Pick(_palette.Facades);

            // 二樓以上蓋在騎樓上方，一樓店面往內縮，形成騎樓
            Box("UpperFloors", house, side, _profile.FacadeX, _profile.FacadeX + BUILDING_DEPTH, ARCADE_HEIGHT, height, z0, z1, facade, true);
            Box("GroundFloor", house, side, _profile.FacadeX + ARCADE_DEPTH, _profile.FacadeX + BUILDING_DEPTH, SIDEWALK_TOP, ARCADE_HEIGHT, z0, z1, _palette.GroundWall, true);
            Box("Pillar", house, side, _profile.FacadeX, _profile.FacadeX + 0.6f, SIDEWALK_TOP, ARCADE_HEIGHT, z0, z0 + 0.6f, facade, true);
            FacadeQuad("Storefront", house, side, _profile.FacadeX + ARCADE_DEPTH - 0.02f, SIDEWALK_TOP + 1.55f, centerZ + 0.3f, width - 1.2f, 3f, Pick(_palette.Storefronts));

            BuildUpperFloors(house, side, z0, z1, floors);
            BuildHorizontalSign(house, side, z0, z1);

            if (floors >= 4 && !isNearPole(z0 + 0.6f) && _random.NextDouble() < 0.6)
                BuildVerticalSign(house, side, z0 + 0.6f, height);

            BuildRooftop(house, side, z0, z1, height);
            BuildParkedScooters(house, side, z0, z1);
        }

        private static void BuildUpperFloors(Transform house, int side, float z0, float z1, int floors)
        {
            float width = z1 - z0;
            int windowCount = width >= 5.5f ? 2 : 1;
            for (int floor = 1; floor < floors; floor++)
            {
                float floorBase = ARCADE_HEIGHT + (floor - 1) * FLOOR_HEIGHT;

                // 每層樓之間的水平飾帶
                Box("FloorBand", house, side, _profile.FacadeX - 0.08f, _profile.FacadeX, floorBase - 0.1f, floorBase + 0.1f, z0, z1, _palette.Band, false);

                for (int i = 0; i < windowCount; i++)
                {
                    float windowZ = z0 + width * (i + 1) / (windowCount + 1);
                    FacadeQuad("Window", house, side, _profile.FacadeX - 0.02f, floorBase + 1.7f, windowZ, 1.6f, 1.4f, Pick(_palette.Windows));
                    Box("Sill", house, side, _profile.FacadeX - 0.12f, _profile.FacadeX, floorBase + 0.9f, floorBase + 0.98f, windowZ - 0.85f, windowZ + 0.85f, _palette.Band, false);

                    // 凸出牆面的鐵窗（台灣老公寓常見）
                    if (_random.NextDouble() < 0.5)
                        BuildWindowCage(house, side, floorBase, windowZ);

                    // 窗下的冷氣室外機
                    if (_random.NextDouble() < 0.4)
                    {
                        Box("AirConditioner", house, side, _profile.FacadeX - 0.35f, _profile.FacadeX, floorBase + 0.25f, floorBase + 0.8f, windowZ - 0.4f, windowZ + 0.4f, _palette.AirConditionerBody, false);
                        FacadeQuad("AirConditionerFront", house, side, _profile.FacadeX - 0.36f, floorBase + 0.525f, windowZ, 0.8f, 0.55f, _palette.AirConditionerFront);
                    }
                }
            }
        }

        private static void BuildWindowCage(Transform house, int side, float floorBase, float windowZ)
        {
            float front = _profile.FacadeX - 0.42f;
            float bottom = floorBase + 0.95f;
            float top = floorBase + 2.5f;

            // 上方斜鐵皮雨遮、底板、前方直條與中間橫桿
            Box("CageRoof", house, side, front - 0.08f, _profile.FacadeX, top, top + 0.06f, windowZ - 0.95f, windowZ + 0.95f, _palette.CageRoof, false);
            Box("CageFloor", house, side, front, _profile.FacadeX, bottom - 0.05f, bottom, windowZ - 0.9f, windowZ + 0.9f, _palette.CageMetal, false);
            for (float z = -0.88f; z <= 0.89f; z += 0.44f)
            {
                Box("CageBar", house, side, front, front + 0.04f, bottom, top, windowZ + z - 0.02f, windowZ + z + 0.02f, _palette.CageMetal, false);
            }

            Box("CageRail", house, side, front, front + 0.04f, (bottom + top) / 2f - 0.02f, (bottom + top) / 2f + 0.02f, windowZ - 0.9f, windowZ + 0.9f, _palette.CageMetal, false);
            for (int end = -1; end <= 1; end += 2)
            {
                Box("CageSide", house, side, front, _profile.FacadeX, bottom, top, windowZ + end * 0.9f - 0.02f, windowZ + end * 0.9f + 0.02f, _palette.CageMetal, false);
            }
        }

        private static void BuildHorizontalSign(Transform house, int side, float z0, float z1)
        {
            float bottom = ARCADE_HEIGHT - 0.9f;
            float top = ARCADE_HEIGHT + 0.35f;
            Box("SignBoard", house, side, _profile.FacadeX - 0.3f, _profile.FacadeX, bottom, top, z0 + 0.2f, z1 - 0.2f, _palette.SignFrame, false);
            FacadeQuad("SignFace", house, side, _profile.FacadeX - 0.31f, (bottom + top) / 2f, (z0 + z1) / 2f, z1 - z0 - 0.5f, top - bottom - 0.1f, Pick(_palette.HorizontalSigns));
        }

        private static void BuildVerticalSign(Transform house, int side, float z, float buildingHeight)
        {
            // 直立招牌垂直於牆面，讓路上的騎士從前後方都看得到
            float bottom = ARCADE_HEIGHT + 0.6f;
            float top = Mathf.Min(buildingHeight - 0.3f, bottom + 4.4f);
            Box("VerticalSign", house, side, _profile.FacadeX - 0.95f, _profile.FacadeX, bottom, top, z - 0.1f, z + 0.1f, _palette.SignFrame, false);
            Material face = Pick(_palette.VerticalSigns);
            Vector3 center = new Vector3(side * (_profile.FacadeX - 0.5f), (bottom + top) / 2f, z);
            Quad("SignFaceFront", house, center + Vector3.back * 0.11f, Quaternion.identity, 0.85f, top - bottom - 0.1f, face);
            Quad("SignFaceBack", house, center + Vector3.forward * 0.11f, Quaternion.Euler(0f, 180f, 0f), 0.85f, top - bottom - 0.1f, face);
        }

        private static void BuildRooftop(Transform house, int side, float z0, float z1, float height)
        {
            float roofTop = height;

            // 頂樓鐵皮加蓋
            if (_random.NextDouble() < 0.5)
            {
                roofTop = height + 2.4f;
                Box("RooftopShed", house, side, _profile.FacadeX + 1.5f, _profile.FacadeX + BUILDING_DEPTH - 0.5f, height, roofTop, z0 + 0.3f, z1 - 0.3f, Pick(_palette.Sheds), false);
                Box("ShedRoof", house, side, _profile.FacadeX + 1.3f, _profile.FacadeX + BUILDING_DEPTH - 0.3f, roofTop, roofTop + 0.15f, z0 + 0.1f, z1 - 0.1f, _palette.ShedRoof, false);
                roofTop += 0.15f;
            }

            if (_random.NextDouble() < 0.4)
            {
                float z = (z0 + z1) / 2f;
                Box("WaterTank", house, side, _profile.FacadeX + BUILDING_DEPTH - 2.4f, _profile.FacadeX + BUILDING_DEPTH - 1.2f, roofTop, roofTop + 1.2f, z - 0.6f, z + 0.6f, _palette.WaterTank, false);
            }
        }

        private static void BuildParkedScooters(Transform house, int side, float z0, float z1)
        {
            int count = _random.Next(0, 4);
            for (int i = 0; i < count; i++)
            {
                float z = z0 + 1.2f + i * 0.85f;
                if (z > z1 - 0.6f)
                    break;

                // 車頭朝向馬路停放
                ParkScooter(house, new Vector3(side * (_profile.FacadeX + 1.9f), SIDEWALK_TOP, z), Quaternion.Euler(0f, -90f * side, 0f));
            }
        }

        /// <summary>
        /// 在 parent 的區域座標放一台隨機顏色的停放機車。
        /// </summary>
        internal static Transform ParkScooter(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            Transform scooter = new GameObject("ParkedScooter").transform;
            scooter.SetParent(parent, false);
            scooter.SetLocalPositionAndRotation(localPosition, localRotation);

            Transform body = PrototypeSceneBuilder.AddModel("scooter_body", scooter, Vector3.zero);
            PrototypeSceneBuilder.AddModel("scooter_handlebar", scooter, new Vector3(0f, 0.98f, 0.52f));
            PrototypeSceneBuilder.AddModel("scooter_wheel", scooter, new Vector3(0f, 0.24f, 0.65f));
            PrototypeSceneBuilder.AddModel("scooter_wheel", scooter, new Vector3(0f, 0.24f, -0.65f));
            RecolorBody(body, Pick(_palette.ScooterBodies));

            return scooter;
        }

        private static List<float> BuildPoles(Transform parent, int side)
        {
            List<float> polesZ = new List<float>();
            Transform poles = new GameObject("UtilityPoles").transform;
            poles.SetParent(parent, false);

            // 左右兩側的電線桿錯開，看起來比較自然
            for (float z = STREET_START_Z + (side > 0 ? 8f : 19f); z < STREET_END_Z - 4f; z += POLE_SPACING)
            {
                polesZ.Add(z);
                float x = side * POLE_X;
                Cylinder("Pole", poles, new Vector3(x, SIDEWALK_TOP + 4.5f, z), 0.28f, 9f, _palette.Concrete, true);
                Cylinder("WarningSleeve", poles, new Vector3(x, SIDEWALK_TOP + 1f, z), 0.32f, 2f, _palette.PoleStripes, false);
                PrototypeSceneBuilder.CreateBlock("CrossArm", poles, new Vector3(x, 8.6f, z), new Vector3(1.4f, 0.12f, 0.12f), _palette.Concrete, false);

                if (_random.NextDouble() < 0.35)
                    Cylinder("Transformer", poles, new Vector3(side * (POLE_X + 0.4f), 7.3f, z), 0.5f, 0.9f, _palette.Transformer, false);

                // 接到下一根電線桿的電線
                if (z + POLE_SPACING < STREET_END_Z - 4f)
                {
                    float[] wireOffsets = { -0.6f, 0f, 0.6f };
                    for (int i = 0; i < wireOffsets.Length; i++)
                    {
                        Vector3 wireCenter = new Vector3(x + wireOffsets[i], 8.45f - i * 0.35f, z + POLE_SPACING / 2f);
                        PrototypeSceneBuilder.CreateBlock("Wire", poles, wireCenter, new Vector3(0.035f, 0.035f, POLE_SPACING), _palette.Wire, false);
                    }
                }

                // 牽到建築物的電線
                Vector3 from = new Vector3(x, 8.2f, z);
                Vector3 to = new Vector3(side * _profile.FacadeX, 6.2f, z + 1.5f);
                Line("DropWire", poles, from, to, _palette.Wire);

                // 偶爾有橫跨馬路、接到對面電線桿的電線（左側電線桿比右側晚 11 公尺）
                if (side > 0 && z + 11f < STREET_END_Z - 4f && _random.NextDouble() < 0.5)
                    Line("CrossWire", poles, new Vector3(-POLE_X, 8.3f, z + 11f), new Vector3(POLE_X, 8.3f, z), _palette.Wire);
            }

            return polesZ;
        }

        private static void BuildRoadMarkings(Transform parent, int side)
        {
            Transform markings = new GameObject("RoadMarkings").transform;
            markings.SetParent(parent, false);
            float length = STREET_END_Z - STREET_START_Z;
            float centerZ = (STREET_START_Z + STREET_END_Z) / 2f;

            // 路邊紅線（禁止臨時停車）與機車優先道白線
            PrototypeSceneBuilder.CreateBlock("RedCurbLine", markings, new Vector3(side * 5.88f, ROAD_TOP + 0.005f, centerZ), new Vector3(0.15f, 0.01f, length), _palette.RedLine, false);
            PrototypeSceneBuilder.CreateBlock("ScooterLaneLine", markings, new Vector3(side * 4.36f, ROAD_TOP + 0.005f, centerZ), new Vector3(0.12f, 0.01f, length), _palette.LaneLine, false);

            // 路面「機車專用」字樣，字頭朝向前進方向（台灣靠右行駛）
            Quaternion rotation = side > 0 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(90f, 180f, 0f);
            for (float z = STREET_START_Z + 10f; z < STREET_END_Z - 5f; z += 40f)
            {
                Quad("ScooterOnlyText", markings, new Vector3(side * 5.1f, ROAD_TOP + 0.012f, z), rotation, 1.1f, 2.2f, _palette.ScooterOnlyText);
            }
        }

        private static void RecolorBody(Transform model, Material color)
        {
            MeshRenderer renderer = model.GetComponentInChildren<MeshRenderer>();
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].name == "body")
                    materials[i] = color;
            }

            renderer.sharedMaterials = materials;
        }

        private static GameObject Box(string name, Transform parent, int side, float xMin, float xMax, float yMin, float yMax, float zMin, float zMax, Material material, bool withCollider)
        {
            Vector3 center = new Vector3(side * (xMin + xMax) / 2f, (yMin + yMax) / 2f, (zMin + zMax) / 2f);
            Vector3 size = new Vector3(xMax - xMin, yMax - yMin, zMax - zMin);

            return PrototypeSceneBuilder.CreateBlock(name, parent, center, size, material, withCollider);
        }

        private static void FacadeQuad(string name, Transform parent, int side, float x, float y, float z, float width, float height, Material material)
        {
            // Quad 預設朝 -Z，轉 90 度讓它面向馬路
            Quad(name, parent, new Vector3(side * x, y, z), Quaternion.Euler(0f, 90f * side, 0f), width, height, material);
        }

        internal static GameObject Quad(string name, Transform parent, Vector3 position, Quaternion rotation, float width, float height, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.SetLocalPositionAndRotation(position, rotation);
            quad.transform.localScale = new Vector3(width, height, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            return quad;
        }

        internal static void Cylinder(string name, Transform parent, Vector3 position, float diameter, float height, Material material, bool withCollider)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localScale = new Vector3(diameter, height / 2f, diameter);
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!withCollider)
                Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        }

        internal static void Line(string name, Transform parent, Vector3 from, Vector3 to, Material material)
        {
            Vector3 center = (from + to) / 2f;
            GameObject line = PrototypeSceneBuilder.CreateBlock(name, parent, center, new Vector3(0.035f, 0.035f, Vector3.Distance(from, to)), material, false);
            line.transform.localRotation = Quaternion.LookRotation(to - from);
        }

        internal static T Pick<T>(IReadOnlyList<T> items)
        {
            return items[_random.Next(items.Count)];
        }

        internal static Material Textured(string materialName, string textureName, bool alphaClip = false)
        {
            Material material = PrototypeSceneBuilder.GetMaterial(materialName, "#FFFFFF");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TEXTURE_FOLDER}/{textureName}.png");
            if (texture == null)
                throw new System.InvalidOperationException($"找不到貼圖 {textureName}.png，請先執行 python tools/generate_textures.py");

            material.SetTexture("_BaseMap", texture);
            if (alphaClip)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }

            EditorUtility.SetDirty(material);

            return material;
        }

        private static List<Material> TexturedSet(string prefix)
        {
            List<Material> materials = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets($"{prefix} t:Texture2D", new[] { TEXTURE_FOLDER }))
            {
                string textureName = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (textureName.StartsWith(prefix))
                    materials.Add(Textured($"Street_{textureName}", textureName));
            }

            return materials;
        }

        private static List<Material> ColorSet(string prefix, string[] colors)
        {
            List<Material> materials = new List<Material>();
            for (int i = 0; i < colors.Length; i++)
            {
                materials.Add(PrototypeSceneBuilder.GetMaterial($"{prefix}{i}", colors[i]));
            }

            return materials;
        }

        /// <summary>
        /// 目前的街景材質（Initialize 之後才有值）。
        /// </summary>
        internal static Palette Materials => _palette;

        /// <summary>
        /// 街景會用到的所有材質。
        /// </summary>
        internal class Palette
        {
            public readonly List<Material> Facades = ColorSet("Facade", FacadeColors);
            public readonly List<Material> Sheds = ColorSet("Shed", ShedColors);
            public readonly List<Material> ScooterBodies = ColorSet("ParkedScooterBody", ScooterColors);
            public readonly List<Material> HorizontalSigns = TexturedSet("sign_h_");
            public readonly List<Material> VerticalSigns = TexturedSet("sign_v_");
            public readonly List<Material> Windows = TexturedSet("window_");
            public readonly List<Material> Storefronts = TexturedSet("storefront_");
            public readonly Material AirConditionerFront = Textured("Street_air_conditioner", "air_conditioner");
            public readonly Material PoleStripes = Textured("Street_pole_stripes", "pole_stripes");
            public readonly Material ScooterOnlyText = Textured("Street_road_scooter_only", "road_scooter_only", true);
            public readonly Material GroundWall = PrototypeSceneBuilder.GetMaterial("GroundWall", "#6E6A64");
            public readonly Material Band = PrototypeSceneBuilder.GetMaterial("FloorBand", "#EDEBE6");
            public readonly Material SignFrame = PrototypeSceneBuilder.GetMaterial("SignFrame", "#2B2B2B");
            public readonly Material AirConditionerBody = PrototypeSceneBuilder.GetMaterial("AirConditionerBody", "#F2F2EE");
            public readonly Material ShedRoof = PrototypeSceneBuilder.GetMaterial("ShedRoof", "#4F5A63");
            public readonly Material WaterTank = PrototypeSceneBuilder.GetMaterial("WaterTank", "#D7E3EA");
            public readonly Material ArcadeFloor = PrototypeSceneBuilder.GetMaterial("ArcadeFloor", "#A88C76");
            public readonly Material Concrete = PrototypeSceneBuilder.GetMaterial("Concrete", "#A8A8A2");
            public readonly Material Transformer = PrototypeSceneBuilder.GetMaterial("Transformer", "#7C8388");
            public readonly Material Wire = PrototypeSceneBuilder.GetMaterial("Wire", "#1A1A1A");
            public readonly Material RedLine = PrototypeSceneBuilder.GetMaterial("RedCurbLine", "#C8322B");
            public readonly Material LaneLine = PrototypeSceneBuilder.GetMaterial("LaneLine", "#F2F2F2");
            public readonly Material CageMetal = PrototypeSceneBuilder.GetMaterial("CageMetal", "#4A4F54");
            public readonly Material CageRoof = PrototypeSceneBuilder.GetMaterial("CageRoof", "#9AA2A8");
        }
    }
}
