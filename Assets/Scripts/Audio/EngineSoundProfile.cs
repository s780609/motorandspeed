using System;

namespace MotorAndSpeed.Audio
{
    /// <summary>
    /// 引擎聲的音高與音量參數。音高 1 等於音效樣本錄製時的轉速。
    /// </summary>
    [Serializable]
    public class EngineSoundProfile
    {
        // 停住、沒催油門時的音高（怠速）
        public float IdlePitch = 0.6f;
        // 到達 TopSpeed 時的音高
        public float MaxPitch = 1.7f;
        // 催滿油門額外加的音高（轉速被拉高）
        public float ThrottlePitch = 0.2f;
        public float IdleVolume = 0.3f;
        public float MaxVolume = 0.9f;
        // 音高拉到最高的速度（m/s）
        public float TopSpeed = 28f;
    }
}
