using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// 世界路網上的 NPC 車輛：沿著路段行駛、停紅燈、跟前車保持距離，到路口時選下一條路並沿曲線轉過去。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WorldVehicle : MonoBehaviour
    {
        // 往前偵測前車的射線長度
        private const float SENSOR_RANGE = 30f;
        // 停止線離路口邊緣的距離
        private const float STOP_LINE_GAP = 4.6f;

        [SerializeField] private float _cruiseSpeed = 13f;
        [SerializeField] private float _speedVariance = 2f;
        [SerializeField] private float _acceleration = 3f;
        [SerializeField] private float _deceleration = 5f;
        [SerializeField] private float _length = 4.5f;
        [SerializeField] private float _sensorHeight = 0.7f;
        [SerializeField] private PaintSlot[] _paintSlots = new PaintSlot[0];

        private WorldTraffic _traffic;
        private Rigidbody _body;
        private System.Random _random;
        private int _edge;
        private bool _forward;
        private float _distance;
        private float _speed;
        private float _cruise;
        // 過路口時沿著貝茲曲線走
        private bool _turning;
        private Vector3 _curveStart;
        private Vector3 _curveControl;
        private Vector3 _curveEnd;
        private float _curveLength;
        private float _curveProgress;
        private int _nextEdge;
        private bool _nextForward;
        private float _nextDistance;

        /// <summary>目前速度（m/s）。</summary>
        public float Speed => _speed;

        /// <summary>車長（公尺）。</summary>
        public float Length => _length;

        /// <summary>是否為機車（走機車騎乘區）。</summary>
        public bool IsScooter => _length < 2.5f;

        /// <summary>目前所在路段。</summary>
        public int Edge => _edge;

        /// <summary>是否正在通過路口。</summary>
        public bool IsTurning => _turning;

        /// <summary>前方要開往的路口。</summary>
        public int AheadJunction
        {
            get
            {
                EdgeData edge = _traffic.Network.Data.edges[_edge];

                return _forward ? edge.end : edge.start;
            }
        }

        /// <summary>離前方路口中心的距離（公尺）。</summary>
        public float DistanceToJunction => _forward ? _traffic.Network.GetEdgePath(_edge).Length - _distance : _distance;

        /// <summary>
        /// 生成時由 WorldTraffic 呼叫：設定隨機速度與顏色。
        /// </summary>
        public void Initialize(WorldTraffic traffic, System.Random random)
        {
            _traffic = traffic;
            _random = random;
            _cruise = _cruiseSpeed + (float)(random.NextDouble() * 2.0 - 1.0) * _speedVariance;
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            Paint(random);
        }

        /// <summary>
        /// 直接放到某條路段上（生成或搬回玩家附近時使用）。
        /// </summary>
        public void PlaceOnEdge(int edge, bool forward, float distance)
        {
            _edge = edge;
            _forward = forward;
            _distance = distance;
            _turning = false;
            _speed = _cruise * 0.6f;
            Place(true);
        }

        private void FixedUpdate()
        {
            if (_traffic == null)
                return;

            float deltaTime = Time.fixedDeltaTime;
            float obstacle = SenseObstacle();

            if (_turning)
            {
                // 已經進入路口，不再理會號誌，直接通過
                Accelerate(TrafficRules.ComputeTargetSpeed(_cruise * 0.7f, _speed, _deceleration, float.PositiveInfinity, SignalState.Green, obstacle), deltaTime);
                _curveProgress += _speed * deltaTime;
                if (_curveProgress >= _curveLength)
                {
                    _edge = _nextEdge;
                    _forward = _nextForward;
                    _distance = _nextDistance;
                    _turning = false;
                }

                Place(false);

                return;
            }

            EdgeData edge = _traffic.Network.Data.edges[_edge];
            RoutePath path = _traffic.Network.GetEdgePath(_edge);
            int ahead = _forward ? edge.end : edge.start;
            float radius = _traffic.JunctionRadius(ahead);
            float toCenter = _forward ? path.Length - _distance : _distance;

            // 前方路口有紅綠燈：停在停止線前
            float stopLine = float.PositiveInfinity;
            SignalState signal = SignalState.Green;
            if (_traffic.TryGetSignal(ahead, _edge, out SignalState state))
            {
                float gap = toCenter - radius - STOP_LINE_GAP;
                if (gap > -0.5f)
                {
                    stopLine = Mathf.Max(0f, gap);
                    signal = state;
                }
            }

            Accelerate(TrafficRules.ComputeTargetSpeed(_cruise, _speed, _deceleration, stopLine, signal, obstacle), deltaTime);
            _distance += (_forward ? 1f : -1f) * _speed * deltaTime;
            if (toCenter - _speed * deltaTime <= radius)
                BeginTurn(ahead);

            Place(false);
        }

        private void Accelerate(float target, float deltaTime)
        {
            // 緊急時允許兩倍減速度
            float rate = target > _speed ? _acceleration : _deceleration * 2f;
            _speed = Mathf.MoveTowards(_speed, target, rate * deltaTime);
        }

        private void BeginTurn(int junction)
        {
            RoadNetwork network = _traffic.Network;
            _nextEdge = TrafficRouting.ChooseNextEdge(network.Data, junction, _edge, true, _random, out _nextForward);
            RoutePath next = network.GetEdgePath(_nextEdge);
            float radius = _traffic.JunctionRadius(junction);
            _nextDistance = Mathf.Clamp(_nextForward ? radius : next.Length - radius, 0f, next.Length);

            JunctionData center = network.Data.junctions[junction];
            _curveStart = CurrentPosition();
            _curveEnd = LanePoint(_nextEdge, _nextForward, _nextDistance);
            _curveControl = new Vector3(center.x, 0f, center.z);
            _curveLength = Mathf.Max(0.5f, TrafficRouting.BezierLength(_curveStart, _curveControl, _curveEnd));
            _curveProgress = 0f;
            _turning = true;
        }

        private float SenseObstacle()
        {
            // 從車頭往前射線：只理會有剛體的東西（其他車輛、玩家），忽略建築和電線桿
            Vector3 forward = transform.forward;
            Vector3 origin = transform.position + Vector3.up * _sensorHeight + forward * (_length * 0.5f + 0.05f);
            if (Physics.Raycast(origin, forward, out RaycastHit hit, SENSOR_RANGE, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.rigidbody != null)
                return hit.distance;

            return float.PositiveInfinity;
        }

        private Vector3 CurrentPosition()
        {
            if (_turning)
                return TrafficRouting.Bezier(_curveStart, _curveControl, _curveEnd, Mathf.Clamp01(_curveProgress / _curveLength));

            return LanePoint(_edge, _forward, _distance);
        }

        private Vector3 LanePoint(int edge, bool forward, float distance)
        {
            RoutePath path = _traffic.Network.GetEdgePath(edge);
            float offset = WorldTraffic.LaneOffset(_traffic.Network.Data.edges[edge], IsScooter);

            return path.GetPoint(distance) + path.GetRight(distance) * (forward ? offset : -offset);
        }

        private void Place(bool teleport)
        {
            Vector3 position = CurrentPosition();
            Vector3 direction;
            if (_turning)
            {
                float t = Mathf.Clamp01(_curveProgress / _curveLength);
                direction = TrafficRouting.Bezier(_curveStart, _curveControl, _curveEnd, Mathf.Min(1f, t + 0.05f)) - TrafficRouting.Bezier(_curveStart, _curveControl, _curveEnd, Mathf.Max(0f, t - 0.05f));
            }
            else
            {
                direction = _traffic.Network.GetEdgePath(_edge).GetTangent(_distance) * (_forward ? 1f : -1f);
            }

            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : transform.rotation;
            if (teleport)
            {
                _body.position = position;
                _body.rotation = rotation;
                transform.SetPositionAndRotation(position, rotation);

                return;
            }

            _body.MovePosition(position);
            _body.MoveRotation(rotation);
        }

        private void Paint(System.Random random)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (PaintSlot slot in _paintSlots)
            {
                if (slot.Options == null || slot.Options.Length == 0)
                    continue;

                Material chosen = slot.Options[random.Next(slot.Options.Length)];
                foreach (Renderer renderer in renderers)
                {
                    Material[] materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null && materials[i].name == slot.MaterialName)
                        {
                            materials[i] = chosen;
                            changed = true;
                        }
                    }

                    if (changed)
                        renderer.sharedMaterials = materials;
                }
            }
        }
    }
}
