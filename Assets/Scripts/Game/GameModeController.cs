using System.Collections.Generic;
using MotorAndSpeed.Audio;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.UI;
using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Game
{
    /// <summary>
    /// 五股世界的遊戲模式：自由騎乘、時限送貨（連續 / 單趟 / 取貨再送）、AI 比賽（衝刺 / 繞圈）。
    /// 負責目標點、導航線、計時、比賽起跑與名次，以及畫面上的資訊與結算面板。
    /// </summary>
    public class GameModeController : MonoBehaviour
    {
        private const float TIME_LIMIT = 300f;
        private const float BONUS_PER_DELIVERY = 45f;
        // 離目標點多近算抵達（公尺）
        private const float REACH_RADIUS = 9f;
        private const int CIRCUIT_LAPS = 3;
        private const float COUNTDOWN = 3f;
        // 每隔多久重新計算導航線（秒）
        private const float GUIDE_INTERVAL = 2f;

        private static readonly Color DeliveryColor = new Color(1f, 0.25f, 0.25f);
        private static readonly Color PickupColor = new Color(0.25f, 0.9f, 0.45f);

        [SerializeField] private TextAsset _worldJson;
        [SerializeField] private RacerAI[] _racerPrefabs = new RacerAI[0];
        [SerializeField] private Material _deliveryBeacon;
        [SerializeField] private Material _pickupBeacon;
        [SerializeField] private Material _fenceSign;
        [SerializeField] private Material _fenceFrame;

        private readonly List<RacerAI> _racers = new List<RacerAI>();
        private readonly List<GameObject> _fences = new List<GameObject>();
        private ScooterController _player;
        private WorldTraffic _traffic;
        private MiniMap _miniMap;
        private RoadNetwork _network;
        private System.Random _random;
        private GameMode _mode = GameMode.FreeRide;

        // 送貨
        private DeliverySession _delivery;
        private DestinationData _target;
        // 取貨模式：開始時就決定好客人位置，取貨時先在地圖上標出來
        private DestinationData _plannedDropoff;
        private Vector3 _pickupPoint;
        private float _deliveryClock;
        private Transform _beacon;
        private Renderer[] _beaconRenderers = new Renderer[0];
        private float _nextGuide;

        // 比賽
        private RoutePath _track;
        private bool _loop;
        private RaceProgress _race;
        private float _countdown;
        // 倒數時最後一次嗶聲是第幾秒
        private int _beepedSecond;
        private bool _racing;
        private float _raceClock;
        private float _finishTime = -1f;

        private string _banner;
        private float _bannerUntil;
        private GUIStyle _bigStyle;
        private GUIStyle _hudStyle;
        private GUIStyle _panelStyle;

        /// <summary>目前的模式。</summary>
        public GameMode Mode => _mode;

        /// <summary>送貨進度（不是送貨模式時為 null）。</summary>
        public DeliverySession Delivery => _delivery;

        /// <summary>目前的目標點（送貨用）。</summary>
        public Vector3 TargetPoint => _target == null ? Vector3.zero : new Vector3(_target.x, 0f, _target.z);

        /// <summary>比賽進度（不是比賽模式時為 null）。</summary>
        public RaceProgress Race => _race;

        /// <summary>AI 選手。</summary>
        public IReadOnlyList<RacerAI> Racers => _racers;

        /// <summary>比賽是否已經起跑（倒數結束）。</summary>
        public bool RaceStarted => _racing;

        /// <summary>比賽時圍住非賽道支線的圍欄。</summary>
        public IReadOnlyList<GameObject> Fences => _fences;

        /// <summary>
        /// 切換到指定模式（會先結束目前的模式）。
        /// </summary>
        public void StartMode(GameMode mode)
        {
            EndCurrentMode();
            _mode = mode;
            if (ModeMenu.Instance != null)
                ModeMenu.Instance.ShowMode(mode);

            switch (mode)
            {
                case GameMode.DeliveryChain:
                    StartDelivery(DeliveryKind.Chain);
                    break;
                case GameMode.DeliverySingle:
                    StartDelivery(DeliveryKind.Single);
                    break;
                case GameMode.DeliveryPickup:
                    StartDelivery(DeliveryKind.PickupDropoff);
                    break;
                case GameMode.RaceSprint:
                    StartRace(false);
                    break;
                case GameMode.RaceCircuit:
                    StartRace(true);
                    break;
            }
        }

        private void Awake()
        {
            _network = new RoadNetwork(WorldData.FromJson(_worldJson.text));
            _random = new System.Random();
        }

        private void Start()
        {
            _player = FindFirstObjectByType<ScooterController>();
            _traffic = FindFirstObjectByType<WorldTraffic>();
            _miniMap = FindFirstObjectByType<MiniMap>();
        }

        private void Update()
        {
            if (_delivery != null)
                UpdateDelivery();

            if (_race != null)
                UpdateRace();
        }

        private void EndCurrentMode()
        {
            if (_beacon != null)
                Destroy(_beacon.gameObject);

            foreach (RacerAI racer in _racers)
            {
                Destroy(racer.gameObject);
            }

            _racers.Clear();
            foreach (GameObject fence in _fences)
            {
                Destroy(fence);
            }

            _fences.Clear();
            _delivery = null;
            _target = null;
            _plannedDropoff = null;
            _race = null;
            _track = null;
            _racing = false;
            _finishTime = -1f;
            _banner = null;
            if (_traffic != null && !_traffic.TrafficEnabled)
                _traffic.SetTrafficEnabled(true);

            if (_player != null)
            {
                _player.InputLocked = false;
                _player.RespawnOverride = null;
            }

            if (_miniMap != null)
            {
                _miniMap.SetGuide(null);
                _miniMap.SetTarget(null, DeliveryColor);
                _miniMap.SetSecondaryTarget(null);
                _miniMap.SetRacers(null);
            }
        }

        private void StartDelivery(DeliveryKind kind)
        {
            _delivery = new DeliverySession(kind, TIME_LIMIT, BONUS_PER_DELIVERY);
            _deliveryClock = 0f;
            _pickupPoint = _player.transform.position;

            // 送貨時按 R 回到最近的路口
            _player.RespawnOverride = () =>
            {
                JunctionData junction = _network.Data.junctions[_network.FindNearestJunction(_player.transform.position)];

                return (new Vector3(junction.x, 0f, junction.z), _player.transform.rotation);
            };
            PickNextTarget();
            ShowBanner(kind == DeliveryKind.PickupDropoff ? "先到店家取貨！" : "開始送貨！");
        }

        private void UpdateDelivery()
        {
            if (_delivery.IsOver)
                return;

            _delivery.Tick(Time.deltaTime);
            _deliveryClock += Time.deltaTime;
            if (_delivery.IsOver)
            {
                GameSounds.Play(GameSound.Finish);
                ClearTarget();

                return;
            }

            Vector3 position = _player.transform.position;
            Vector3 target = TargetPoint;
            if (new Vector2(position.x - target.x, position.z - target.z).magnitude <= REACH_RADIUS)
            {
                DeliveryStage before = _delivery.Stage;
                _delivery.ReachTarget();
                if (_delivery.IsOver)
                {
                    GameSounds.Play(GameSound.Finish);
                    ClearTarget();

                    return;
                }

                GameSounds.Play(GameSound.Checkpoint);
                if (before == DeliveryStage.Pickup)
                {
                    _pickupPoint = target;
                    ShowBanner("取貨完成！送去給客人");
                }
                else
                {
                    ShowBanner($"送達！+{BONUS_PER_DELIVERY:F0} 秒");
                }

                PickNextTarget();
            }

            if (Time.time >= _nextGuide)
                UpdateGuide();
        }

        private void PickNextTarget()
        {
            Vector3 position = _player.transform.position;
            bool pickup = _delivery.Stage == DeliveryStage.Pickup;
            switch (_delivery.Kind)
            {
                case DeliveryKind.Single:
                    _target = ChooseDestination(position, 900f, float.MaxValue, _target);
                    break;
                case DeliveryKind.PickupDropoff when pickup:
                    // 取貨點在附近，客人在取貨點的另一頭；兩個地點一開始就都標在地圖上
                    _target = ChooseDestination(position, 250f, 700f, null);
                    Vector3 pickupPoint = new Vector3(_target.x, 0f, _target.z);
                    _plannedDropoff = ChooseDestination(pickupPoint, 600f, 1400f, _target);
                    _miniMap.SetSecondaryTarget(new Vector3(_plannedDropoff.x, 0f, _plannedDropoff.z));
                    break;
                case DeliveryKind.PickupDropoff:
                    _target = _plannedDropoff ?? ChooseDestination(_pickupPoint, 600f, 1400f, _target);
                    _plannedDropoff = null;
                    _miniMap.SetSecondaryTarget(null);
                    break;
                default:
                    _target = ChooseDestination(position, 300f, 1100f, _target);
                    break;
            }

            PlaceBeacon(TargetPoint, pickup ? _pickupBeacon : _deliveryBeacon);
            _miniMap.SetTarget(TargetPoint, pickup ? PickupColor : DeliveryColor);
            UpdateGuide();
        }

        private DestinationData ChooseDestination(Vector3 from, float minDistance, float maxDistance, DestinationData exclude)
        {
            List<DestinationData> candidates = new List<DestinationData>();
            foreach (DestinationData destination in _network.Data.destinations)
            {
                float distance = Vector3.Distance(from, new Vector3(destination.x, 0f, destination.z));
                if (destination != exclude && distance >= minDistance && distance <= maxDistance)
                    candidates.Add(destination);
            }

            // 找不到符合距離的地點時，任選一個
            if (candidates.Count == 0)
                candidates.AddRange(_network.Data.destinations.FindAll(d => d != exclude));

            return candidates[_random.Next(candidates.Count)];
        }

        private void UpdateGuide()
        {
            _nextGuide = Time.time + GUIDE_INTERVAL;
            Vector3 from = _player.transform.position;
            Vector3 to = TargetPoint;
            int start = _network.FindNearestJunction(from);
            int end = _network.FindNearestJunction(to);
            List<int> path = _network.FindPath(start, end);
            List<Vector3> points = new List<Vector3> { from };
            if (path != null)
                points.AddRange(_network.ChainPoints(start, path));

            points.Add(to);
            _miniMap.SetGuide(points);
        }

        private void ClearTarget()
        {
            if (_beacon != null)
                _beacon.gameObject.SetActive(false);

            _miniMap.SetGuide(null);
            _miniMap.SetTarget(null, DeliveryColor);
        }

        private void PlaceBeacon(Vector3 position, Material material)
        {
            if (_beacon == null)
            {
                // 目標光柱：高高的發光柱子加上地面光圈，遠遠就看得到
                _beacon = new GameObject("TargetBeacon").transform;
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.transform.SetParent(_beacon, false);
                pillar.transform.localPosition = new Vector3(0f, 20f, 0f);
                pillar.transform.localScale = new Vector3(1.2f, 20f, 1.2f);
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.transform.SetParent(_beacon, false);
                ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                ring.transform.localScale = new Vector3(REACH_RADIUS * 2f, 0.03f, REACH_RADIUS * 2f);
                foreach (Collider collider in _beacon.GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }

                _beaconRenderers = _beacon.GetComponentsInChildren<Renderer>();
            }

            _beacon.gameObject.SetActive(true);
            _beacon.position = position;
            foreach (Renderer renderer in _beaconRenderers)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void StartRace(bool loop)
        {
            _traffic.SetTrafficEnabled(false);
            WorldData data = _network.Data;
            List<Vector3> points = (loop ? data.circuit : data.sprint).points.ConvertAll(p => new Vector3(p.x, 0f, p.z));
            if (loop && Vector3.Distance(points[0], points[points.Count - 1]) > 1f)
                points.Add(points[0]);

            _track = new RoutePath(points);
            _loop = loop;
            foreach (FencePlacement placement in RaceFencing.Plan(_network, _track))
            {
                _fences.Add(BuildFence(placement));
            }

            _race = new RaceProgress(1 + _racerPrefabs.Length, _track.Length, loop ? CIRCUIT_LAPS : 1, loop);

            // 起跑格：四台並排（玩家在最右側），起跑時誰都不會擋到誰
            float[] slots = { 8f, 8f, 8f, 8f };
            float[] lanes = { 2.4f, 0.8f, -0.8f, -2.4f };
            _player.TeleportTo(_track.GetPoint(slots[0]) + _track.GetRight(slots[0]) * lanes[0] + Vector3.up * 0.05f, Quaternion.LookRotation(_track.GetTangent(slots[0])));
            _player.InputLocked = true;
            _player.RespawnOverride = () =>
            {
                float distance = _track.GetClosestDistance(_player.transform.position);

                return (_track.GetPoint(distance), Quaternion.LookRotation(_track.GetTangent(distance)));
            };

            List<Transform> markers = new List<Transform>();
            for (int i = 0; i < _racerPrefabs.Length; i++)
            {
                RacerAI racer = Instantiate(_racerPrefabs[i]);
                int index = i + 1;
                racer.Setup(_track, loop, slots[index], lanes[index], () => RacingLine.CatchUpFactor(_race.GetProgress(index), _race.GetProgress(0)));
                _race.Update(index, slots[index]);
                _racers.Add(racer);
                markers.Add(racer.transform);
            }

            _race.Update(0, slots[0]);
            _miniMap.SetRacers(markers);
            _miniMap.SetGuide(points);
            _miniMap.SetTarget(loop ? (Vector3?)null : points[points.Count - 1], DeliveryColor);
            _countdown = COUNTDOWN;
            _beepedSecond = 0;
            _raceClock = 0f;
        }

        private GameObject BuildFence(FencePlacement placement)
        {
            // 區域座標：+Z 朝支線外、-Z 那一面朝向路口與賽道
            GameObject root = new GameObject("RaceFence");
            root.transform.SetPositionAndRotation(placement.Position, Quaternion.LookRotation(placement.Outward));

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            board.transform.localScale = new Vector3(placement.Width, 1.2f, 0.25f);
            board.GetComponent<Renderer>().sharedMaterial = _fenceFrame;

            // 箭頭面：箭頭貼圖預設朝右，比賽方向在左邊時把整面轉 180 度
            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sign.name = "Arrows";
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0f, 0.65f, -0.13f);
            sign.transform.localRotation = placement.ArrowsRight ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);
            sign.transform.localScale = new Vector3(placement.Width, 0.9f, 1f);
            Destroy(sign.GetComponent<Collider>());
            Renderer renderer = sign.GetComponent<Renderer>();
            renderer.sharedMaterial = _fenceSign;

            // 依圍欄長度重複箭頭，每 1.2 公尺一個
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetVector("_BaseMap_ST", new Vector4(Mathf.Max(1f, Mathf.Round(placement.Width / 1.2f)), 1f, 0f, 0f));
            renderer.SetPropertyBlock(block);

            return root;
        }

        private void UpdateRace()
        {
            if (!_racing)
            {
                _countdown -= Time.deltaTime;
                if (_countdown > 0f)
                {
                    // 3、2、1 每秒嗶一聲
                    int second = Mathf.CeilToInt(_countdown);
                    if (second != _beepedSecond)
                    {
                        _beepedSecond = second;
                        GameSounds.Play(GameSound.CountdownTick);
                    }

                    return;
                }

                _racing = true;
                _player.InputLocked = false;
                foreach (RacerAI racer in _racers)
                {
                    racer.Go();
                }

                GameSounds.Play(GameSound.CountdownGo);
                ShowBanner("出發！");
            }

            if (_finishTime < 0f)
                _raceClock += Time.deltaTime;

            _race.Update(0, _track.GetClosestDistance(_player.transform.position));
            for (int i = 0; i < _racers.Count; i++)
            {
                _race.Update(i + 1, _racers[i].Distance);
            }

            if (_finishTime < 0f && _race.IsFinished(0))
            {
                _finishTime = _raceClock;
                GameSounds.Play(GameSound.Finish);
            }
        }

        private void ShowBanner(string text)
        {
            _banner = text;
            _bannerUntil = Time.time + 2.5f;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_banner != null && Time.time < _bannerUntil)
                GUI.Label(new Rect(0f, Screen.height * 0.25f, Screen.width, 80f), _banner, _bigStyle);

            if (_delivery != null)
                DrawDeliveryHud();

            if (_race != null)
                DrawRaceHud();
        }

        private void DrawDeliveryHud()
        {
            if (_delivery.IsOver)
            {
                string result;
                if (_delivery.Kind == DeliveryKind.Chain)
                {
                    result = $"時間到！\n共送達 {_delivery.Delivered} 單";
                }
                else
                {
                    result = _delivery.Succeeded ? $"準時送達！\n用時 {FormatTime(_deliveryClock)}" : "超時了…\n下次再快一點！";
                }

                DrawResultPanel(result);

                return;
            }

            float remaining = _delivery.TimeRemaining;
            _bigStyle.normal.textColor = remaining < 30f ? new Color(1f, 0.35f, 0.3f) : Color.white;
            GUI.Label(new Rect(0f, 12f, Screen.width, 60f), FormatTime(remaining), _bigStyle);
            _bigStyle.normal.textColor = Color.white;

            string action = _delivery.Stage == DeliveryStage.Pickup ? "取貨" : "送到";
            float distance = Vector3.Distance(_player.transform.position, TargetPoint);
            string count = _delivery.Kind == DeliveryKind.Chain ? $"　已送 {_delivery.Delivered} 單" : string.Empty;
            string next = _plannedDropoff != null ? $"　→ 再送到：{_plannedDropoff.name}" : string.Empty;
            GUI.Label(new Rect(0f, 70f, Screen.width, 30f), $"{action}：{_target.name}（還有 {distance:F0} 公尺）{next}{count}", _hudStyle);
        }

        private void DrawRaceHud()
        {
            if (!_racing)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 120f), Mathf.CeilToInt(_countdown).ToString(), _bigStyle);

                return;
            }

            int racers = _racers.Count + 1;
            string lap = _loop ? $"　第 {_race.GetLap(0)} / {CIRCUIT_LAPS} 圈" : string.Empty;
            GUI.Label(new Rect(0f, 12f, Screen.width, 60f), $"第 {_race.GetPosition(0)} / {racers} 名{lap}", _bigStyle);
            GUI.Label(new Rect(0f, 70f, Screen.width, 30f), $"時間 {FormatTime(_finishTime >= 0f ? _finishTime : _raceClock)}", _hudStyle);

            if (_finishTime >= 0f)
                DrawResultPanel($"完賽！第 {_race.GetPosition(0)} 名\n時間 {FormatTime(_finishTime)}");
        }

        private void DrawResultPanel(string message)
        {
            Rect panel = new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 110f, 400f, 220f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 100f), message, _panelStyle);
            if (GUI.Button(new Rect(panel.x + 30f, panel.y + 150f, 160f, 44f), "再玩一次"))
            {
                GameSounds.Play(GameSound.Click);
                StartMode(_mode);
            }

            if (GUI.Button(new Rect(panel.x + 210f, panel.y + 150f, 160f, 44f), "回自由騎乘"))
            {
                GameSounds.Play(GameSound.Click);
                StartMode(GameMode.FreeRide);
            }
        }

        private void EnsureStyles()
        {
            if (_bigStyle != null)
                return;

            _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _bigStyle.normal.textColor = Color.white;
            _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _hudStyle.normal.textColor = Color.white;
            _panelStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _panelStyle.normal.textColor = Color.white;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));

            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
