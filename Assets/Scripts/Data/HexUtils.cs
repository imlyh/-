using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 六边形坐标与世界坐标转换（尖顶朝上 Pointy-Top，Axial 坐标系）
    ///
    /// 坐标公式 (Axial q,r → 世界 x,z，R = 0.5)：
    ///   x = R * (3/2 * q)
    ///   z = R * (√3/2 * q + √3 * r)
    ///
    /// 相邻六边形中心距 = 2R * √3/2 ≈ 0.866
    /// 六边形外接圆半径 R = 0.5，边长 = R，宽(平边) = √3*R ≈ 0.866，高(尖顶) = 2R = 1.0
    /// </summary>
    public static class HexUtils
    {
        private const float R = 0.5f;
        private const float Sqrt3 = 1.7320508f;
        private const float Sqrt3Over2 = 0.8660254f;

        /// <summary>
        /// Axial 坐标 (q, r) → 世界坐标 (x, z)，Y 始终为 0
        /// </summary>
        public static float3 ToWorldPosition(HexCoordinates hex)
        {
            float x = R * (1.5f * hex.q);
            float z = R * (Sqrt3Over2 * hex.q + Sqrt3 * hex.r);
            return new float3(x, 0f, z);
        }

        /// <summary>
        /// 获取向目标移动的下一个六边形坐标（选择最接近目标的邻居方向）
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
        /// 计算两个六边形中心之间的世界空间距离
        /// </summary>
        public static float WorldDistance(HexCoordinates a, HexCoordinates b)
        {
            return math.distance(ToWorldPosition(a), ToWorldPosition(b));
        }
    }
}
