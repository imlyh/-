using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 六边形坐标与世界坐标转换（尖顶朝上 Pointy-Top，Offset 偏移坐标系）
    ///
    /// 坐标转换公式：
    ///   Axial(q, r) → Offset(col, row) → World(x, z)
    ///     col = q
    ///     row = r + (q - (q & 1)) / 2
    ///     x   = col * 1.299
    ///     z   = row * 2.0 + (col % 2) * 1.0
    ///
    /// 关键参数（六边形外接圆半径 = 1.0）：
    ///   水平间距 1.299  |  垂直间距 2.0  |  奇数列 Z 偏移 1.0
    /// </summary>
    public static class HexUtils
    {
        private const float HorizontalSpacing = 1.2990381f;  // √3 * 0.75
        private const float VerticalSpacing = 2f;
        private const float OddRowOffset = 1f;

        /// <summary>
        /// 轴向坐标 (q, r) → 世界坐标 (x, z)，Y 始终为 0
        /// </summary>
        public static float3 ToWorldPosition(HexCoordinates hex)
        {
            int col = hex.q;
            int row = hex.r + (hex.q - (hex.q & 1)) / 2;
            float x = col * HorizontalSpacing;
            float z = row * VerticalSpacing + (col & 1) * OddRowOffset;
            return new float3(x, 0f, z);
        }

        /// <summary>
        /// 获取向目标移动的下一个六边形坐标
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
        /// 两个六边形中心之间的世界空间距离
        /// </summary>
        public static float WorldDistance(HexCoordinates a, HexCoordinates b)
        {
            return math.distance(ToWorldPosition(a), ToWorldPosition(b));
        }
    }
}
