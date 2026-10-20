using UnityEngine;

namespace MotorAndSpeed.Audio
{
    /// <summary>
    /// 介面與比賽提示音。
    /// </summary>
    public enum GameSound
    {
        Click,
        CountdownTick,
        CountdownGo,
        Checkpoint,
        Finish,
    }

    /// <summary>
    /// 場景的 2D 聲音：街道環境底噪（循環）與介面、比賽提示音。由場景建置工具放進場景。
    /// </summary>
    public class GameSounds : MonoBehaviour
    {
        [SerializeField] private AudioClip _ambience;
        [SerializeField] private float _ambienceVolume = 0.35f;
        [SerializeField] private AudioClip _click;
        [SerializeField] private AudioClip _countdownTick;
        [SerializeField] private AudioClip _countdownGo;
        [SerializeField] private AudioClip _checkpoint;
        [SerializeField] private AudioClip _finish;
        [SerializeField] private float _effectsVolume = 0.8f;

        private AudioSource _ambienceSource;
        private AudioSource _effects;

        /// <summary>目前場景的聲音（場景裡沒有時為 null）。</summary>
        public static GameSounds Instance { get; private set; }

        /// <summary>環境底噪音源。</summary>
        public AudioSource Ambience => _ambienceSource;

        /// <summary>提示音音源。</summary>
        public AudioSource Effects => _effects;

        /// <summary>最後播放的提示音（沒播過時為 null）。</summary>
        public GameSound? LastPlayed { get; private set; }

        /// <summary>
        /// 播放提示音；場景裡沒有 GameSounds 時不做任何事。
        /// </summary>
        public static void Play(GameSound sound)
        {
            if (Instance != null)
                Instance.PlayNow(sound);
        }

        private void Awake()
        {
            Instance = this;
            _ambienceSource = CreateSource();
            _ambienceSource.clip = _ambience;
            _ambienceSource.loop = true;
            _ambienceSource.volume = _ambienceVolume;
            _effects = CreateSource();
            if (_ambience != null)
                _ambienceSource.Play();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void PlayNow(GameSound sound)
        {
            AudioClip clip = sound switch
            {
                GameSound.Click => _click,
                GameSound.CountdownTick => _countdownTick,
                GameSound.CountdownGo => _countdownGo,
                GameSound.Checkpoint => _checkpoint,
                _ => _finish,
            };
            LastPlayed = sound;
            if (clip != null)
                _effects.PlayOneShot(clip, _effectsVolume);
        }

        private AudioSource CreateSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            return source;
        }
    }
}
