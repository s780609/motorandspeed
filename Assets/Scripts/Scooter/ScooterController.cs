using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotorAndSpeed.Scooter
{
    /// <summary>
    /// 街機式速克達操控：碰撞體保持直立，傾斜與俯仰只套用在外觀（Visual）上。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ScooterController : MonoBehaviour
    {
        // 射線起點離車底的高度，必須在碰撞體內部，射線才不會打到自己
        private const float PROBE_HEIGHT = 0.3f;
        // 車底離地多少公尺以內算著地
        private const float GROUNDED_TOLERANCE = 0.08f;
        // 方向輸入的變化速度，鍵盤才不會一按就打到底
        private const float STEER_RESPONSE = 4f;
        // 掉到這個高度以下自動回到起點
        private const float FALL_LIMIT_Y = -20f;
        // 接觸面朝上超過這個程度視為地面（地面、斜坡不算撞擊）
        private const float GROUND_NORMAL_Y = 0.6f;
        // 撞擊偏轉套用的速度：約 0.3 秒內偏完
        private const float IMPACT_YAW_RESPONSE = 10f;
        // 車身左右晃動的最大角度與持續時間
        private const float IMPACT_WOBBLE_DEGREES = 8f;
        private const float IMPACT_WOBBLE_DURATION = 0.8f;

        [SerializeField] private ScooterSettings _settings = new ScooterSettings();
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _handlebar;
        [SerializeField] private Transform _frontWheel;
        [SerializeField] private Transform _rearWheel;
        [SerializeField] private float _wheelRadius = 0.24f;
        [SerializeField] private float _maxHandlebarAngle = 15f;
        [SerializeField] private float _visualResponsiveness = 8f;

        private Rigidbody _body;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _moveDirection;
        private float _speed;
        private float _throttle;
        private float _brake;
        private float _steer;
        private float _lean;
        private float _pitch;
        private float _wheelSpin;
        private bool _isGrounded;
        // 還沒套用完的撞擊偏轉角度
        private float _pendingImpactYaw;
        private float _impactTime = -10f;
        private float _impactStrength;
        // 最近一次著地時，沿著坡面前進造成的上升速度（m/s）
        private float _slopeRiseSpeed;

        /// <summary>被撞時觸發，參數為晃動強度（0~1），鏡頭用來晃動。</summary>
        public event Action<float> Impacted;

        /// <summary>目前速度（m/s），負數代表倒車。</summary>
        public float Speed => _speed;

        /// <summary>目前油門 0~1（引擎聲用）。</summary>
        public float Throttle => _throttle;

        /// <summary>是否著地。</summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>極速（m/s）。</summary>
        public float MaxSpeed => _settings.MaxSpeed;

        /// <summary>鎖住玩家輸入（比賽倒數時不能偷跑）。</summary>
        public bool InputLocked { get; set; }

        /// <summary>按 R 時要回到的位置；沒設定時回到起點（遊戲模式會改成賽道或道路上最近的點）。</summary>
        public Func<(Vector3 Position, Quaternion Rotation)> RespawnOverride { get; set; }

        /// <summary>
        /// 瞬間移動到指定位置並停住（遊戲模式用來放到起跑格）。
        /// </summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            _speed = 0f;
            _pendingImpactYaw = 0f;
            _slopeRiseSpeed = 0f;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _moveDirection = rotation * Vector3.forward;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            // 三軸都鎖住：被撞時物理不會讓車子打轉，轉向完全由 MoveRotation 控制
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _moveDirection = transform.forward;
        }

        private void Update()
        {
            ReadInput();
            UpdateVisuals(Time.deltaTime);

            if (transform.position.y < FALL_LIMIT_Y)
                Respawn();
        }

        private void FixedUpdate()
        {
            // 斜斜擦上路緣石時，物理引擎會把撞進去的速度轉成往上的速度，整台車彈得很高；
            // 往上的速度只保留斜坡造成的部分，衝上跳台照樣能飛出去
            Vector3 velocity = _body.linearVelocity;
            float limitedRise = ScooterPhysics.LimitRiseSpeed(velocity.y, _slopeRiseSpeed, _settings);
            if (limitedRise < velocity.y)
                _body.linearVelocity = new Vector3(velocity.x, limitedRise, velocity.z);

            Vector3 probeOrigin = transform.position + Vector3.up * PROBE_HEIGHT;
            _isGrounded = GroundProbe.TryGetGround(probeOrigin, PROBE_HEIGHT + GROUNDED_TOLERANCE, out RaycastHit hit);
            float deltaTime = Time.fixedDeltaTime;

            // 撞擊偏轉分幾幀慢慢套用，看起來是被撞歪而不是瞬間轉向
            float impactYaw = _pendingImpactYaw * (1f - Mathf.Exp(-IMPACT_YAW_RESPONSE * deltaTime));
            _pendingImpactYaw -= impactYaw;

            // 騰空時不能加速或轉向，交給重力
            if (!_isGrounded)
            {
                if (impactYaw != 0f)
                    _body.MoveRotation(_body.rotation * Quaternion.Euler(0f, impactYaw, 0f));

                return;
            }

            SyncSpeedWithCollisions();

            _speed = ScooterPhysics.UpdateSpeed(_speed, _throttle, _brake, deltaTime, _settings);
            float yaw = ScooterPhysics.ComputeYawRate(_steer, _speed, _settings) * deltaTime + impactYaw;
            _body.MoveRotation(_body.rotation * Quaternion.Euler(0f, yaw, 0f));

            // 沿著地面坡度前進，衝上斜坡就會自然飛出去
            _moveDirection = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            _body.linearVelocity = _moveDirection * _speed;
            _slopeRiseSpeed = Mathf.Max(0f, _body.linearVelocity.y);
        }

        private void OnCollisionEnter(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            if (contact.normal.y > GROUND_NORMAL_Y)
                return;

            // 撞到很輕的東西（例如交通錐）幾乎沒感覺；撞到牆或 NPC 車輛就是全力
            Rigidbody other = collision.rigidbody;
            float massFactor = other == null || other.isKinematic ? 1f : Mathf.Clamp01(other.mass / _body.mass);
            float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)) * massFactor;
            float hitSide = Vector3.Dot(contact.point - transform.position, transform.right);

            _pendingImpactYaw += ScooterPhysics.ComputeImpactYaw(impactSpeed, hitSide, _settings);
            float shake = ScooterPhysics.ComputeImpactShake(impactSpeed, _settings);
            if (shake <= 0f)
                return;

            _impactTime = Time.time;
            _impactStrength = shake;
            Impacted?.Invoke(shake);
        }

        private void ReadInput()
        {
            float throttle = 0f;
            float brake = 0f;
            float steer = 0f;
            bool reset = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    throttle = 1f;

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    brake = 1f;

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    steer -= 1f;

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    steer += 1f;

                reset = keyboard.rKey.wasPressedThisFrame;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                throttle = Mathf.Max(throttle, gamepad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, gamepad.leftTrigger.ReadValue());
                float stick = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(stick) > Mathf.Abs(steer))
                    steer = stick;

                reset |= gamepad.buttonNorth.wasPressedThisFrame;
            }

            if (InputLocked)
            {
                throttle = 0f;
                brake = 0f;
                steer = 0f;
                reset = false;
            }

            _throttle = throttle;
            _brake = brake;
            _steer = Mathf.MoveTowards(_steer, steer, STEER_RESPONSE * Time.deltaTime);

            if (reset)
                Respawn();
        }

        private void SyncSpeedWithCollisions()
        {
            // 撞到東西時實際速度會變小，把速度同步回來，才不會一直推著牆跑
            float actualSpeed = Vector3.Dot(_body.linearVelocity, _moveDirection);
            if (_speed > 0f)
            {
                _speed = Mathf.Min(_speed, Mathf.Max(0f, actualSpeed));
            }
            else
            {
                _speed = Mathf.Max(_speed, Mathf.Min(0f, actualSpeed));
            }
        }

        private void UpdateVisuals(float deltaTime)
        {
            float blend = 1f - Mathf.Exp(-_visualResponsiveness * deltaTime);

            // 轉彎時壓車；騰空時慢慢回正
            float targetLean = _isGrounded ? ScooterPhysics.ComputeLeanAngle(_steer, _speed, _settings) : 0f;
            _lean = Mathf.Lerp(_lean, targetLean, blend);

            // 車頭跟著坡度或飛行方向抬起、壓下
            float targetPitch = 0f;
            Vector3 velocity = _body.linearVelocity;
            if (_isGrounded)
            {
                targetPitch = -Mathf.Asin(Mathf.Clamp(_moveDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
            else if (velocity.sqrMagnitude > 1f)
            {
                targetPitch = -Mathf.Asin(Mathf.Clamp(velocity.normalized.y, -1f, 1f)) * Mathf.Rad2Deg * 0.6f;
            }

            _pitch = Mathf.Lerp(_pitch, targetPitch, blend);
            // 被撞後車身左右晃幾下再停住（衰減的正弦波）
            float sinceImpact = Time.time - _impactTime;
            float wobble = 0f;
            if (sinceImpact < IMPACT_WOBBLE_DURATION)
                wobble = _impactStrength * IMPACT_WOBBLE_DEGREES * Mathf.Sin(sinceImpact * 30f) * Mathf.Exp(-sinceImpact * 6f);

            _visual.localRotation = Quaternion.Euler(_pitch, 0f, _lean + wobble);

            _wheelSpin = Mathf.Repeat(_wheelSpin + _speed / _wheelRadius * Mathf.Rad2Deg * deltaTime, 360f);
            Quaternion steering = Quaternion.Euler(0f, _steer * _maxHandlebarAngle, 0f);
            Quaternion spin = Quaternion.Euler(_wheelSpin, 0f, 0f);
            _handlebar.localRotation = steering;
            _frontWheel.localRotation = steering * spin;
            _rearWheel.localRotation = spin;
        }

        private void Respawn()
        {
            if (RespawnOverride != null)
            {
                (Vector3 position, Quaternion rotation) = RespawnOverride();
                TeleportTo(position + Vector3.up * 0.5f, rotation);

                return;
            }

            TeleportTo(_spawnPosition + Vector3.up * 0.5f, _spawnRotation);
        }
    }
}
