using System.Collections.Generic;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// 五股世界的 NPC 車流：只在玩家附近維持固定數量的車（車流圈），離太遠的車搬回玩家附近重新出現。
    /// 比賽模式可以整個關掉，讓道路淨空。
    /// </summary>
    public class WorldTraffic : MonoBehaviour
    {
        // 每隔多久檢查一次要不要搬車（秒）
        private const float RECYCLE_INTERVAL = 0.5f;
        // 生成時跟其他車至少保持的距離（公尺）
        private const float SPAWN_CLEARANCE = 10f;

        [SerializeField] private TextAsset _worldJson;
        [SerializeField] private WorldVehicle _scooterPrefab;
        [SerializeField] private WorldVehicle[] _bajiuScooterPrefabs = new WorldVehicle[0];
        [SerializeField] private WorldVehicle _carPrefab;
        [SerializeField] private WorldVehicle _truckPrefab;
        [SerializeField] private WorldVehicle _busPrefab;
        [SerializeField] private int _vehicleCount = 120;
        [SerializeField] private float _spawnRadius = 350f;
        [SerializeField] private float _despawnRadius = 450f;
        [SerializeField] private float _minSpawnDistance = 60f;
        [SerializeField] private int _seed = 11;

        private readonly List<WorldVehicle> _vehicles = new List<WorldVehicle>();
        private readonly List<int> _mainEdges = new List<int>();
        private readonly Dictionary<int, TrafficSignal> _signals = new Dictionary<int, TrafficSignal>();
        private float[] _junctionRadius;
        private float _mainLength;
        private Transform _player;
        private System.Random _random;
        private float _nextRecycle;
        private bool _enabled = true;

        /// <summary>路網。</summary>
        public RoadNetwork Network { get; private set; }

        /// <summary>所有 NPC 車輛。</summary>
        public IReadOnlyList<WorldVehicle> Vehicles => _vehicles;

        /// <summary>車流是否開啟。</summary>
        public bool TrafficEnabled => _enabled;

        /// <summary>
        /// 車道離道路中心的距離（往行進方向右側）：汽車道或機車騎乘區。
        /// </summary>
        public static float LaneOffset(EdgeData edge, bool scooter)
        {
            float half = edge.width / 2f;

            // 單行道（分隔島一側）：汽車靠中間、機車靠路邊
            if (edge.oneway != 0)
                return scooter ? half - 1.5f : 1f;

            float edgeLine = Mathf.Min(3.4f, half - 2.6f);

            return scooter ? edgeLine + 0.8f : 1.8f;
        }

        /// <summary>
        /// 路口半徑：接進來最寬的道路一半再加 1 公尺（與場景建置相同）。
        /// </summary>
        public float JunctionRadius(int junction)
        {
            return _junctionRadius[junction];
        }

        /// <summary>
        /// 從某條路段開進路口時要看的燈號；路口沒有紅綠燈回傳 false。
        /// </summary>
        public bool TryGetSignal(int junction, int edge, out SignalState state)
        {
            state = SignalState.Green;
            if (!_signals.TryGetValue(junction, out TrafficSignal signal))
                return false;

            state = signal.GetStateForEdge(edge);

            return true;
        }

        /// <summary>
        /// 開關車流：關閉時所有 NPC 消失（比賽淨空道路），開啟時在玩家附近重新出現。
        /// </summary>
        public void SetTrafficEnabled(bool enabled)
        {
            _enabled = enabled;
            foreach (WorldVehicle vehicle in _vehicles)
            {
                vehicle.gameObject.SetActive(enabled);
                if (enabled)
                    Respawn(vehicle);
            }
        }

        private void Awake()
        {
            Network = new RoadNetwork(WorldData.FromJson(_worldJson.text));
            WorldData data = Network.Data;
            _junctionRadius = new float[data.junctions.Count];
            foreach (EdgeData edge in data.edges)
            {
                _junctionRadius[edge.start] = Mathf.Max(_junctionRadius[edge.start], edge.width / 2f + 1f);
                _junctionRadius[edge.end] = Mathf.Max(_junctionRadius[edge.end], edge.width / 2f + 1f);

                // 只在夠長的主要道路上生成車輛
                float usable = Network.GetEdgePath(edge.id).Length - 2f * (edge.width / 2f + 1f) - 12f;
                if (edge.main && usable > 0f)
                {
                    _mainEdges.Add(edge.id);
                    _mainLength += usable;
                }
            }

            foreach (TrafficSignal signal in FindObjectsByType<TrafficSignal>(FindObjectsSortMode.None))
            {
                if (signal.JunctionId >= 0)
                    _signals[signal.JunctionId] = signal;
            }

            _random = new System.Random(_seed);
        }

        private void Start()
        {
            ScooterController player = FindFirstObjectByType<ScooterController>();
            _player = player != null ? player.transform : transform;
            for (int i = 0; i < _vehicleCount; i++)
            {
                WorldVehicle prefab = PickPrefab(_random.NextDouble());
                if (prefab == null)
                    continue;

                WorldVehicle vehicle = Instantiate(prefab, transform);
                vehicle.Initialize(this, _random);
                _vehicles.Add(vehicle);
                Respawn(vehicle);
            }
        }

        private WorldVehicle PickPrefab(double roll)
        {
            // 機車最多（其中約一成是 8+9，改裝車、破爛車各半），其次是汽車，少量貨車與公車
            if (roll < 0.45)
                return _scooterPrefab;

            if (roll < 0.5)
            {
                if (_bajiuScooterPrefabs.Length == 0)
                    return _scooterPrefab;

                int index = Mathf.Min((int)((roll - 0.45) / 0.05 * _bajiuScooterPrefabs.Length), _bajiuScooterPrefabs.Length - 1);

                return _bajiuScooterPrefabs[index];
            }

            if (roll < 0.85)
                return _carPrefab;

            if (roll < 0.95)
                return _truckPrefab;

            return _busPrefab;
        }

        private void Update()
        {
            if (!_enabled || Time.time < _nextRecycle)
                return;

            _nextRecycle = Time.time + RECYCLE_INTERVAL;
            Vector3 center = _player.position;
            foreach (WorldVehicle vehicle in _vehicles)
            {
                if ((vehicle.transform.position - center).sqrMagnitude > _despawnRadius * _despawnRadius)
                    Respawn(vehicle);
            }
        }

        private void Respawn(WorldVehicle vehicle)
        {
            if (_mainEdges.Count == 0)
                return;

            Vector3 center = _player != null ? _player.position : Vector3.zero;
            int edgeId = _mainEdges[0];
            bool forward = true;
            float distance = 0f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                // 依道路長度加權隨機挑一個位置
                float pick = (float)_random.NextDouble() * _mainLength;
                foreach (int candidate in _mainEdges)
                {
                    EdgeData edge = Network.Data.edges[candidate];
                    float margin = edge.width / 2f + 1f + 6f;
                    float usable = Network.GetEdgePath(candidate).Length - 2f * margin;
                    if (pick > usable)
                    {
                        pick -= usable;
                        continue;
                    }

                    edgeId = candidate;
                    distance = margin + pick;
                    forward = edge.oneway == 1 || (edge.oneway == 0 && _random.NextDouble() < 0.5);
                    break;
                }

                Vector3 position = Network.GetEdgePath(edgeId).GetPoint(distance);
                float fromPlayer = Vector3.Distance(position, center);
                if (fromPlayer >= _minSpawnDistance && fromPlayer <= _spawnRadius && !IsCrowded(position, vehicle))
                    break;
            }

            vehicle.PlaceOnEdge(edgeId, forward, distance);
        }

        private bool IsCrowded(Vector3 position, WorldVehicle except)
        {
            foreach (WorldVehicle other in _vehicles)
            {
                if (other != except && other.gameObject.activeSelf && (other.transform.position - position).sqrMagnitude < SPAWN_CLEARANCE * SPAWN_CLEARANCE)
                    return true;
            }

            return false;
        }
    }
}
