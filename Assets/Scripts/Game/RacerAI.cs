using System;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Game
{
    /// <summary>
    /// AI 比賽選手：沿著賽道行駛，依前方彎道調整速度，前面有車就減速。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RacerAI : MonoBehaviour
    {
        // 往前看多遠來判斷彎道（公尺）
        private const float LOOKAHEAD = 60f;
        // 過彎能承受的橫向加速度（m/s²）
        private const float LATERAL_GRIP = 7f;
        private const float ACCELERATION = 4.5f;
        private const float BRAKE = 9f;
        // 往前偵測其他選手的射線長度
        private const float SENSOR_RANGE = 15f;
        // 前方多近有人擋住就換車道超車（公尺）
        private const float OVERTAKE_DISTANCE = 12f;
        // 換車道的橫移速度（m/s）與兩次換道的最短間隔（秒）
        private const float LANE_CHANGE_SPEED = 2.5f;
        private const float LANE_CHANGE_COOLDOWN = 2f;

        [SerializeField] private float _maxSpeed = 26f;

        private RoutePath _track;
        private Rigidbody _body;
        private Func<float> _catchUp;
        private bool _loop;
        private float _distance;
        private float _lane;
        private float _laneTarget;
        private float _nextLaneChange;
        private float _speed;
        private bool _running;

        /// <summary>在賽道上的距離（繞圈賽每圈從 0 重新開始）。</summary>
        public float Distance => _distance;

        /// <summary>目前速度（m/s）。</summary>
        public float Speed => _speed;

        /// <summary>
        /// 放到起跑格。
        /// </summary>
        /// <param name="track">賽道</param>
        /// <param name="loop">是否為繞圈賽</param>
        /// <param name="distance">起跑位置（離起點的距離）</param>
        /// <param name="lane">橫向位置（往右為正）</param>
        /// <param name="catchUp">拉近差距的速度倍率</param>
        public void Setup(RoutePath track, bool loop, float distance, float lane, Func<float> catchUp)
        {
            _track = track;
            _loop = loop;
            _distance = distance;
            _lane = lane;
            _laneTarget = lane;
            _nextLaneChange = 0f;
            _catchUp = catchUp;
            _speed = 0f;
            _running = false;
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            Place(true);
        }

        /// <summary>
        /// 倒數結束，出發。
        /// </summary>
        public void Go()
        {
            _running = true;
        }

        private void FixedUpdate()
        {
            if (_track == null || !_running)
                return;

            float deltaTime = Time.fixedDeltaTime;
            float target = RacingLine.TargetSpeed(_track, _distance, _maxSpeed, LOOKAHEAD, LATERAL_GRIP) * _catchUp();

            // 衝刺賽到終點後慢慢停下來
            if (!_loop && _distance >= _track.Length - 1f)
                target = 0f;

            // 前面有人擋住：換到另一側車道超車，換道期間照樣保持車距
            float obstacle = SenseObstacle();
            if (obstacle < OVERTAKE_DISTANCE && Time.time >= _nextLaneChange)
            {
                _laneTarget = -_laneTarget;
                _nextLaneChange = Time.time + LANE_CHANGE_COOLDOWN;
            }

            _lane = Mathf.MoveTowards(_lane, _laneTarget, LANE_CHANGE_SPEED * deltaTime);
            target = TrafficRules.ComputeTargetSpeed(target, _speed, BRAKE, float.PositiveInfinity, SignalState.Green, obstacle);
            float rate = target > _speed ? ACCELERATION : BRAKE;
            _speed = Mathf.MoveTowards(_speed, target, rate * deltaTime);
            _distance += _speed * deltaTime;
            if (_loop && _distance >= _track.Length)
                _distance -= _track.Length;

            _distance = Mathf.Min(_distance, _track.Length);
            Place(false);
        }

        private float SenseObstacle()
        {
            Vector3 forward = transform.forward;
            Vector3 origin = transform.position + Vector3.up * 0.6f + forward * 1.05f;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, SENSOR_RANGE, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.rigidbody != null)
                return hit.distance;

            return float.PositiveInfinity;
        }

        private void Place(bool teleport)
        {
            Vector3 position = _track.GetPoint(_distance) + _track.GetRight(_distance) * _lane;
            Quaternion rotation = Quaternion.LookRotation(_track.GetTangent(Mathf.Min(_distance + 2f, _track.Length)));
            if (teleport)
            {
                _body.position = position;
                _body.rotation = rotation;
                transform.SetPositionAndRotation(position, rotation);

                return;
            }

            _body.MovePosition(position);
            _body.MoveRotation(Quaternion.Slerp(_body.rotation, rotation, 0.3f));
        }
    }
}
