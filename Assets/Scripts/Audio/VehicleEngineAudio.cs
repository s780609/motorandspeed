using UnityEngine;

namespace MotorAndSpeed.Audio
{
    /// <summary>
    /// NPC 車輛與 AI 選手的 3D 引擎聲：從位置變化估計車速與油門，離越遠越小聲，超過最大距離完全聽不到。
    /// 不依賴車輛的移動程式，任何會動的車都可以掛。
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleEngineAudio : MonoBehaviour
    {
        // 車速估計的平滑程度
        private const float SPEED_SHARPNESS = 6f;
        private const float THROTTLE_SHARPNESS = 4f;
        // 一幀內移動超過這個速度（m/s）視為瞬間移動（被搬回玩家附近），不當成真的加速
        private const float TELEPORT_SPEED = 80f;
        private const float DOPPLER = 0.3f;

        [SerializeField] private AudioClip _clip;
        [SerializeField] private EngineSoundProfile _profile = new EngineSoundProfile();
        // 這個距離內音量不衰減（公尺）
        [SerializeField] private float _minDistance = 4f;
        // 超過這個距離完全聽不到（公尺）
        [SerializeField] private float _maxDistance = 80f;
        // 這個加速度以上算催滿油門（m/s²）
        [SerializeField] private float _fullThrottleAcceleration = 3f;

        private AudioSource _source;
        private Vector3 _lastPosition;
        private float _speed;
        private float _throttle;
        // 每台車音高略有不同，同款車不會整齊劃一
        private float _pitchVariation = 1f;

        /// <summary>引擎聲的音源。</summary>
        public AudioSource Source => _source;

        /// <summary>估計的車速（m/s）。</summary>
        public float EstimatedSpeed => _speed;

        /// <summary>超過這個距離完全聽不到（公尺）。</summary>
        public float MaxDistance => _maxDistance;

        /// <summary>
        /// 由場景建置工具設定音效與參數。
        /// </summary>
        public void Configure(AudioClip clip, EngineSoundProfile profile, float minDistance, float maxDistance, float fullThrottleAcceleration)
        {
            _clip = clip;
            _profile = profile;
            _minDistance = minDistance;
            _maxDistance = maxDistance;
            _fullThrottleAcceleration = fullThrottleAcceleration;
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = _clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.dopplerLevel = DOPPLER;
            _source.minDistance = _minDistance;
            _source.maxDistance = _maxDistance;
            // 自訂衰減曲線：距離反比衰減，接近最大距離時淡到 0，遠處的車不佔用聲道
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, BuildRolloff(_minDistance / _maxDistance));
            _pitchVariation = Random.Range(0.94f, 1.06f);
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _speed = 0f;
            _throttle = 0f;
            if (_source == null || _clip == null)
                return;

            ApplySound();
            _source.Play();
            // 從隨機位置開始播，附近幾台車的引擎聲不會疊在同一個相位
            _source.timeSamples = Random.Range(0, _clip.samples);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Vector3 position = transform.position;
            float measured = Vector3.Distance(position, _lastPosition) / deltaTime;
            _lastPosition = position;
            if (measured > TELEPORT_SPEED)
                measured = _speed;

            float previous = _speed;
            _speed = EngineAudio.Smooth(_speed, measured, SPEED_SHARPNESS, deltaTime);
            float acceleration = (_speed - previous) / deltaTime;
            _throttle = EngineAudio.Smooth(_throttle, EngineAudio.EstimateThrottle(acceleration, _fullThrottleAcceleration), THROTTLE_SHARPNESS, deltaTime);
            ApplySound();
        }

        private void ApplySound()
        {
            _source.pitch = EngineAudio.ComputePitch(_speed, _throttle, _profile) * _pitchVariation;
            _source.volume = EngineAudio.ComputeVolume(_speed, _throttle, _profile);
        }

        private static AnimationCurve BuildRolloff(float minRatio)
        {
            // 橫軸是 距離 / 最大距離
            Keyframe[] keys = new Keyframe[10];
            keys[0] = new Keyframe(0f, 1f);
            for (int i = 1; i < keys.Length; i++)
            {
                float x = Mathf.Lerp(minRatio, 1f, (i - 1f) / (keys.Length - 2f));
                float value = Mathf.Min(1f, minRatio / x) * (1f - Mathf.Pow(x, 4f));
                keys[i] = new Keyframe(x, value);
            }

            return new AnimationCurve(keys);
        }
    }
}
