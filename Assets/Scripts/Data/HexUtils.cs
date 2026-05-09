using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 六边形坐标与世界坐标之间的转换工具（尖顶六边形，XZ平面）
    /// </summary>
    public static class HexUtils
    {
        private const float HexSize = 1f;
        private const float Sqrt3 = 1.7320508f;

        /// <summary>
        /// 轴向坐标 (q, r) → 世界坐标 (x, z)
        /// </summary>
        public static float3 ToWorldPosition(HexCoordinates hex)
        {
            float x = HexSize * (Sqrt3 * hex.q + Sqrt3 * 0.5f * hex.r);
            float z = HexSize * (1.5f * hex.r);
            return new float3(x, 0f, z);
        }

        /// <summary>
        /// 获取移动方向的下一个六边形坐标（选择最接近目标的方向）
        /// </summary>
        public static HexCoordinates NextHexToward(HexCoordinates current, HexCoordinates target)
        {
            if (current == target)
                return current;

            int bestDist = int.MaxValue;
            HexCoordinates best = current;

            foreach (var dir in HexCoordinates.Directions)
            {
                var neighbor = current + dir;
                int dist = neighbor.DistanceTo(target);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = neighbor;
                }
            }

            return best;
        }

        /// <summary>
        /// 两个六边形网格中心之间的世界空间距离
        /// </summary>
        public static float WorldDistance(HexCoordinates a, HexCoordinates b)
        {
            return math.distance(ToWorldPosition(a), ToWorldPosition(b));
        }
    }
}
