using System;

namespace MotorAndSpeed.Traffic
{
    /// <summary>
    /// 七段顯示器編碼（紅綠燈倒數秒數）。
    /// 位元 0~6 依序為 a 上、b 右上、c 右下、d 下、e 左下、f 左上、g 中間。
    /// </summary>
    public static class SevenSegment
    {
        public const int SEGMENT_COUNT = 7;

        // 0~9 各自要亮的段
        private static readonly int[] Digits =
        {
            0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110,
            0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111,
        };

        /// <summary>
        /// 把秒數轉成十位數與個位數的亮燈位元；0 以下全暗，超過 99 顯示 99，十位數為 0 時不亮。
        /// </summary>
        public static void Encode(int value, out int tensMask, out int onesMask)
        {
            tensMask = 0;
            onesMask = 0;
            if (value <= 0)
                return;

            value = Math.Min(value, 99);
            onesMask = Digits[value % 10];
            if (value >= 10)
                tensMask = Digits[value / 10];
        }
    }
}
