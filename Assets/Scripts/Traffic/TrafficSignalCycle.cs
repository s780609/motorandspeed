namespace MotorAndSpeed.Traffic
{
    public enum SignalState
    {
        Green,
        Yellow,
        Red,
    }

    /// <summary>
    /// 某個時間點的號誌狀態與剩餘秒數（主線 / 橫向道路）。
    /// </summary>
    public struct SignalPhase
    {
        public SignalState Main;
        public SignalState Cross;
        public float MainRemaining;
        public float CrossRemaining;
    }

    /// <summary>
    /// 紅綠燈週期：主線綠 → 主線黃 → 全紅 → 橫向綠 → 橫向黃 → 全紅，然後重來。
    /// </summary>
    public class TrafficSignalCycle
    {
        private readonly float _mainGreen;
        private readonly float _crossGreen;
        private readonly float _yellow;
        private readonly float _allRed;

        public float CycleLength => _mainGreen + _crossGreen + 2f * (_yellow + _allRed);

        public TrafficSignalCycle(float mainGreen, float crossGreen, float yellow, float allRed)
        {
            _mainGreen = mainGreen;
            _crossGreen = crossGreen;
            _yellow = yellow;
            _allRed = allRed;
        }

        /// <summary>
        /// 計算 time 秒時的號誌狀態，超過一個週期會自動循環。
        /// </summary>
        public SignalPhase Evaluate(float time)
        {
            float cycle = CycleLength;
            float t = (time % cycle + cycle) % cycle;
            float mainYellowEnd = _mainGreen + _yellow;
            float crossGreenStart = mainYellowEnd + _allRed;
            float crossYellowStart = crossGreenStart + _crossGreen;
            float crossYellowEnd = crossYellowStart + _yellow;
            SignalPhase phase = new SignalPhase();

            if (t < _mainGreen)
            {
                phase.Main = SignalState.Green;
                phase.MainRemaining = _mainGreen - t;
            }
            else if (t < mainYellowEnd)
            {
                phase.Main = SignalState.Yellow;
                phase.MainRemaining = mainYellowEnd - t;
            }
            else
            {
                phase.Main = SignalState.Red;
                phase.MainRemaining = cycle - t;
            }

            if (t < crossGreenStart)
            {
                phase.Cross = SignalState.Red;
                phase.CrossRemaining = crossGreenStart - t;
            }
            else if (t < crossYellowStart)
            {
                phase.Cross = SignalState.Green;
                phase.CrossRemaining = crossYellowStart - t;
            }
            else if (t < crossYellowEnd)
            {
                phase.Cross = SignalState.Yellow;
                phase.CrossRemaining = crossYellowEnd - t;
            }
            else
            {
                // 橫向的紅燈一路持續到下一個週期的橫向綠燈
                phase.Cross = SignalState.Red;
                phase.CrossRemaining = cycle - t + crossGreenStart;
            }

            return phase;
        }
    }
}
