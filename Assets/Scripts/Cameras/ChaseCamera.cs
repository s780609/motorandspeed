using MotorAndSpeed.Scooter;
using UnityEngine;

namespace MotorAndSpeed.Cameras
{
    /// <summary>
    /// 跟在速克達後方的追蹤鏡頭，速度越快視野越廣，增加速度感。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private ScooterController _target;
        [SerializeField] private float _distance = 4.5f;
        [SerializeField] private float _height = 1.8f;
        [SerializeField] private float _lookHeight = 0.9f;
        [SerializeField] private float _followSharpness = 10f;
        [SerializeField] private float _baseFieldOfView = 60f;
        [SerializeField] private float _maxFieldOfView = 78f;
        // 被撞時鏡頭晃動的最大幅度（公尺）與持續時間（秒）
        [SerializeField] private float _shakeAmplitude = 0.25f;
        [SerializeField] private float _shakeDuration = 0.45f;

        private Camera _camera;
        private float _shakeStrength;
        private float _shakeStart = -10f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (_target != null)
                _target.Impacted += OnImpacted;
        }

        private void OnDisable()
        {
            if (_target != null)
                _target.Impacted -= OnImpacted;
        }

        private void OnImpacted(float strength)
        {
            _shakeStrength = strength;
            _shakeStart = Time.time;
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;

            Transform target = _target.transform;
            float blend = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);

            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            Vector3 desiredPosition = target.position - flatForward * _distance + Vector3.up * _height;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.LookAt(target.position + Vector3.up * _lookHeight);

            // 撞擊晃動：用雜訊產生不規則的抖動，隨時間淡出
            float sinceShake = Time.time - _shakeStart;
            if (sinceShake < _shakeDuration)
            {
                float fade = 1f - sinceShake / _shakeDuration;
                float amplitude = _shakeAmplitude * _shakeStrength * fade * fade;
                float x = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * 2f;
                transform.position += (transform.right * x + transform.up * y) * amplitude;
            }

            float speedRatio = Mathf.Clamp01(Mathf.Abs(_target.Speed) / _target.MaxSpeed);
            float targetFieldOfView = Mathf.Lerp(_baseFieldOfView, _maxFieldOfView, speedRatio);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFieldOfView, blend);
        }
    }
}
