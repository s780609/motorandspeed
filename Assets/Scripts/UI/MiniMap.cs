using System.Collections.Generic;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 左上角小地圖：面向方向朝上，顯示五股路網、NPC 車輛、紅綠燈，
    /// 以及遊戲模式設定的導航箭頭、目標點與對手位置；有導航線時畫面上方另有大箭頭指出前進方向。
    /// ＋／－ 或滑鼠滾輪縮放，M 切換大地圖，N 或點指北針切換「車頭朝上／正北朝上」。
    /// </summary>
    public class MiniMap : MonoBehaviour
    {
        private const int TEXTURE_SIZE = 2048;
        private const float SMALL_FRAME = 240f;
        private const float LARGE_FRAME = 560f;
        private const float MARGIN = 16f;
        private const float BORDER = 3f;
        // 地圖圖片比框大 1.5 倍，旋轉時角落才不會露出空白
        private const float IMAGE_SCALE = 1.5f;
        private const float MIN_METERS_PER_PIXEL = 0.25f;
        private const float MAX_METERS_PER_PIXEL = 8f;
        // 導航線每隔幾公尺畫一個點
        private const float GUIDE_SPACING = 12f;
        // 畫面上方的導航箭頭指向路線前方幾公尺
        private const float LOOK_AHEAD = 30f;

        private static readonly Color32 BackgroundColor = new Color32(38, 44, 51, 255);
        private static readonly Color32 MinorRoadColor = new Color32(107, 115, 125, 255);
        private static readonly Color32 MainRoadColor = new Color32(154, 163, 173, 255);
        private static readonly Color GuideColor = new Color(0.25f, 0.66f, 0.96f);
        private static readonly Color RacerColor = new Color(1f, 0.55f, 0.15f);

        [SerializeField] private TextAsset _worldJson;
        [SerializeField] private float _metersPerPixel = 1.25f;

        private readonly List<Image> _vehicleDots = new List<Image>();
        private readonly List<Image> _signalDots = new List<Image>();
        private readonly List<Image> _guideDots = new List<Image>();
        private readonly List<Image> _racerDots = new List<Image>();
        private readonly List<Vector3> _guide = new List<Vector3>();
        private readonly List<Vector3> _guidePath = new List<Vector3>();
        private readonly List<Transform> _racers = new List<Transform>();
        private MapProjection _projection;
        private Texture2D _texture;
        private RectTransform _frame;
        private RectTransform _markers;
        private RawImage _map;
        private Image _target;
        private Vector3? _targetPoint;
        private Image _secondary;
        private Vector3? _secondaryPoint;
        private Image _navigationArrow;
        private ScooterController _player;
        private WorldTraffic _traffic;
        private TrafficSignal[] _signals = new TrafficSignal[0];
        private Sprite _dotSprite;
        private Sprite _arrowSprite;
        private bool _large;
        private bool _northUp;
        private RectTransform _playerArrow;
        private RectTransform _compass;

        /// <summary>目前的縮放（每個畫面像素代表幾公尺）。</summary>
        public float MetersPerPixel => _metersPerPixel;

        /// <summary>產生好的地圖貼圖。</summary>
        public Texture2D MapTexture => _texture;

        /// <summary>true：正北朝上；false：車頭朝上（預設）。</summary>
        public bool NorthUp
        {
            get => _northUp;
            set => _northUp = value;
        }

        /// <summary>地圖目前旋轉的角度（度）。</summary>
        public float MapRotation => _map != null ? _map.rectTransform.localEulerAngles.z : 0f;

        /// <summary>下一個目標點（例如取貨後要送去的客人），沒有時為 null。</summary>
        public Vector3? SecondaryTarget => _secondaryPoint;

        /// <summary>畫面上方的導航箭頭是否顯示中。</summary>
        public bool NavigationArrowShown => _navigationArrow != null && _navigationArrow.enabled;

        /// <summary>
        /// 設定導航線（依序經過的點），傳 null 清除。
        /// </summary>
        public void SetGuide(IReadOnlyList<Vector3> points)
        {
            _guide.Clear();
            _guidePath.Clear();
            if (points == null || points.Count == 0)
                return;

            _guidePath.AddRange(points);

            // 依固定間隔取點，點數才不會太多
            _guide.Add(points[0]);
            float carried = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                carried += Vector3.Distance(points[i - 1], points[i]);
                if (carried >= GUIDE_SPACING)
                {
                    _guide.Add(points[i]);
                    carried = 0f;
                }
            }
        }

        /// <summary>
        /// 設定目標點（送貨地點），傳 null 清除。
        /// </summary>
        public void SetTarget(Vector3? target, Color color)
        {
            _targetPoint = target;
            if (_target != null)
                _target.color = color;
        }

        /// <summary>
        /// 設定下一個目標點（以空心圈顯示），傳 null 清除。
        /// </summary>
        public void SetSecondaryTarget(Vector3? target)
        {
            _secondaryPoint = target;
        }

        /// <summary>
        /// 設定要顯示的對手（比賽 AI），傳 null 清除。
        /// </summary>
        public void SetRacers(IReadOnlyList<Transform> racers)
        {
            _racers.Clear();
            if (racers != null)
                _racers.AddRange(racers);
        }

        private void Start()
        {
            WorldData data = WorldData.FromJson(_worldJson.text);
            _player = FindFirstObjectByType<ScooterController>();
            _traffic = FindFirstObjectByType<WorldTraffic>();
            _signals = FindObjectsByType<TrafficSignal>(FindObjectsSortMode.None);
            _texture = BuildTexture(data);
            BuildInterface();
        }

        private void Update()
        {
            if (_player == null || _frame == null)
                return;

            HandleInput();

            float frameSize = _large ? LARGE_FRAME : SMALL_FRAME;
            _frame.sizeDelta = Vector2.one * frameSize;
            Vector3 position = _player.transform.position;
            float heading = _player.transform.eulerAngles.y;

            // 車頭朝上時整張地圖跟著面向轉；正北朝上時地圖不動，改轉玩家箭頭
            float mapHeading = _northUp ? 0f : heading;
            _playerArrow.localEulerAngles = new Vector3(0f, 0f, _northUp ? -heading : 0f);

            // 以玩家為中心取地圖的一塊，再依面向旋轉
            float imageSize = frameSize * IMAGE_SCALE;
            float uvSize = imageSize * _metersPerPixel / _projection.WorldSize;
            Vector2 uv = _projection.WorldToUv(position);
            _map.rectTransform.sizeDelta = Vector2.one * imageSize;
            _map.uvRect = new Rect(uv.x - uvSize / 2f, uv.y - uvSize / 2f, uvSize, uvSize);
            _map.rectTransform.localEulerAngles = new Vector3(0f, 0f, mapHeading);

            float half = frameSize / 2f - BORDER - 4f;
            UpdateGuide(position, mapHeading, half);
            UpdateVehicles(position, mapHeading, half);
            UpdateSignals(position, mapHeading, half);
            UpdateRacers(position, mapHeading, half);

            PlaceOnEdge(_target, _targetPoint, position, mapHeading, half);
            PlaceOnEdge(_secondary, _secondaryPoint, position, mapHeading, half);

            // 指北針沿著外框繞，永遠指向地圖上的北方
            float radians = mapHeading * Mathf.Deg2Rad;
            _compass.anchoredPosition = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * (frameSize / 2f - 14f);

            // 畫面上方的大箭頭是給騎車看的，永遠以車頭方向為準
            UpdateNavigationArrow(position, heading);
        }

        // 目標點在畫面外時，貼在邊緣指出方向
        private void PlaceOnEdge(Image marker, Vector3? point, Vector3 position, float heading, float half)
        {
            marker.enabled = point.HasValue;
            if (!point.HasValue)
                return;

            Vector2 offset = MapProjection.ToHeadingUp(position, heading, point.Value) / _metersPerPixel;
            float overflow = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) / half;
            marker.rectTransform.anchoredPosition = overflow > 1f ? offset / overflow : offset;
        }

        private void UpdateNavigationArrow(Vector3 position, float heading)
        {
            _navigationArrow.enabled = _guidePath.Count >= 2;
            if (!_navigationArrow.enabled)
                return;

            Vector3 ahead = Navigation.LookAheadPoint(_guidePath, position, LOOK_AHEAD);
            _navigationArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, ArrowAngle(MapProjection.ToHeadingUp(position, heading, ahead)));
        }

        // 箭頭圖案朝上，要轉多少度才指向 direction（UI 的正角度是逆時針）
        private static float ArrowAngle(Vector2 direction)
        {
            return -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        }

        private void HandleInput()
        {
            int steps = 0;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                    steps++;

                if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                    steps--;

                if (keyboard.mKey.wasPressedThisFrame)
                    _large = !_large;

                if (keyboard.nKey.wasPressedThisFrame)
                    _northUp = !_northUp;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f && RectTransformUtility.RectangleContainsScreenPoint(_frame, mouse.position.ReadValue(), null))
                    steps += scroll > 0f ? 1 : -1;
            }

            if (steps != 0)
                _metersPerPixel = MapProjection.Zoom(_metersPerPixel, steps, MIN_METERS_PER_PIXEL, MAX_METERS_PER_PIXEL);
        }

        private void UpdateGuide(Vector3 position, float heading, float half)
        {
            while (_guideDots.Count < _guide.Count)
            {
                Image dot = CreateDot("Guide", 9f, GuideColor);
                dot.sprite = _arrowSprite;
                dot.transform.SetSiblingIndex(0);
                _guideDots.Add(dot);
            }

            for (int i = 0; i < _guideDots.Count; i++)
            {
                bool visible = i < _guide.Count && Place(_guideDots[i], position, heading, _guide[i], half);
                _guideDots[i].enabled = visible;
                if (!visible)
                    continue;

                // 每個導航點都是指向下一個點的小箭頭，最後一點沿用前一段的方向
                int from = i < _guide.Count - 1 ? i : i - 1;
                if (from < 0)
                    continue;

                Vector2 direction = MapProjection.ToHeadingUp(position, heading, _guide[from + 1]) - MapProjection.ToHeadingUp(position, heading, _guide[from]);
                _guideDots[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, ArrowAngle(direction));
            }
        }

        private void UpdateVehicles(Vector3 position, float heading, float half)
        {
            if (_traffic == null)
                return;

            IReadOnlyList<WorldVehicle> vehicles = _traffic.Vehicles;
            while (_vehicleDots.Count < vehicles.Count)
            {
                _vehicleDots.Add(CreateDot("Vehicle", 6f, Color.white));
            }

            for (int i = 0; i < vehicles.Count; i++)
            {
                Image dot = _vehicleDots[i];
                dot.enabled = vehicles[i].gameObject.activeInHierarchy && Place(dot, position, heading, vehicles[i].transform.position, half);
                if (!dot.enabled)
                    continue;

                // 依車長分辨：機車黃色、汽車白色、大車紫色（藍色留給導航線）
                if (vehicles[i].IsScooter)
                {
                    dot.color = new Color(1f, 0.88f, 0.54f);
                }
                else
                {
                    dot.color = vehicles[i].Length > 6f ? new Color(0.78f, 0.6f, 1f) : Color.white;
                }
            }
        }

        private void UpdateSignals(Vector3 position, float heading, float half)
        {
            for (int i = 0; i < _signals.Length; i++)
            {
                Image dot = _signalDots[i];
                dot.enabled = Place(dot, position, heading, _signals[i].transform.position, half);
                if (!dot.enabled)
                    continue;

                switch (_signals[i].MainState)
                {
                    case SignalState.Green:
                        dot.color = new Color(0.2f, 0.95f, 0.45f);
                        break;
                    case SignalState.Yellow:
                        dot.color = new Color(1f, 0.8f, 0.1f);
                        break;
                    default:
                        dot.color = new Color(1f, 0.25f, 0.2f);
                        break;
                }
            }
        }

        private void UpdateRacers(Vector3 position, float heading, float half)
        {
            while (_racerDots.Count < _racers.Count)
            {
                _racerDots.Add(CreateDot("Racer", 10f, RacerColor));
            }

            for (int i = 0; i < _racerDots.Count; i++)
            {
                _racerDots[i].enabled = i < _racers.Count && _racers[i] != null && Place(_racerDots[i], position, heading, _racers[i].position, half);
            }
        }

        // 把世界座標放到小地圖上，回傳是否在框內
        private bool Place(Image dot, Vector3 player, float heading, Vector3 world, float half)
        {
            Vector2 offset = MapProjection.ToHeadingUp(player, heading, world) / _metersPerPixel;
            if (Mathf.Abs(offset.x) > half || Mathf.Abs(offset.y) > half)
                return false;

            dot.rectTransform.anchoredPosition = offset;

            return true;
        }

        private Texture2D BuildTexture(WorldData data)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (EdgeData edge in data.edges)
            {
                foreach (RoutePoint point in edge.points)
                {
                    min = Vector2.Min(min, new Vector2(point.x, point.z));
                    max = Vector2.Max(max, new Vector2(point.x, point.z));
                }
            }

            foreach (BridgeData bridge in data.bridges)
            {
                foreach (RoutePoint point in bridge.points)
                {
                    min = Vector2.Min(min, new Vector2(point.x, point.z));
                    max = Vector2.Max(max, new Vector2(point.x, point.z));
                }
            }

            _projection = new MapProjection(min - Vector2.one * 80f, max + Vector2.one * 80f, TEXTURE_SIZE);
            float pixelsPerMeter = 1f / _projection.MetersPerPixel;
            Color32[] pixels = new Color32[TEXTURE_SIZE * TEXTURE_SIZE];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = BackgroundColor;
            }

            // 先畫巷弄，再畫主要道路蓋在上面
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (EdgeData edge in data.edges)
                {
                    if (edge.main != (pass == 1))
                        continue;

                    List<Vector2> line = new List<Vector2>(edge.points.Count);
                    foreach (RoutePoint point in edge.points)
                    {
                        line.Add(_projection.WorldToPixel(new Vector3(point.x, 0f, point.z)));
                    }

                    float radius = Mathf.Max(1.2f, edge.width / 2f * pixelsPerMeter);
                    MapTextureBuilder.DrawPolyline(pixels, TEXTURE_SIZE, line, radius, edge.main ? MainRoadColor : MinorRoadColor);
                }
            }

            // 高架橋畫在最上面
            foreach (BridgeData bridge in data.bridges)
            {
                List<Vector2> line = bridge.points.ConvertAll(p => _projection.WorldToPixel(new Vector3(p.x, 0f, p.z)));
                MapTextureBuilder.DrawPolyline(pixels, TEXTURE_SIZE, line, bridge.width / 2f * pixelsPerMeter, MainRoadColor);
            }

            Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, true)
            {
                name = "MiniMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            return texture;
        }

        private void BuildInterface()
        {
            _dotSprite = CreateSprite(16, (x, y) => (x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f) <= 56f);
            _arrowSprite = CreateSprite(32, (x, y) => y >= 2 && Mathf.Abs(x - 15.5f) <= (30 - y) * 0.42f);

            GameObject canvasObject = new GameObject("MiniMapCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            _frame = CreateRect("Frame", canvasObject.transform);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0f, 1f);
            _frame.anchoredPosition = new Vector2(MARGIN, -MARGIN);
            _frame.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.07f, 0.9f);

            RectTransform viewport = CreateRect("Viewport", _frame);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.one * BORDER;
            viewport.offsetMax = -Vector2.one * BORDER;
            viewport.gameObject.AddComponent<RectMask2D>();

            _map = CreateRect("Map", viewport).gameObject.AddComponent<RawImage>();
            _map.texture = _texture;
            _map.raycastTarget = false;

            _markers = CreateRect("Markers", viewport);
            for (int i = 0; i < _signals.Length; i++)
            {
                _signalDots.Add(CreateDot("Signal", 8f, Color.red));
            }

            // 下一個目標用空心圈，和現在要去的實心點區分
            _secondary = CreateDot("SecondaryTarget", 16f, new Color(1f, 0.2f, 0.25f));
            _secondary.sprite = CreateSprite(16, (x, y) =>
            {
                float squared = (x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f);

                return squared <= 56f && squared >= 25f;
            });
            _secondary.enabled = false;

            _target = CreateDot("Target", 14f, new Color(1f, 0.2f, 0.25f));
            _target.enabled = false;

            // 玩家箭頭固定在中央（正北朝上時會跟著面向旋轉）
            Image arrow = CreateRect("Player", _markers).gameObject.AddComponent<Image>();
            arrow.sprite = _arrowSprite;
            arrow.color = new Color(1f, 0.82f, 0.25f);
            arrow.rectTransform.sizeDelta = new Vector2(18f, 18f);
            arrow.raycastTarget = false;
            _playerArrow = arrow.rectTransform;

            // 指北針：圓底紅色 N，點一下切換正北朝上
            Image compass = CreateRect("Compass", _frame).gameObject.AddComponent<Image>();
            compass.sprite = _dotSprite;
            compass.color = new Color(0.05f, 0.06f, 0.07f, 0.9f);
            compass.rectTransform.sizeDelta = new Vector2(22f, 22f);
            compass.gameObject.AddComponent<Button>().onClick.AddListener(() => _northUp = !_northUp);
            _compass = compass.rectTransform;
            Text north = CreateRect("N", _compass).gameObject.AddComponent<Text>();
            north.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            north.fontSize = 15;
            north.fontStyle = FontStyle.Bold;
            north.alignment = TextAnchor.MiddleCenter;
            north.color = new Color(1f, 0.3f, 0.3f);
            north.text = "N";
            north.raycastTarget = false;
            north.rectTransform.sizeDelta = new Vector2(22f, 22f);

            Text hint = CreateRect("Hint", _frame).gameObject.AddComponent<Text>();
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 13;
            hint.color = new Color(1f, 1f, 1f, 0.85f);
            hint.text = "+ / - 或滾輪：縮放　M：大地圖　N：正北朝上";
            hint.raycastTarget = false;
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0f, 0f);
            hint.rectTransform.pivot = new Vector2(0f, 1f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            hint.rectTransform.sizeDelta = new Vector2(420f, 20f);

            // 畫面上方中央的大導航箭頭（在送貨地點文字下面）
            _navigationArrow = CreateRect("NavigationArrow", canvasObject.transform).gameObject.AddComponent<Image>();
            _navigationArrow.sprite = _arrowSprite;
            _navigationArrow.color = new Color(0.25f, 0.66f, 0.96f, 0.95f);
            _navigationArrow.raycastTarget = false;
            _navigationArrow.rectTransform.anchorMin = _navigationArrow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _navigationArrow.rectTransform.anchoredPosition = new Vector2(0f, -140f);
            _navigationArrow.rectTransform.sizeDelta = new Vector2(64f, 64f);
            _navigationArrow.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            _navigationArrow.enabled = false;
        }

        private Image CreateDot(string name, float size, Color color)
        {
            Image dot = CreateRect(name, _markers).gameObject.AddComponent<Image>();
            dot.sprite = _dotSprite;
            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.raycastTarget = false;

            return dot;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            return rect;
        }

        private static Sprite CreateSprite(int size, System.Func<int, int, bool> inside)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
