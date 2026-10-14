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

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
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

            float speedRatio = Mathf.Clamp01(Mathf.Abs(_target.Speed) / _target.MaxSpeed);
            float targetFieldOfView = Mathf.Lerp(_baseFieldOfView, _maxFieldOfView, speedRatio);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFieldOfView, blend);
        }
    }
}
