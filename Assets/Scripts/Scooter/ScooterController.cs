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

        /// <summary>目前速度（m/s），負數代表倒車。</summary>
        public float Speed => _speed;

        /// <summary>是否著地。</summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>極速（m/s）。</summary>
        public float MaxSpeed => _settings.MaxSpeed;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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
            Vector3 probeOrigin = transform.position + Vector3.up * PROBE_HEIGHT;
            _isGrounded = GroundProbe.TryGetGround(probeOrigin, PROBE_HEIGHT + GROUNDED_TOLERANCE, out RaycastHit hit);

            // 騰空時不能加速或轉向，交給重力
            if (!_isGrounded)
                return;

            SyncSpeedWithCollisions();

            float deltaTime = Time.fixedDeltaTime;
            _speed = ScooterPhysics.UpdateSpeed(_speed, _throttle, _brake, deltaTime, _settings);
            float yaw = ScooterPhysics.ComputeYawRate(_steer, _speed, _settings) * deltaTime;
            _body.MoveRotation(_body.rotation * Quaternion.Euler(0f, yaw, 0f));

            // 沿著地面坡度前進，衝上斜坡就會自然飛出去
            _moveDirection = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            _body.linearVelocity = _moveDirection * _speed;
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
            _visual.localRotation = Quaternion.Euler(_pitch, 0f, _lean);

            _wheelSpin = Mathf.Repeat(_wheelSpin + _speed / _wheelRadius * Mathf.Rad2Deg * deltaTime, 360f);
            Quaternion steering = Quaternion.Euler(0f, _steer * _maxHandlebarAngle, 0f);
            Quaternion spin = Quaternion.Euler(_wheelSpin, 0f, 0f);
            _handlebar.localRotation = steering;
            _frontWheel.localRotation = steering * spin;
            _rearWheel.localRotation = spin;
        }

        private void Respawn()
        {
            _speed = 0f;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.position = _spawnPosition + Vector3.up * 0.5f;
            _body.rotation = _spawnRotation;
            _moveDirection = _spawnRotation * Vector3.forward;
        }
    }
}
