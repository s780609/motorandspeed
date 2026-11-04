using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 從固定視角替五股世界與測試場景截圖（PC 與 Web 兩種畫質），用來比較美化前後。
    /// 視角由 wugu_world.json 推算，重建世界後位置不變；截圖存到 Screenshots/&lt;tag&gt;。
    /// batchmode：-executeMethod MotorAndSpeed.EditorTools.ScreenshotTool.Capture -shotTag before（不可加 -nographics，需要 GPU 渲染）
    /// </summary>
    internal static class ScreenshotTool
    {
        private const int WIDTH = 1600;
        private const int HEIGHT = 900;
        private const string OUTPUT_FOLDER = "Screenshots";
        private const string WORLD_SCENE = "Assets/Scenes/WuguWorld.unity";
        private const string PROTOTYPE_SCENE = "Assets/Scenes/Prototype.unity";
        private const string WORLD_JSON = "Assets/Data/wugu_world.json";

        // 品質等級：1 = PC、0 = Web（WebGL 預設用的等級）
        private static readonly (int Level, string Name)[] Qualities = { (1, "PC"), (0, "Web") };

        private static readonly string[] Scooters =
        {
            PrototypeSceneBuilder.PREFAB_PATH,
            "Assets/Prefabs/Npc/NpcScooter.prefab",
            "Assets/Prefabs/Npc/NpcScooterBajiu.prefab",
            "Assets/Prefabs/Npc/NpcScooterBajiuJunk.prefab",
            "Assets/Prefabs/Racers/Racer0.prefab",
        };

        private static readonly string[] BigVehicles =
        {
            "Assets/Prefabs/Npc/NpcCar.prefab",
            "Assets/Prefabs/Npc/NpcTruck.prefab",
            "Assets/Prefabs/Npc/NpcBus.prefab",
        };

        [MenuItem("MotorAndSpeed/截圖比較")]
        public static void Capture()
        {
            string tag = ReadArgument("-shotTag") ?? "shot";
            string folder = Path.Combine(OUTPUT_FOLDER, tag);
            Directory.CreateDirectory(folder);
            int previousQuality = QualitySettings.GetQualityLevel();
            try
            {
                CaptureWorld(folder);
                CapturePrototype(folder);
            }
            finally
            {
                QualitySettings.SetQualityLevel(previousQuality, true);
            }

            Debug.Log($"[ScreenshotTool] 完成：{folder}");
        }

        private static void CaptureWorld(string folder)
        {
            EditorSceneManager.OpenScene(WORLD_SCENE, OpenSceneMode.Single);
            WorldData data = WorldData.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>(WORLD_JSON).text);
            Vector3 start = new Vector3(data.start.x, 0f, data.start.z);
            Vector3 direction = new Vector3(data.start.dirX, 0f, data.start.dirZ).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction);

            List<(string Name, Vector3 Position, Vector3 Target)> views = new List<(string, Vector3, Vector3)>
            {
                ("start", start - direction * 4.5f + Vector3.up * 1.8f, start + Vector3.up * 0.9f),
                ("aerial", start - direction * 110f + Vector3.up * 110f, start),
            };

            EdgeData hero = data.edges.FirstOrDefault(e => e.name != null && e.name.Contains("成泰路三段"))
                ?? data.edges.FirstOrDefault(e => e.name != null && e.name.Contains("成泰路"));
            if (hero != null)
                views.Add(StreetView("hero", hero));

            EdgeData lane = data.edges.Where(e => !e.main && e.length > 60f).OrderBy(e => Vector3.Distance(PointAt(e.points, 0.5f), start)).FirstOrDefault();
            if (lane != null)
                views.Add(StreetView("lane", lane));

            if (data.newTown != null && data.newTown.Count > 0)
            {
                Vector3 center = new Vector3(data.newTown.Average(p => p.x), 0f, data.newTown.Average(p => p.z));
                views.Add(("newtown", center + new Vector3(-45f, 35f, -45f), center));
            }

            if (data.bridges.Count > 0)
            {
                List<RoutePoint> points = data.bridges[0].points;
                Vector3 point = PointAt(points, 0.6f);
                Vector3 along = (PointAt(points, 0.65f) - point).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, along);
                views.Add(("bridge", point - along * 30f + side * 12f + Vector3.up * 18f, point + Vector3.up * 8f));
            }

            Camera camera = GetCamera();
            foreach ((string name, Vector3 position, Vector3 target) in views)
            {
                RenderAllQualities(camera, position, target, Path.Combine(folder, $"wugu_{name}"));
            }

            // 車輛一字排開：機車一排、汽車貨車公車一排，都朝起點方向
            Vector3 scooterRow = start + direction * 14f;
            List<GameObject> spawned = SpawnRow(Scooters, scooterRow, direction, right, 1.6f);
            RenderAllQualities(camera, scooterRow + direction * 5.5f - right * 2.5f + Vector3.up * 1.6f, scooterRow + Vector3.up * 0.7f, Path.Combine(folder, "wugu_scooters"));
            DestroyAll(spawned);

            Vector3 bigRow = start + direction * 30f;
            spawned = SpawnRow(BigVehicles, bigRow, direction, right, 3.4f);
            RenderAllQualities(camera, bigRow + direction * 13f - right * 7f + Vector3.up * 3f, bigRow + Vector3.up * 1.2f, Path.Combine(folder, "wugu_vehicles"));
            DestroyAll(spawned);
        }

        private static void CapturePrototype(string folder)
        {
            EditorSceneManager.OpenScene(PROTOTYPE_SCENE, OpenSceneMode.Single);
            ScooterController scooter = Object.FindFirstObjectByType<ScooterController>();
            if (scooter == null)
                return;

            Vector3 position = scooter.transform.position;
            Vector3 forward = scooter.transform.forward;
            Camera camera = GetCamera();
            RenderAllQualities(camera, position - forward * 4.5f + Vector3.up * 1.8f, position + Vector3.up * 0.9f, Path.Combine(folder, "prototype_street"));
            RenderAllQualities(camera, position - forward * 60f + Vector3.up * 45f, position + forward * 20f, Path.Combine(folder, "prototype_aerial"));
        }

        private static (string, Vector3, Vector3) StreetView(string name, EdgeData edge)
        {
            // 站在路邊、視線略高，沿路往前看兩側的建築
            Vector3 point = PointAt(edge.points, 0.5f);
            Vector3 along = (PointAt(edge.points, 0.55f) - point).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Vector3 position = point + side * (edge.width * 0.3f) - along * 5f + Vector3.up * 1.6f;

            return (name, position, point + along * 35f + Vector3.up * 3f);
        }

        private static Vector3 PointAt(List<RoutePoint> points, float t)
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                total += Vector2.Distance(new Vector2(points[i - 1].x, points[i - 1].z), new Vector2(points[i].x, points[i].z));
            }

            float target = total * Mathf.Clamp01(t);
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 a = new Vector3(points[i - 1].x, 0f, points[i - 1].z);
                Vector3 b = new Vector3(points[i].x, 0f, points[i].z);
                float length = Vector3.Distance(a, b);
                if (target <= length || i == points.Count - 1)
                    return Vector3.Lerp(a, b, length > 0f ? Mathf.Clamp01(target / length) : 0f);

                target -= length;
            }

            return new Vector3(points[0].x, 0f, points[0].z);
        }

        private static List<GameObject> SpawnRow(string[] prefabPaths, Vector3 center, Vector3 direction, Vector3 right, float spacing)
        {
            List<GameObject> spawned = new List<GameObject>();
            for (int i = 0; i < prefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                if (prefab == null)
                    continue;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Vector3 offset = right * ((i - (prefabPaths.Length - 1) * 0.5f) * spacing);
                instance.transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(direction));
                spawned.Add(instance);
            }

            return spawned;
        }

        private static void DestroyAll(List<GameObject> objects)
        {
            foreach (GameObject instance in objects)
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Camera GetCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
                camera = new GameObject("ScreenshotCamera").AddComponent<Camera>();

            camera.fieldOfView = 60f;

            return camera;
        }

        private static void RenderAllQualities(Camera camera, Vector3 position, Vector3 target, string pathWithoutExtension)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            foreach ((int level, string name) in Qualities)
            {
                QualitySettings.SetQualityLevel(level, true);
                Render(camera, $"{pathWithoutExtension}_{name}.png");
            }
        }

        private static void Render(Camera camera, string path)
        {
            RenderTexture texture = new RenderTexture(WIDTH, HEIGHT, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = camera.targetTexture;

            // 渲染兩次：讓陰影與後製的狀態先初始化
            for (int i = 0; i < 2; i++)
            {
                RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = texture };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.targetTexture = texture;
                    camera.Render();
                    camera.targetTexture = previous;
                }
            }

            RenderTexture.active = texture;
            Texture2D image = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            texture.Release();
            Object.DestroyImmediate(texture);
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);

            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
