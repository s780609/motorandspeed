using MotorAndSpeed.World;
using UnityEngine;

namespace MotorAndSpeed.Game
{
    /// <summary>
    /// AI 選手的速度規劃（純邏輯，方便單元測試）。
    /// </summary>
    public static class RacingLine
    {
        // 往前取樣的間隔（公尺）
        private const float SAMPLE_STEP = 5f;
        // AI 煞車減速度（m/s²），用來算出彎道前要提早多久減速
        private const float BRAKE_DECELERATION = 8f;
        // 差距多少公尺以內不調整速度
        private const float CATCH_UP_DEAD_ZONE = 20f;
        // 差距多少公尺時調整到最大
        private const float CATCH_UP_RANGE = 250f;
        private const float CATCH_UP_STRENGTH = 0.15f;

        /// <summary>
        /// 依前方彎道算出安全速度：越彎越慢，直線用最高速。
        /// </summary>
        /// <param name="track">賽道路徑</param>
        /// <param name="distance">目前在賽道上的距離</param>
        /// <param name="maxSpeed">最高速（m/s）</param>
        /// <param name="lookahead">往前看多遠（公尺）</param>
        /// <param name="lateralGrip">過彎能承受的橫向加速度（m/s²）</param>
        public static float TargetSpeed(RoutePath track, float distance, float maxSpeed, float lookahead, float lateralGrip)
        {
            Vector3 current = track.GetTangent(distance);
            float target = maxSpeed;
            for (float ahead = SAMPLE_STEP; ahead <= lookahead; ahead += SAMPLE_STEP)
            {
                float angle = Vector3.Angle(current, track.GetTangent(distance + ahead)) * Mathf.Deg2Rad;
                if (angle < 0.01f)
                    continue;

                // 走了 ahead 公尺轉了 angle 弧度，相當於半徑 ahead / angle 的彎；
                // 過彎速度 √(橫向加速度 × 半徑)，再加上這段距離內可以煞下來的速度
                float cornerSpeed = Mathf.Sqrt(lateralGrip * ahead / angle);
                float allowed = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * BRAKE_DECELERATION * ahead);
                target = Mathf.Min(target, allowed);
            }

            return target;
        }

        /// <summary>
        /// 拉近差距的速度倍率：落後玩家時加速、領先太多時放慢，讓比賽保持緊張。
        /// </summary>
        /// <param name="aiProgress">AI 的總進度（公尺）</param>
        /// <param name="playerProgress">玩家的總進度（公尺）</param>
        public static float CatchUpFactor(float aiProgress, float playerProgress)
        {
            float gap = playerProgress - aiProgress;
            float strength = Mathf.Clamp01((Mathf.Abs(gap) - CATCH_UP_DEAD_ZONE) / (CATCH_UP_RANGE - CATCH_UP_DEAD_ZONE));

            return 1f + Mathf.Sign(gap) * strength * CATCH_UP_STRENGTH;
        }
    }
}
