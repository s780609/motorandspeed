using System;

namespace MotorAndSpeed.Scooter
{
    /// <summary>
    /// 速克達操控手感參數（單位：公尺、秒、度）。
    /// </summary>
    [Serializable]
    public class ScooterSettings
    {
        // 極速（m/s），28 m/s 約 100 km/h
        public float MaxSpeed = 28f;
        // 油門加速度（m/s²），越接近極速加速越慢
        public float Acceleration = 9f;
        public float BrakeDeceleration = 20f;
        // 放開油門時的自然減速
        public float CoastDeceleration = 4f;
        public float MaxReverseSpeed = 5f;
        public float ReverseAcceleration = 6f;

        // 最大轉向速度（度/秒）
        public float TurnSpeed = 110f;
        // 低於這個速度時轉向會變遲鈍，停住時不能原地轉
        public float TurnFullSpeed = 6f;
        // 極速時轉向剩下的比例，避免高速過度靈敏
        public float HighSpeedTurnFactor = 0.55f;

        // 最大車身傾斜角度（度）
        public float MaxLean = 35f;
        // 達到這個速度時才會壓到最大傾斜
        public float LeanFullSpeed = 10f;
    }
}
