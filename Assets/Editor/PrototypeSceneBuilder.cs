using System;
using MotorAndSpeed.Cameras;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 產生速克達 Prefab 與原型測試場景（方塊搭的環狀道路、斜坡、交通錐、街屋）。
    /// 選單：MotorAndSpeed / 重建測試場景
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string MODELS_FOLDER = "Assets/Models";
        private const string MATERIALS_FOLDER = "Assets/Materials/Prototype";
        private const string PHYSICS_MATERIAL_PATH = "Assets/Physics/Frictionless.asset";
        private const string PREFAB_PATH = "Assets/Prefabs/Scooter.prefab";
        private const string SCENE_PATH = "Assets/Scenes/Prototype.unity";
        private const float ROAD_WIDTH = 12f;
        private const float ROAD_TOP = 0.02f;

        // 與 tools/generate_models.py 的組裝位置一致
        private static readonly Vector3 FrontWheelPosition = new Vector3(0f, 0.24f, 0.65f);
        private static readonly Vector3 RearWheelPosition = new Vector3(0f, 0.24f, -0.65f);
        private static readonly Vector3 HandlebarPosition = new Vector3(0f, 0.98f, 0.52f);

        private static readonly string[] BuildingColors =
        {
            "#E8D8C3", "#C9D6DF", "#F2C6B4", "#D5E8D4", "#E6E6E6", "#F7E3AF",
        };

        /// <summary>
        /// 重建 Prefab 與測試場景，並設為建置場景。
        /// </summary>
        [MenuItem("MotorAndSpeed/重建測試場景")]
        public static void Build()
        {
            GameObject scooterPrefab = BuildScooterPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            BuildTrack();

            GameObject scooter = (GameObject)PrefabUtility.InstantiatePrefab(scooterPrefab);
            scooter.transform.position = new Vector3(0f, 0.05f, 0f);
            ScooterController controller = scooter.GetComponent<ScooterController>();

            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0f, 1.85f, -4.5f);
            camera.transform.LookAt(scooter.transform.position + Vector3.up * 0.9f);
            SetReference(camera.gameObject.AddComponent<ChaseCamera>(), "_target", controller);
            SetReference(camera.gameObject.AddComponent<SpeedHud>(), "_target", controller);

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE_PATH, true) };
            AssetDatabase.SaveAssets();

            Bounds bodyBounds = scooter.transform.Find("Visual/scooter_body").GetComponentInChildren<Renderer>().bounds;
            Debug.Log($"[PrototypeSceneBuilder] 完成：{SCENE_PATH}，scooter_body 尺寸 {bodyBounds.size}");
        }

        private static GameObject BuildScooterPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            GameObject root = new GameObject("Scooter");

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 150f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 底部用球體：在斜坡上也能讓射線正確判斷著地；車身用方塊擋住障礙物
            PhysicsMaterial frictionless = GetFrictionlessMaterial();
            SphereCollider baseCollider = root.AddComponent<SphereCollider>();
            baseCollider.center = new Vector3(0f, 0.35f, 0f);
            baseCollider.radius = 0.35f;
            baseCollider.sharedMaterial = frictionless;
            BoxCollider hullCollider = root.AddComponent<BoxCollider>();
            hullCollider.center = new Vector3(0f, 0.65f, 0f);
            hullCollider.size = new Vector3(0.5f, 0.75f, 1.9f);
            hullCollider.sharedMaterial = frictionless;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            AddModel("scooter_body", visual, Vector3.zero);
            AddModel("penguin_rider", visual, Vector3.zero);
            Transform handlebar = AddModel("scooter_handlebar", visual, HandlebarPosition);
            Transform frontWheel = AddModel("scooter_wheel", visual, FrontWheelPosition);
            frontWheel.name = "FrontWheel";
            Transform rearWheel = AddModel("scooter_wheel", visual, RearWheelPosition);
            rearWheel.name = "RearWheel";

            ScooterController controller = root.AddComponent<ScooterController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("_visual").objectReferenceValue = visual;
            serialized.FindProperty("_handlebar").objectReferenceValue = handlebar;
            serialized.FindProperty("_frontWheel").objectReferenceValue = frontWheel;
            serialized.FindProperty("_rearWheel").objectReferenceValue = rearWheel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);

            return prefab;
        }

        private static void BuildTrack()
        {
            Transform track = new GameObject("Track").transform;
            Material grass = GetMaterial("Grass", "#8DB580");
            Material asphalt = GetMaterial("Asphalt", "#4A4D52");
            Material laneLine = GetMaterial("LaneLine", "#F2F2F2");
            Material sidewalk = GetMaterial("Sidewalk", "#B9B4AA");
            Material ramp = GetMaterial("Ramp", "#F2A541");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Grass";
            ground.transform.SetParent(track, false);
            ground.transform.position = new Vector3(-40f, 0f, 80f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = grass;

            // 環狀道路：四個角落 (0,-20) → (0,180) → (-80,180) → (-80,-20)
            Vector3[] corners =
            {
                new Vector3(0f, 0f, -20f),
                new Vector3(0f, 0f, 180f),
                new Vector3(-80f, 0f, 180f),
                new Vector3(-80f, 0f, -20f),
            };
            for (int i = 0; i < corners.Length; i++)
            {
                BuildRoadSegment(track, corners[i], corners[(i + 1) % corners.Length], asphalt, laneLine);
            }

            // 起點直線兩側的人行道與街屋
            System.Random random = new System.Random(7);
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBlock("Sidewalk", track, new Vector3(side * 7.5f, 0.05f, 80f), new Vector3(3f, 0.1f, 188f), sidewalk, true);
                for (float z = -10f; z <= 170f; z += 10f)
                {
                    float height = 7f + (float)random.NextDouble() * 9f;
                    int colorIndex = random.Next(BuildingColors.Length);
                    Material color = GetMaterial($"Building{colorIndex}", BuildingColors[colorIndex]);
                    CreateBlock("Building", track, new Vector3(side * 13f, height / 2f, z), new Vector3(8f, height, 9f), color, true);
                }
            }

            // 跳台：後端貼齊路面，前端約 1.7 公尺高
            CreateBlock("Ramp", track, new Vector3(0f, 0.575f, 110f), new Vector3(6f, 0.6f, 7f), ramp, true, Quaternion.Euler(-14f, 0f, 0f));

            // 交通錐 S 彎
            Transform cones = new GameObject("Cones").transform;
            cones.SetParent(track, false);
            int index = 0;
            for (float z = 25f; z <= 65f; z += 8f)
            {
                Transform cone = AddModel("traffic_cone", cones, new Vector3(index % 2 == 0 ? 2.5f : -2.5f, ROAD_TOP, z));
                Rigidbody coneBody = cone.gameObject.AddComponent<Rigidbody>();
                coneBody.mass = 4f;
                BoxCollider coneCollider = cone.gameObject.AddComponent<BoxCollider>();
                coneCollider.center = new Vector3(0f, 0.365f, 0f);
                coneCollider.size = new Vector3(0.4f, 0.73f, 0.4f);
                index++;
            }
        }

        private static void BuildRoadSegment(Transform parent, Vector3 from, Vector3 to, Material asphalt, Material laneLine)
        {
            Vector3 direction = (to - from).normalized;
            float length = Vector3.Distance(from, to);
            Vector3 center = (from + to) / 2f;
            Quaternion rotation = Quaternion.LookRotation(direction);

            CreateBlock("Road", parent, center + Vector3.up * (ROAD_TOP - 0.1f), new Vector3(ROAD_WIDTH, 0.2f, length + ROAD_WIDTH), asphalt, true, rotation);

            // 中央虛線：每 8 公尺一段 3 公尺長的白線
            for (float offset = 4f; offset < length - 2f; offset += 8f)
            {
                Vector3 position = from + direction * offset + Vector3.up * (ROAD_TOP + 0.005f);
                CreateBlock("LaneDash", parent, position, new Vector3(0.25f, 0.01f, 3f), laneLine, false, rotation);
            }
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool withCollider, Quaternion? rotation = null)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.rotation = rotation ?? Quaternion.identity;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!withCollider)
                Object.DestroyImmediate(block.GetComponent<Collider>());

            return block;
        }

        private static Transform AddModel(string modelName, Transform parent, Vector3 localPosition)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{MODELS_FOLDER}/{modelName}.obj");
            if (model == null)
                throw new InvalidOperationException($"找不到模型 {modelName}.obj，請先執行 python tools/generate_models.py");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.transform.localPosition = localPosition;

            return instance.transform;
        }

        private static Material GetMaterial(string materialName, string hexColor)
        {
            EnsureFolder(MATERIALS_FOLDER);
            string path = $"{MATERIALS_FOLDER}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            ColorUtility.TryParseHtmlString(hexColor, out Color color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);

            return material;
        }

        private static PhysicsMaterial GetFrictionlessMaterial()
        {
            EnsureFolder("Assets/Physics");
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PHYSICS_MATERIAL_PATH);
            if (material != null)
                return material;

            material = new PhysicsMaterial("Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(material, PHYSICS_MATERIAL_PATH);

            return material;
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
