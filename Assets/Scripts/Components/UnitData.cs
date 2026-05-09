using Unity.Entities;

namespace ConquestGame
{
    public struct UnitData : IComponentData
    {
        public int Attack;
        public int Defense;
        public int Health;
        public int MaxHealth;
        public float MoveSpeed;
        public OwnerType Owner;
        public UnitState State;
        public HexCoordinates CurrentPosition;
        public HexCoordinates TargetPosition;
        public float MoveTimer;
    }
}
