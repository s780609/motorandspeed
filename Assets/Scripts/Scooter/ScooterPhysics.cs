using UnityEngine;

namespace MotorAndSpeed.Scooter
{
    /// <summary>
    /// 街機式速克達物理計算（純數學，不依賴場景，方便單元測試）。
    /// </summary>
    public static class ScooterPhysics
    {
        /// <summary>
        /// 根據油門與煞車計算下一刻的前進速度。
        /// </summary>
        /// <param name="speed">目前速度（m/s），負數代表倒車</param>
        /// <param name="throttle">油門 0~1</param>
        /// <param name="brake">煞車 0~1，停住後持續按會倒車</param>
        /// <param name="deltaTime">經過時間（秒）</param>
        /// <param name="settings">操控參數</param>
        /// <returns>新的速度（m/s）</returns>
        public static float UpdateSpeed(float speed, float throttle, float brake, float deltaTime, ScooterSettings settings)
        {
            throttle = Mathf.Clamp01(throttle);
            brake = Mathf.Clamp01(brake);

            if (brake > 0f)
            {
                // 前進中煞車只會停到 0，下一刻起才開始倒車
                if (speed > 0f)
                {
                    return Mathf.Max(0f, speed - settings.BrakeDeceleration * brake * deltaTime);
                }

                return Mathf.Max(-settings.MaxReverseSpeed, speed - settings.ReverseAcceleration * brake * deltaTime);
            }

            if (throttle > 0f)
            {
                // 倒車中催油門，先煞停
                if (speed < 0f)
                {
                    return Mathf.Min(0f, speed + settings.BrakeDeceleration * throttle * deltaTime);
                }

                // 越接近極速，加速度越小
                float taper = 1f - 0.7f * Mathf.Clamp01(speed / settings.MaxSpeed);

                return Mathf.Min(settings.MaxSpeed, speed + settings.Acceleration * throttle * taper * deltaTime);
            }

            return Mathf.MoveTowards(speed, 0f, settings.CoastDeceleration * deltaTime);
        }

        /// <summary>
        /// 計算轉向速度。正數為向右轉（從上往下看順時針）。
        /// </summary>
        /// <returns>每秒轉動角度（度/秒）</returns>
        public static float ComputeYawRate(float steer, float speed, ScooterSettings settings)
        {
            float absSpeed = Mathf.Abs(speed);
            float lowSpeedFactor = Mathf.Clamp01(absSpeed / settings.TurnFullSpeed);
            float highSpeedFactor = Mathf.Lerp(1f, settings.HighSpeedTurnFactor, absSpeed / settings.MaxSpeed);

            return Mathf.Clamp(steer, -1f, 1f) * settings.TurnSpeed * lowSpeedFactor * highSpeedFactor * Mathf.Sign(speed);
        }

        /// <summary>
        /// 計算車身傾斜角度（繞前進軸）。往右轉時車身往右倒，角度為負數。
        /// </summary>
        /// <returns>傾斜角度（度）</returns>
        public static float ComputeLeanAngle(float steer, float speed, ScooterSettings settings)
        {
            float speedFactor = Mathf.Clamp01(Mathf.Abs(speed) / settings.LeanFullSpeed);

            return -Mathf.Clamp(steer, -1f, 1f) * settings.MaxLean * speedFactor;
        }
    }
}
