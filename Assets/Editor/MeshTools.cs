using System.Collections.Generic;
using MotorAndSpeed.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 場景建置用的網格工具：沿路徑的帶狀網格、路緣、重複貼圖方塊、圓盤、合併與存檔。
    /// </summary>
    internal static class MeshTools
    {
        /// <summary>
        /// 依網格名稱統計「實例數 × 頂點數」的前幾名並輸出到 log：合併前呼叫，找出最佔世界資料的物件。
        /// </summary>
        public static void LogVertexBreakdown(IEnumerable<Transform> roots, int top = 15)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            Dictionary<string, long> vertices = new Dictionary<string, long>();
            long total = 0;
            foreach (Transform root in roots)
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null)
                        continue;

                    counts.TryGetValue(mesh.name, out int count);
                    vertices.TryGetValue(mesh.name, out long sum);
                    counts[mesh.name] = count + 1;
                    vertices[mesh.name] = sum + mesh.vertexCount;
                    total += mesh.vertexCount;
                }
            }

            List<string> names = new List<string>(vertices.Keys);
            names.Sort((a, b) => vertices[b].CompareTo(vertices[a]));
            System.Text.StringBuilder log = new System.Text.StringBuilder($"[MeshTools] 合併前頂點統計：共 {total:N0} 個頂點\n");
            for (int i = 0; i < Mathf.Min(top, names.Count); i++)
            {
                string name = names[i];
                log.AppendLine($"  {name}: {counts[name]:N0} 個 × 平均 {vertices[name] / counts[name]:N0} = {vertices[name]:N0}（{100.0 * vertices[name] / total:F1}%）");
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 沿著路徑產生平面帶狀網格（路面、標線、人行道），offset 以路徑右手邊為正。
        /// </summary>
        public static Mesh Ribbon(RoutePath path, float s0, float s1, float offsetA, float offsetB, float y, float step = 2f)
        {
            float left = Mathf.Min(offsetA, offsetB);
            float right = Mathf.Max(offsetA, offsetB);
            int count = Mathf.Max(2, Mathf.CeilToInt((s1 - s0) / step) + 1);
            Vector3[] vertices = new Vector3[count * 2];
            Vector3[] normals = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                float s = Mathf.Lerp(s0, s1, i / (count - 1f));
                Vector3 point = path.GetPoint(s) + Vector3.up * y;
                Vector3 sideways = path.GetRight(s);
                vertices[i * 2] = point + sideways * left;
                vertices[i * 2 + 1] = point + sideways * right;
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
                uv[i * 2] = new Vector2(0f, s / 4f);
                uv[i * 2 + 1] = new Vector2(1f, s / 4f);
            }

            for (int i = 0; i < count - 1; i++)
            {
                // Unity 以順時針為正面：從上方看左前、右後的順序
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1;
                triangles[t + 4] = i * 2 + 2;
                triangles[t + 5] = i * 2 + 3;
            }

            return CreateMesh("Ribbon", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// 沿著有高低的折線拉出長條方塊（橋面、護欄、橋上標線）：上下左右四面加兩端封口。
        /// left / right 是相對中線往右手邊的偏移，bottom / top 是相對折線高度的上下範圍。
        /// </summary>
        public static Mesh ProfileStrip(IReadOnlyList<Vector3> points, float left, float right, float bottom, float top)
        {
            float[] lefts = new float[points.Count];
            float[] rights = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                lefts[i] = left;
                rights[i] = right;
            }

            return ProfileStrip(points, lefts, rights, bottom, top);
        }

        /// <summary>
        /// 同上，但每個點的左右偏移可以不同（寬度會變的分隔島）。
        /// </summary>
        public static Mesh ProfileStrip(IReadOnlyList<Vector3> points, IReadOnlyList<float> lefts, IReadOnlyList<float> rights, float bottom, float top)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();
            Vector3[] sides = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 forward = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)];
                forward.y = 0f;
                sides[i] = Vector3.Cross(Vector3.up, forward.normalized);
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 leftTop0 = points[i] + sides[i] * lefts[i] + Vector3.up * top;
                Vector3 rightTop0 = points[i] + sides[i] * rights[i] + Vector3.up * top;
                Vector3 leftBottom0 = points[i] + sides[i] * lefts[i] + Vector3.up * bottom;
                Vector3 rightBottom0 = points[i] + sides[i] * rights[i] + Vector3.up * bottom;
                Vector3 leftTop1 = points[i + 1] + sides[i + 1] * lefts[i + 1] + Vector3.up * top;
                Vector3 rightTop1 = points[i + 1] + sides[i + 1] * rights[i + 1] + Vector3.up * top;
                Vector3 leftBottom1 = points[i + 1] + sides[i + 1] * lefts[i + 1] + Vector3.up * bottom;
                Vector3 rightBottom1 = points[i + 1] + sides[i + 1] * rights[i + 1] + Vector3.up * bottom;
                Vector3 side = (sides[i] + sides[i + 1]).normalized;
                AddQuad(vertices, normals, triangles, leftTop0, rightTop0, rightTop1, leftTop1, Vector3.up);
                AddQuad(vertices, normals, triangles, leftBottom0, rightBottom0, rightBottom1, leftBottom1, Vector3.down);
                AddQuad(vertices, normals, triangles, rightBottom0, rightTop0, rightTop1, rightBottom1, side);
                AddQuad(vertices, normals, triangles, leftBottom0, leftTop0, leftTop1, leftBottom1, -side);
            }

            // 兩端封口
            int last = points.Count - 1;
            Vector3 startNormal = -Vector3.Cross(sides[0], Vector3.up);
            Vector3 endNormal = Vector3.Cross(sides[last], Vector3.up);
            AddQuad(vertices, normals, triangles, points[0] + sides[0] * lefts[0] + Vector3.up * bottom, points[0] + sides[0] * rights[0] + Vector3.up * bottom, points[0] + sides[0] * rights[0] + Vector3.up * top, points[0] + sides[0] * lefts[0] + Vector3.up * top, startNormal);
            AddQuad(vertices, normals, triangles, points[last] + sides[last] * lefts[last] + Vector3.up * bottom, points[last] + sides[last] * rights[last] + Vector3.up * bottom, points[last] + sides[last] * rights[last] + Vector3.up * top, points[last] + sides[last] * lefts[last] + Vector3.up * top, endNormal);

            Vector2[] uv = new Vector2[vertices.Count];

            return CreateMesh("Strip", vertices.ToArray(), normals.ToArray(), uv, triangles.ToArray());
        }

        // 四個角依序繞一圈；依期望的法線自動決定繞的方向，正面才會朝外
        private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
            }

            // Unity 以順時針為正面，此時 Cross(b - a, c - a) 指向觀看者
            bool facing = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f;
            int[] order = facing ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
            foreach (int index in order)
            {
                triangles.Add(start + index);
            }
        }

        /// <summary>
        /// 平放的圓盤（路口補面），正面朝上。
        /// </summary>
        public static Mesh Disc(Vector3 center, float radius, float y, int segments = 20)
        {
            Vector3[] vertices = new Vector3[segments + 1];
            Vector3[] normals = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = center + Vector3.up * y;
            for (int i = 0; i < segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                vertices[i + 1] = center + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            }

            for (int i = 0; i <= segments; i++)
            {
                normals[i] = Vector3.up;
                uv[i] = new Vector2(vertices[i].x / 4f, vertices[i].z / 4f);
            }

            for (int i = 0; i < segments; i++)
            {
                // 角度逆時針遞增，所以反過來接成順時針
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            return CreateMesh("Disc", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// 人行道路緣的垂直面，朝向馬路。
        /// </summary>
        public static Mesh CurbFace(RoutePath path, float s0, float s1, float offset, float height, float step = 2f)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt((s1 - s0) / step) + 1);
            Vector3[] vertices = new Vector3[count * 2];
            Vector3[] normals = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[(count - 1) * 6];
            float facing = offset > 0f ? -1f : 1f;
            for (int i = 0; i < count; i++)
            {
                float s = Mathf.Lerp(s0, s1, i / (count - 1f));
                Vector3 sideways = path.GetRight(s);
                Vector3 bottom = path.GetPoint(s) + sideways * offset;
                vertices[i * 2] = bottom;
                vertices[i * 2 + 1] = bottom + Vector3.up * height;
                normals[i * 2] = sideways * facing;
                normals[i * 2 + 1] = sideways * facing;
                uv[i * 2] = new Vector2(s / 4f, 0f);
                uv[i * 2 + 1] = new Vector2(s / 4f, 1f);
            }

            for (int i = 0; i < count - 1; i++)
            {
                int t = i * 6;
                int bottom0 = i * 2;
                int top0 = bottom0 + 1;
                int bottom1 = bottom0 + 2;
                int top1 = bottom0 + 3;
                if (facing < 0f)
                {
                    triangles[t] = bottom0;
                    triangles[t + 1] = bottom1;
                    triangles[t + 2] = top0;
                    triangles[t + 3] = top0;
                    triangles[t + 4] = bottom1;
                    triangles[t + 5] = top1;
                }
                else
                {
                    triangles[t] = bottom0;
                    triangles[t + 1] = top0;
                    triangles[t + 2] = bottom1;
                    triangles[t + 3] = top0;
                    triangles[t + 4] = top1;
                    triangles[t + 5] = bottom1;
                }
            }

            return CreateMesh("Curb", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// 中心在原點的方塊網格，UV 依公尺計算（每 tile 公尺重複一次貼圖），外牆窗戶才不會被拉長。
        /// </summary>
        public static Mesh TiledBox(Vector3 size, Vector2 tile, bool plainCaps = false)
        {
            // 幾何在執行期的 BoxMeshes（方便測試）；plainCaps 時屋頂不貼窗戶
            BoxMeshes.Tiled(size, tile, plainCaps, out Vector3[] vertices, out Vector3[] normals, out Vector2[] uv, out int[] triangles);

            return CreateMesh("TiledBox", vertices, normals, uv, triangles);
        }

        public static Mesh CreateMesh(string name, Vector3[] vertices, Vector3[] normals, Vector2[] uv, int[] triangles)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormatFor(vertices.Length) };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// 把 root 底下所有網格依材質合併成少數幾個物件；碰撞體保留，其餘空物件移除。
        /// </summary>
        /// <param name="keep">合併後的網格交給呼叫端存檔</param>
        public static void Combine(Transform root, System.Action<Mesh> keep)
        {
            // 先拆開 Prefab 實例，才能移除裡面的元件
            List<GameObject> instances = new List<GameObject>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject))
                    instances.Add(child.gameObject);
            }

            foreach (GameObject instance in instances)
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
            // 每個 OBJ 都有自己的一份材質，但都來自同一份 palette.mtl：同名就是同一種，合併成一組省 draw call
            Dictionary<string, Material> objMaterials = new Dictionary<string, Material>();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Mesh mesh = filter.sharedMesh;
                if (renderer == null || mesh == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material material = materials[Mathf.Min(sub, materials.Length - 1)];
                    if (material == null)
                        continue;

                    if (AssetDatabase.GetAssetPath(material).EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (objMaterials.TryGetValue(material.name, out Material shared))
                            material = shared;
                        else
                            objMaterials[material.name] = material;
                    }

                    if (!groups.TryGetValue(material, out List<CombineInstance> list))
                    {
                        list = new List<CombineInstance>();
                        groups[material] = list;
                    }

                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = toRoot * filter.transform.localToWorldMatrix });
                }
            }

            foreach (MeshFilter filter in filters)
            {
                Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(filter);
            }

            RemoveEmptyChildren(root);

            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                Mesh combined = new Mesh { name = $"{root.name}_{group.Key.name}", indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(group.Value.ToArray(), true, true);

                // 合併後頂點不多就改用 16 位元索引，索引資料減半
                if (combined.vertexCount < 65535)
                {
                    int[] triangles = combined.triangles;
                    combined.indexFormat = IndexFormat.UInt16;
                    combined.triangles = triangles;
                }

                // 沒有法線貼圖就不需要切線；純色材質也不需要 UV，可以省下大量檔案空間
                combined.tangents = null;
                if (group.Key.GetTexture("_BaseMap") == null)
                    combined.uv = null;

                combined.RecalculateBounds();
                keep(combined);

                GameObject merged = new GameObject($"Combined_{group.Key.name}");
                merged.transform.SetParent(root, false);
                merged.AddComponent<MeshFilter>().sharedMesh = combined;
                merged.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
        }

        /// <summary>
        /// 把網格依分組存成二進位資源檔（每組一個檔案）。
        /// </summary>
        public static void SaveBundles(string folder, Dictionary<string, List<Mesh>> groups, string readableGroup)
        {
            PrototypeSceneBuilder.EnsureFolder(folder);
            foreach (KeyValuePair<string, List<Mesh>> group in groups)
            {
                MeshBundle bundle = ScriptableObject.CreateInstance<MeshBundle>();
                bundle.Meshes.AddRange(group.Value);
                string path = $"{folder}/{group.Key}.asset";
                AssetDatabase.CreateAsset(bundle, path);
                foreach (Mesh mesh in group.Value)
                {
                    // 只用來渲染的網格設成不可讀：執行期只留 GPU 那一份；MeshCollider 要讀資料，碰撞群組保持可讀
                    if (group.Key != readableGroup)
                        SetReadable(mesh, false);

                    AssetDatabase.AddObjectToAsset(mesh, path);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 共用的八角柱網格（尺寸同 Unity 內建圓柱：直徑 1、高 2、中心在原點），頂點只有內建圓柱的一半。
        /// </summary>
        public static Mesh Prism8
        {
            get
            {
                if (_prism8 == null)
                    _prism8 = BuildPrism(8);

                return _prism8;
            }
        }

        /// <summary>
        /// 把 root 底下只掛 BoxCollider 的子物件，收成 root 本身的多個 BoxCollider（世界中的範圍不變），
        /// 再刪掉變空的子物件。合併網格之後呼叫，可以大幅減少場景物件數。
        /// </summary>
        public static void FoldBoxColliders(Transform root)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.transform == root || box.GetComponents<Component>().Length != 2)
                    continue;

                // 有旋轉的碰撞體收到 root 會變形，留在原處
                Matrix4x4 matrix = toRoot * box.transform.localToWorldMatrix;
                if (Mathf.Abs(matrix.m01) + Mathf.Abs(matrix.m02) + Mathf.Abs(matrix.m10) + Mathf.Abs(matrix.m12) + Mathf.Abs(matrix.m20) + Mathf.Abs(matrix.m21) > 1e-4f)
                    continue;

                BoxCollider folded = root.gameObject.AddComponent<BoxCollider>();
                folded.center = matrix.MultiplyPoint3x4(box.center);
                folded.size = Vector3.Scale(new Vector3(Mathf.Abs(matrix.m00), Mathf.Abs(matrix.m11), Mathf.Abs(matrix.m22)), box.size);
                folded.isTrigger = box.isTrigger;
                folded.sharedMaterial = box.sharedMaterial;
                Object.DestroyImmediate(box);
            }

            RemoveEmptyChildren(root);
        }

        private static Mesh _prism8;

        private static IndexFormat IndexFormatFor(int vertexCount)
        {
            return vertexCount < 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32;
        }

        private static void SetReadable(Mesh mesh, bool readable)
        {
            SerializedObject serialized = new SerializedObject(mesh);
            serialized.FindProperty("m_IsReadable").boolValue = readable;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Mesh BuildPrism(int sides)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                // 側面：每面 4 個頂點，平面著色；UV 沿圓周 0~1、沿高度 0~1（警示套貼圖用）
                float a0 = 2f * Mathf.PI * i / sides;
                float a1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                Vector3 normal = ((p0 + p1) * 0.5f).normalized;
                int start = vertices.Count;
                vertices.AddRange(new[] { p0 + Vector3.down, p0 + Vector3.up, p1 + Vector3.up, p1 + Vector3.down });
                normals.AddRange(new[] { normal, normal, normal, normal });
                uv.AddRange(new[] { new Vector2((float)i / sides, 0f), new Vector2((float)i / sides, 1f), new Vector2((float)(i + 1) / sides, 1f), new Vector2((float)(i + 1) / sides, 0f) });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            foreach (float y in new[] { 1f, -1f })
            {
                int start = vertices.Count;
                for (int i = 0; i < sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f));
                    normals.Add(new Vector3(0f, y, 0f));
                    uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                }

                for (int i = 1; i < sides - 1; i++)
                {
                    if (y > 0f)
                        triangles.AddRange(new[] { start, start + i + 1, start + i });
                    else
                        triangles.AddRange(new[] { start, start + i, start + i + 1 });
                }
            }

            return CreateMesh($"Prism{sides}", vertices.ToArray(), normals.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        private static void RemoveEmptyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                RemoveEmptyChildren(child);
                if (child.childCount == 0 && child.GetComponents<Component>().Length == 1)
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
