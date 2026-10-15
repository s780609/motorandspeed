using System;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// NPC 車輛的駕駛判斷（純數學，方便單元測試）。
    /// </summary>
    public static class TrafficRules
    {
        // 跟前車保持的最小距離（公尺）
        private const float MIN_GAP = 2.5f;
        // 停在停止線前的距離（公尺）
        private const float STOP_MARGIN = 1f;

        /// <summary>
        /// 計算 NPC 這一刻想要的速度：前方有車要減速，紅燈要停在停止線前，黃燈來得及停就停。
        /// </summary>
        /// <param name="cruiseSpeed">平常的巡航速度（m/s）</param>
        /// <param name="currentSpeed">目前速度（m/s）</param>
        /// <param name="deceleration">舒適的煞車減速度（m/s²）</param>
        /// <param name="distanceToStopLine">到前方停止線的距離，沒有號誌時傳 float.PositiveInfinity</param>
        /// <param name="signal">前方號誌狀態</param>
        /// <param name="distanceToObstacle">到前方車輛的距離，沒有車時傳 float.PositiveInfinity</param>
        /// <returns>目標速度（m/s）</returns>
        public static float ComputeTargetSpeed(float cruiseSpeed, float currentSpeed, float deceleration, float distanceToStopLine, SignalState signal, float distanceToObstacle)
        {
            float target = cruiseSpeed;
            target = Math.Min(target, BrakingSpeed(distanceToObstacle - MIN_GAP, deceleration));

            bool mustStop = signal == SignalState.Red;
            if (signal == SignalState.Yellow)
            {
                // 黃燈：煞得住就停，太近煞不住就直接通過
                float stoppingDistance = currentSpeed * currentSpeed / (2f * deceleration);
                mustStop = stoppingDistance <= distanceToStopLine;
            }

            if (mustStop && distanceToStopLine >= 0f)
                target = Math.Min(target, BrakingSpeed(distanceToStopLine - STOP_MARGIN, deceleration));

            return target;
        }

        // 用固定減速度在 distance 內剛好停下的最高速度：v = √(2ad)
        private static float BrakingSpeed(float distance, float deceleration)
        {
            if (float.IsPositiveInfinity(distance))
                return float.PositiveInfinity;

            return distance <= 0f ? 0f : (float)Math.Sqrt(2f * deceleration * distance);
        }
    }
}
