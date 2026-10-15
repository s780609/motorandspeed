using System;
using UnityEngine;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// 換色設定：把名稱為 MaterialName 的材質隨機換成 Options 其中之一。
    /// </summary>
    [Serializable]
    public class PaintSlot
    {
        public string MaterialName;
        public Material[] Options;
    }
}
