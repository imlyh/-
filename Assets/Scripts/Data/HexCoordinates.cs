using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 六边形轴向坐标 (Axial Coordinates)
    /// q = column, r = row
    /// </summary>
    public struct HexCoordinates
    {
        public int q;
        public int r;

        public HexCoordinates(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        // 六个方向邻居的偏移量
        public static readonly HexCoordinates[] Directions = new[]
        {
            new HexCoordinates(+1,  0), // 右
            new HexCoordinates(+1, -1), // 右上
            new HexCoordinates( 0, -1), // 左上
            new HexCoordinates(-1,  0), // 左
            new HexCoordinates(-1, +1), // 左下
            new HexCoordinates( 0, +1), // 右下
        };

        public int DistanceTo(HexCoordinates other)
        {
            int s1 = -q - r;
            int s2 = -other.q - other.r;
            return (math.abs(q - other.q) + math.abs(r - other.r) + math.abs(s1 - s2)) / 2;
        }

        public static HexCoordinates operator +(HexCoordinates a, HexCoordinates b) =>
            new(a.q + b.q, a.r + b.r);

        public static bool operator ==(HexCoordinates a, HexCoordinates b) =>
            a.q == b.q && a.r == b.r;

        public static bool operator !=(HexCoordinates a, HexCoordinates b) =>
            !(a == b);

        public override bool Equals(object obj) =>
            obj is HexCoordinates other && this == other;

        public override int GetHashCode() =>
            (q * 73856093) ^ (r * 19349663);

        public override string ToString() => $"({q}, {r})";
    }
}
