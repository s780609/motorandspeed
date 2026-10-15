namespace MotorAndSpeed.Game
{
    public enum GameMode
    {
        FreeRide,
        DeliveryChain,
        DeliverySingle,
        DeliveryPickup,
        RaceSprint,
        RaceCircuit,
        TestTrack,
    }

    /// <summary>
    /// 遊戲模式的顯示名稱與選單順序。
    /// </summary>
    public static class GameModes
    {
        /// <summary>選單上依序列出的模式。</summary>
        public static readonly GameMode[] All =
        {
            GameMode.FreeRide,
            GameMode.DeliveryChain,
            GameMode.DeliverySingle,
            GameMode.DeliveryPickup,
            GameMode.RaceSprint,
            GameMode.RaceCircuit,
            GameMode.TestTrack,
        };

        /// <summary>
        /// 模式在選單上顯示的中文名稱。
        /// </summary>
        public static string DisplayName(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.DeliveryChain:
                    return "時限送貨：連續送";
                case GameMode.DeliverySingle:
                    return "時限送貨：單趟";
                case GameMode.DeliveryPickup:
                    return "時限送貨：取貨再送";
                case GameMode.RaceSprint:
                    return "AI 比賽：成泰路衝刺";
                case GameMode.RaceCircuit:
                    return "AI 比賽：市區繞圈 3 圈";
                case GameMode.TestTrack:
                    return "測試賽道";
                default:
                    return "自由騎乘";
            }
        }

        /// <summary>
        /// 是否在五股世界場景中進行。
        /// </summary>
        public static bool IsWorldMode(GameMode mode)
        {
            return mode != GameMode.TestTrack;
        }
    }
}
