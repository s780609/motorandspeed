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

        // 撞擊速度（m/s）低於這個值不算撞擊，例如輕輕擦到
        public float ImpactThreshold = 1.5f;
        // 每多 1 m/s 撞擊速度，車頭偏多少度
        public float ImpactYawPerSpeed = 1.8f;
        // 車頭最多偏幾度
        public float MaxImpactYaw = 20f;
        // 撞擊速度超過門檻多少時，鏡頭晃動達到最大
        public float ImpactShakeRange = 10f;

        // 撞擊（例如斜斜擦上路緣石）造成的上升速度上限（m/s），斜坡本身的上升速度不受限
        public float MaxBumpRiseSpeed = 1.2f;
    }
}
