using System;
using UnityEngine;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// 一組燈頭：紅黃綠三顆燈加上台灣常見的倒數秒數（兩位數七段顯示器）。
    /// </summary>
    [Serializable]
    public class SignalHead
    {
        public Renderer Red;
        public Renderer Yellow;
        public Renderer Green;
        // 前 7 段是十位數，後 7 段是個位數，順序 a~g
        public Renderer[] Segments;
    }

    /// <summary>
    /// 路口紅綠燈：依週期切換主線與橫向道路的燈號。
    /// </summary>
    public class TrafficSignal : MonoBehaviour
    {
        [SerializeField] private float _distanceAlongMain;
        // 停止線離路口中心的距離（沿著主線）
        [SerializeField] private float _stopLineOffset = 12f;
        [SerializeField] private float _timeOffset;
        [SerializeField] private float _mainGreen = 22f;
        [SerializeField] private float _crossGreen = 12f;
        [SerializeField] private float _yellow = 3f;
        [SerializeField] private float _allRed = 1.5f;
        [SerializeField] private SignalHead[] _mainHeads = new SignalHead[0];
        [SerializeField] private SignalHead[] _crossHeads = new SignalHead[0];
        [SerializeField] private Material _lampOff;
        [SerializeField] private Material _redOn;
        [SerializeField] private Material _yellowOn;
        [SerializeField] private Material _greenOn;
        // 世界路網用：路口編號、每條進路（路段）與它的燈號組別（0 = 主線組、1 = 橫向組）
        [SerializeField] private int _junctionId = -1;
        [SerializeField] private int[] _approachEdges = new int[0];
        [SerializeField] private int[] _approachPhases = new int[0];

        private TrafficSignalCycle _cycle;
        private SignalPhase _shown;
        private bool _hasShown;
        private int _shownMainSeconds = -1;
        private int _shownCrossSeconds = -1;

        /// <summary>路口在主線上的距離。</summary>
        public float DistanceAlongMain => _distanceAlongMain;

        /// <summary>停止線離路口中心的距離。</summary>
        public float StopLineOffset => _stopLineOffset;

        /// <summary>主線目前的燈號。</summary>
        public SignalState MainState => Cycle.Evaluate(Time.time + _timeOffset).Main;

        /// <summary>所在路口編號（世界路網用，沒有時為 -1）。</summary>
        public int JunctionId => _junctionId;

        private TrafficSignalCycle Cycle => _cycle ??= new TrafficSignalCycle(_mainGreen, _crossGreen, _yellow, _allRed);

        /// <summary>
        /// 從某條路段開進這個路口時要看的燈號；不是這個路口的進路回傳綠燈。
        /// </summary>
        public SignalState GetStateForEdge(int edgeId)
        {
            int index = Array.IndexOf(_approachEdges, edgeId);
            if (index < 0)
                return SignalState.Green;

            SignalPhase phase = Cycle.Evaluate(Time.time + _timeOffset);

            return _approachPhases[index] == 0 ? phase.Main : phase.Cross;
        }

        /// <summary>
        /// 由世界場景建置工具設定：路口、各進路的組別。
        /// </summary>
        public void ConfigureJunction(int junctionId, int[] approachEdges, int[] approachPhases)
        {
            _junctionId = junctionId;
            _approachEdges = approachEdges;
            _approachPhases = approachPhases;
        }

        /// <summary>
        /// 由場景建置工具設定燈頭與材質。
        /// </summary>
        public void Configure(float distanceAlongMain, float stopLineOffset, float timeOffset, SignalHead[] mainHeads, SignalHead[] crossHeads, Material lampOff, Material redOn, Material yellowOn, Material greenOn)
        {
            _distanceAlongMain = distanceAlongMain;
            _stopLineOffset = stopLineOffset;
            _timeOffset = timeOffset;
            _mainHeads = mainHeads;
            _crossHeads = crossHeads;
            _lampOff = lampOff;
            _redOn = redOn;
            _yellowOn = yellowOn;
            _greenOn = greenOn;
        }

        private void Update()
        {
            SignalPhase phase = Cycle.Evaluate(Time.time + _timeOffset);
            if (!_hasShown || phase.Main != _shown.Main || phase.Cross != _shown.Cross)
            {
                ApplyLamps(_mainHeads, phase.Main);
                ApplyLamps(_crossHeads, phase.Cross);
                _shown = phase;
                _hasShown = true;
            }

            _shownMainSeconds = ApplyCountdown(_mainHeads, phase.Main, phase.MainRemaining, _shownMainSeconds);
            _shownCrossSeconds = ApplyCountdown(_crossHeads, phase.Cross, phase.CrossRemaining, _shownCrossSeconds);
        }

        private int ApplyCountdown(SignalHead[] heads, SignalState state, float remaining, int shownSeconds)
        {
            // 黃燈不顯示秒數；秒數有變才更新
            int seconds = state == SignalState.Yellow ? 0 : Mathf.CeilToInt(remaining);
            if (seconds == shownSeconds)
                return shownSeconds;

            SevenSegment.Encode(seconds, out int tensMask, out int onesMask);
            Material lit = state == SignalState.Green ? _greenOn : _redOn;
            foreach (SignalHead head in heads)
            {
                if (head.Segments == null)
                    continue;

                for (int i = 0; i < head.Segments.Length; i++)
                {
                    int mask = i < SevenSegment.SEGMENT_COUNT ? tensMask : onesMask;
                    bool on = (mask & (1 << (i % SevenSegment.SEGMENT_COUNT))) != 0;
                    head.Segments[i].sharedMaterial = on ? lit : _lampOff;
                }
            }

            return seconds;
        }

        private void ApplyLamps(SignalHead[] heads, SignalState state)
        {
            foreach (SignalHead head in heads)
            {
                head.Red.sharedMaterial = state == SignalState.Red ? _redOn : _lampOff;
                head.Yellow.sharedMaterial = state == SignalState.Yellow ? _yellowOn : _lampOff;
                head.Green.sharedMaterial = state == SignalState.Green ? _greenOn : _lampOff;
            }
        }

    }
}
