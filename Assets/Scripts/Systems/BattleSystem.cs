using Unity.Entities;
using Unity.Collections;

namespace ConquestGame
{
    /// <summary>
    /// 第3个执行的System —— 检测同格敌对单位并结算战斗
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitMovementSystem))]
    public partial struct BattleSystem : ISystem
    {
        private const float CombatInterval = 1f;

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var gameTime = SystemAPI.GetSingleton<GameTimeData>();
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            bool anyFighting = false;

            // 先收集所有非移动状态单位的位置信息
            var positionMap = new NativeHashMap<HexCoordinates, BattleEntry>(256, state.WorldUpdateAllocator);

            foreach (var (unit, entity) in
                SystemAPI.Query<RefRW<UnitData>>().WithEntityAccess())
            {
                if (unit.ValueRO.State == UnitState.Moving)
                    continue;
                if (unit.ValueRO.Health <= 0)
                    continue;

                var pos = unit.ValueRO.CurrentPosition;
                if (positionMap.TryGetValue(pos, out var entry))
                {
                    // 同格已有单位
                    if (entry.firstOwner != unit.ValueRO.Owner)
                    {
                        entry.hasConflict = true;
                    }
                    positionMap[pos] = entry;
                }
                else
                {
                    positionMap[pos] = new BattleEntry
                    {
                        firstOwner = unit.ValueRO.Owner,
                        hasConflict = false
                    };
                }
            }

            // 再次遍历，对冲突格子结算战斗
            foreach (var (unit, entity) in
                SystemAPI.Query<RefRW<UnitData>>().WithEntityAccess())
            {
                if (unit.ValueRO.State == UnitState.Moving)
                    continue;
                if (unit.ValueRO.Health <= 0)
                    continue;

                var pos = unit.ValueRO.CurrentPosition;
                if (!positionMap.TryGetValue(pos, out var entry) || !entry.hasConflict)
                {
                    if (unit.ValueRO.State == UnitState.Fighting)
                        unit.ValueRW.State = UnitState.Idle;
                    continue;
                }

                // 进入战斗状态
                unit.ValueRW.State = UnitState.Fighting;
                anyFighting = true;

                // 战斗计时
                unit.ValueRW.CombatTimer += dt * gameTime.SpeedMultiplier;
                if (unit.ValueRO.CombatTimer < CombatInterval)
                    continue;

                unit.ValueRW.CombatTimer -= CombatInterval;

                // 找到同格敌对单位并结算伤害
                foreach (var (enemyData, enemyEntity) in
                    SystemAPI.Query<RefRW<UnitData>>().WithEntityAccess())
                {
                    if (enemyEntity == entity)
                        continue;
                    if (enemyData.ValueRO.Owner == unit.ValueRO.Owner)
                        continue;
                    if (enemyData.ValueRO.CurrentPosition != pos)
                        continue;
                    if (enemyData.ValueRO.Health <= 0)
                        continue;

                    int damage = math.max(1, unit.ValueRO.Attack - enemyData.ValueRO.Defense / 2);
                    enemyData.ValueRW.Health -= damage;

                    if (enemyData.ValueRW.Health <= 0)
                    {
                        ecb.DestroyEntity(enemyEntity);
                    }
                }

                // 如果本单位死亡
                if (unit.ValueRO.Health <= 0)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
        }

        private struct BattleEntry
        {
            public OwnerType firstOwner;
            public bool hasConflict;
        }
    }
}
