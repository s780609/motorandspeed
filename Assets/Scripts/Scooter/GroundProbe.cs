using UnityEngine;

namespace MotorAndSpeed.Scooter
{
    /// <summary>
    /// 往下發射射線，判斷車子是否在地面上。
    /// </summary>
    public static class GroundProbe
    {
        /// <summary>
        /// 從 origin 往正下方射出 maxDistance 長的射線，忽略觸發器。
        /// </summary>
        /// <param name="origin">射線起點</param>
        /// <param name="maxDistance">射線長度（公尺），超過這個距離就算騰空</param>
        /// <param name="hit">打到的地面資訊</param>
        /// <returns>打到地面回傳 true</returns>
        public static bool TryGetGround(Vector3 origin, float maxDistance, out RaycastHit hit)
        {
            return Physics.Raycast(origin, Vector3.down, out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
