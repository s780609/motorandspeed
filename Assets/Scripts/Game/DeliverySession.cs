using System;

namespace MotorAndSpeed.Game
{
    public enum DeliveryKind
    {
        // 連續送貨：每送到一單加時間，時間用完結算
        Chain,
        // 單趟送貨：時限內送到一個地點
        Single,
        // 先到店家取貨，再送到客人那裡
        PickupDropoff,
    }

    public enum DeliveryStage
    {
        Pickup,
        Dropoff,
    }

    /// <summary>
    /// 送貨模式的計時與進度（純邏輯，方便單元測試）。
    /// </summary>
    public class DeliverySession
    {
        private readonly float _bonusPerDelivery;

        public DeliveryKind Kind { get; }
        public DeliveryStage Stage { get; private set; }
        public float TimeRemaining { get; private set; }
        public int Delivered { get; private set; }
        public bool IsOver { get; private set; }
        public bool Succeeded { get; private set; }

        public DeliverySession(DeliveryKind kind, float timeLimit = 300f, float bonusPerDelivery = 45f)
        {
            Kind = kind;
            TimeRemaining = timeLimit;
            _bonusPerDelivery = bonusPerDelivery;
            Stage = kind == DeliveryKind.PickupDropoff ? DeliveryStage.Pickup : DeliveryStage.Dropoff;
        }

        /// <summary>
        /// 經過 deltaTime 秒；時間用完時結束。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsOver)
                return;

            TimeRemaining = Math.Max(0f, TimeRemaining - deltaTime);
            if (TimeRemaining > 0f)
                return;

            // 連續送貨只要送到過一單就算有成績；其他模式時間到就是失敗
            IsOver = true;
            Succeeded = Kind == DeliveryKind.Chain && Delivered > 0;
        }

        /// <summary>
        /// 抵達目前的目標點（取貨點或送貨點）。
        /// </summary>
        public void ReachTarget()
        {
            if (IsOver)
                return;

            switch (Kind)
            {
                case DeliveryKind.Chain:
                    Delivered++;
                    TimeRemaining += _bonusPerDelivery;
                    break;
                case DeliveryKind.PickupDropoff when Stage == DeliveryStage.Pickup:
                    Stage = DeliveryStage.Dropoff;
                    break;
                default:
                    Delivered = 1;
                    IsOver = true;
                    Succeeded = true;
                    break;
            }
        }
    }
}
