using Unity.Mathematics;
using System;

/// ============================================================================
/// GridCoordinates — 方形网格坐标，类名保留 HexCoordinates 兼容旧引用
/// ============================================================================
///
/// 【设计思路】
///   用整数 (q, r) 表示方形网格中的格子位置。
///   q = 列（Column），r = 行（Row），对应世界 X 和 Z 轴。
///   相邻格子通过 Directions 数组的 4 个方向（右/上/左/下）访问。
///   距离 = 曼哈顿距离 |Δq| + |Δr|
/// ============================================================================
namespace ConquestGame
{
    public struct HexCoordinates : IEquatable<HexCoordinates>
    {
        public int q; // 列 (Column / X)
        public int r; // 行 (Row / Z)

        public HexCoordinates(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        /// <summary>
        /// 四方向邻居偏移（右/上/左/下）
        /// </summary>
        public static readonly HexCoordinates[] Directions = new[]
        {
            new HexCoordinates(+1,  0), // 右
            new HexCoordinates( 0, +1), // 上
            new HexCoordinates(-1,  0), // 左
            new HexCoordinates( 0, -1), // 下
        };

        /// <summary>
        /// 曼哈顿距离（四方向网格路径距离）
        /// </summary>
        public int DistanceTo(HexCoordinates other)
        {
            return math.abs(q - other.q) + math.abs(r - other.r);
        }

        public static HexCoordinates operator +(HexCoordinates a, HexCoordinates b) =>
            new(a.q + b.q, a.r + b.r);

        public static bool operator ==(HexCoordinates a, HexCoordinates b) =>
            a.q == b.q && a.r == b.r;

        public static bool operator !=(HexCoordinates a, HexCoordinates b) =>
            !(a == b);

        public bool Equals(HexCoordinates other) => this == other;

        public override bool Equals(object obj) =>
            obj is HexCoordinates other && this == other;

        public override int GetHashCode() =>
            (q * 73856093) ^ (r * 19349663);

        public override string ToString() => $"({q}, {r})";
    }
}
