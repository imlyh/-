using Unity.Entities;

namespace ConquestGame
{
    /// <summary>
    /// 单例组件 —— 整个 World 只有一个 Entity 持有
    /// </summary>
    public struct PlayerData : IComponentData
    {
        public int Gold;
        public int PopulationCap;
        public int AttackBonus;
        public int DefenseBonus;
        public int HealthBonus;
    }
}
