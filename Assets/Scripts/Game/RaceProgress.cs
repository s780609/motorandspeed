using System;

namespace MotorAndSpeed.Game
{
    /// <summary>
    /// 比賽進度與名次：衝刺賽看跑了多遠，繞圈賽另外計算圈數（純邏輯，方便單元測試）。
    /// </summary>
    public class RaceProgress
    {
        // 衝刺賽離終點多近算抵達（公尺）
        private const float FINISH_MARGIN = 5f;

        private readonly float _trackLength;
        private readonly int _laps;
        private readonly bool _closedLoop;
        private readonly float[] _distance;
        private readonly int[] _completedLaps;
        private readonly bool[] _hasPosition;
        private readonly int[] _finishOrder;
        private int _finishedCount;

        public RaceProgress(int racerCount, float trackLength, int laps, bool closedLoop)
        {
            _trackLength = trackLength;
            _laps = laps;
            _closedLoop = closedLoop;
            _distance = new float[racerCount];
            _completedLaps = new int[racerCount];
            _hasPosition = new bool[racerCount];
            _finishOrder = new int[racerCount];
            for (int i = 0; i < racerCount; i++)
            {
                _finishOrder[i] = -1;
            }
        }

        /// <summary>
        /// 更新某位選手在賽道上的位置（離起點的距離）。
        /// </summary>
        public void Update(int racer, float distanceAlongTrack)
        {
            if (IsFinished(racer))
                return;

            // 繞圈賽：從賽道尾端跳回開頭表示過了起跑線；反過來表示倒退回去，要扣回一圈
            if (_closedLoop && _hasPosition[racer])
            {
                float previous = _distance[racer];
                if (previous > _trackLength * 0.75f && distanceAlongTrack < _trackLength * 0.25f)
                {
                    _completedLaps[racer]++;
                }
                else if (previous < _trackLength * 0.25f && distanceAlongTrack > _trackLength * 0.75f)
                {
                    _completedLaps[racer]--;
                }
            }

            _distance[racer] = distanceAlongTrack;
            _hasPosition[racer] = true;

            bool finished = _closedLoop ? _completedLaps[racer] >= _laps : distanceAlongTrack >= _trackLength - FINISH_MARGIN;
            if (finished)
                _finishOrder[racer] = _finishedCount++;
        }

        /// <summary>總進度（圈數 × 賽道長 + 目前距離）。</summary>
        public float GetProgress(int racer)
        {
            return _completedLaps[racer] * _trackLength + _distance[racer];
        }

        /// <summary>目前第幾圈（從 1 開始，完賽後停在最後一圈）。</summary>
        public int GetLap(int racer)
        {
            return Math.Max(1, Math.Min(_completedLaps[racer] + 1, _laps));
        }

        /// <summary>是否已完賽。</summary>
        public bool IsFinished(int racer)
        {
            return _finishOrder[racer] >= 0;
        }

        /// <summary>目前名次（從 1 開始）：先完賽的在前，其餘依進度排。</summary>
        public int GetPosition(int racer)
        {
            if (IsFinished(racer))
                return _finishOrder[racer] + 1;

            int ahead = _finishedCount;
            for (int other = 0; other < _distance.Length; other++)
            {
                if (other != racer && !IsFinished(other) && GetProgress(other) > GetProgress(racer))
                    ahead++;
            }

            return ahead + 1;
        }
    }
}
