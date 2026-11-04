namespace MotorAndSpeed.World
{
    /// <summary>
    /// 依位置產生固定的亂數種子：建築細節、樹種、街道設施改用這個，不會動到世界建置共用亂數的呼叫順序
    /// （共用亂數多呼叫一次，後面所有房子的配置都會改變）。
    /// </summary>
    public static class LotSeed
    {
        /// <summary>
        /// 位置量化到公分後混合成種子；salt 區分同一個位置的不同用途。
        /// </summary>
        public static int For(float x, float z, int salt)
        {
            unchecked
            {
                uint h = (uint)UnityEngine.Mathf.RoundToInt(x * 100f) * 0x9E3779B1u;
                h ^= (uint)UnityEngine.Mathf.RoundToInt(z * 100f) * 0x85EBCA77u;
                h ^= (uint)salt * 0xC2B2AE3Du;

                // murmur3 的收尾混合，讓相鄰位置的種子也差很多
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;

                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// 以位置種子建立亂數產生器。
        /// </summary>
        public static System.Random Random(float x, float z, int salt)
        {
            return new System.Random(For(x, z, salt));
        }
    }
}
