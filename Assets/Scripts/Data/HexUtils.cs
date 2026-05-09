using Unity.Mathematics;

/// ============================================================================
/// GridUtils — 方形网格坐标 → 世界坐标转换
/// ============================================================================
///
/// 【设计思路】
///   1x1 方格紧密排列（cellSize = 1.0），格子中心世界坐标为 (q, 0, r)。
///   最简单直接的映射：列 → X，行 → Z。
/// ============================================================================
namespace ConquestGame
{
    public static class HexUtils
    {
        private const float CellSize = 1f;

        /// <summary>
        /// 网格坐标 (q, r) → 世界坐标 (x, z)，Y = 0
        /// </summary>
        public static float3 ToWorldPosition(HexCoordinates coord)
        {
            return new float3(coord.q * CellSize, 0f, coord.r * CellSize);
        }

        /// <summary>
        /// 获取向目标移动的下一个格子坐标（四方向中选曼哈顿距离最近的）
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
        /// 计算两个格子中心之间的世界空间距离
        /// </summary>
        public static float WorldDistance(HexCoordinates a, HexCoordinates b)
        {
            return math.distance(ToWorldPosition(a), ToWorldPosition(b));
        }
    }
}
