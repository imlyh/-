using Unity.Entities;

namespace ConquestGame
{
    public struct GoldMineData : IComponentData
    {
        public int ProductionRate;
        public OwnerType Owner;
    }
}
