using Unity.Entities;

namespace ConquestGame
{
    public struct HexCellData : IComponentData
    {
        public HexCoordinates Coordinates;
        public CellType CellType;
        public bool IsOccupied;
        public OwnerType Owner;
    }
}
