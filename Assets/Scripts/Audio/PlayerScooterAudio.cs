using MotorAndSpeed.Scooter;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotorAndSpeed.Audio
{
    /// <summary>
    /// 玩家機車的聲音：引擎聲（音高、音量跟著車速與油門）、喇叭（按住 H 或手把 LB）、碰撞聲。
    /// </summary>
    [RequireComponent(typeof(ScooterController))]
    public class PlayerScooterAudio : MonoBehaviour
    {
        // 音高、音量跟上目標的速度
        private const float ENGINE_SHARPNESS = 8f;
        // 喇叭淡入淡出的速度（每秒音量），放開時不會喀一聲
        private const float HORN_FADE = 12f;

        [SerializeField] private AudioClip _engineClip;
        [SerializeField] private AudioClip _hornClip;
        [SerializeField] private AudioClip _crashClip;
        [SerializeField] private EngineSoundProfile _profile = new EngineSoundProfile();
        [SerializeField] private float _hornVolume = 0.7f;
        [SerializeField] private float _crashVolume = 1f;

        private ScooterController _scooter;
        private AudioSource _engine;
        private AudioSource _horn;
        private AudioSource _effects;
        private float _pitch;
        private float _volume;

        /// <summary>引擎聲音源。</summary>
        public AudioSource Engine => _engine;

        /// <summary>喇叭音源。</summary>
        public AudioSource Horn => _horn;

        /// <summary>碰撞等單次音效的音源。</summary>
        public AudioSource Effects => _effects;

        private void Awake()
        {
            _scooter = GetComponent<ScooterController>();
            _engine = CreateSource(_engineClip, true);
            _horn = CreateSource(_hornClip, true);
            _effects = CreateSource(null, false);
            _pitch = _profile.IdlePitch;
            _volume = _profile.IdleVolume;
        }

        private void OnEnable()
        {
            _scooter.Impacted += OnImpacted;
            if (_engineClip != null)
                _engine.Play();
        }

        private void OnDisable()
        {
            _scooter.Impacted -= OnImpacted;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            float speed = _scooter.Speed;
            float throttle = _scooter.Throttle;
            _pitch = EngineAudio.Smooth(_pitch, EngineAudio.ComputePitch(speed, throttle, _profile), ENGINE_SHARPNESS, deltaTime);
            _volume = EngineAudio.Smooth(_volume, EngineAudio.ComputeVolume(speed, throttle, _profile), ENGINE_SHARPNESS, deltaTime);
            _engine.pitch = _pitch;
            _engine.volume = _volume;

            UpdateHorn(deltaTime);
        }

        private void UpdateHorn(float deltaTime)
        {
            bool held = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.isPressed)
                held = true;

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.leftShoulder.isPressed)
                held = true;

            if (held && !_horn.isPlaying && _hornClip != null)
            {
                _horn.volume = 0f;
                _horn.Play();
            }

            _horn.volume = Mathf.MoveTowards(_horn.volume, held ? _hornVolume : 0f, HORN_FADE * deltaTime);
            if (!held && _horn.isPlaying && _horn.volume <= 0f)
                _horn.Stop();
        }

        private void OnImpacted(float strength)
        {
            if (_crashClip == null)
                return;

            // 輕擦小聲、正面撞大聲；音高稍微隨機，連續撞擊聽起來不會一模一樣
            _effects.pitch = Random.Range(0.9f, 1.1f);
            _effects.PlayOneShot(_crashClip, _crashVolume * Mathf.Lerp(0.35f, 1f, strength));
        }

        private AudioSource CreateSource(AudioClip clip, bool loop)
        {
            // 玩家自己的聲音不做 3D 定位（鏡頭就在車後面）
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            return source;
        }
    }
}
