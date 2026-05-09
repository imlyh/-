using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 第6个执行的System —— 定时出兵 + 敌方单位向玩家城堡推进
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    public partial struct EnemyAISystem : ISystem
    {
        private int spawnCount;

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var gameTime = SystemAPI.GetSingleton<GameTimeData>();
            var mapSettings = SystemAPI.GetSingleton<MapSettingsData>();

            var spawner = SystemAPI.GetSingletonRW<EnemySpawnerData>();
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // 出兵计时
            spawner.ValueRW.Timer += dt * gameTime.SpeedMultiplier;
            if (spawner.ValueRO.Timer >= spawner.ValueRO.Interval)
            {
                spawner.ValueRW.Timer -= spawner.ValueRO.Interval;
                spawnCount++;

                var spawnPos = mapSettings.EnemyCastlePos + new HexCoordinates(-1, 0);
                var enemy = ecb.CreateEntity();
                ecb.SetName(enemy, $"EnemyWarrior_{spawnCount}");
                ecb.AddComponent(enemy, new UnitData
                {
                    Attack = 10,
                    Defense = 5,
                    Health = 100,
                    MaxHealth = 100,
                    MoveSpeed = 0.8f,
                    Owner = OwnerType.Enemy,
                    State = UnitState.Moving,
                    CurrentPosition = spawnPos,
                    TargetPosition = mapSettings.PlayerCastlePos,
                    MoveTimer = 0f,
                    CombatTimer = 0f
                });
                ecb.AddComponent(enemy, LocalTransform.FromPosition(
                    HexUtils.ToWorldPosition(spawnPos)));
            }

            // 空闲的敌方单位自动向玩家城堡推进
            foreach (var (unit, entity) in
                SystemAPI.Query<RefRW<UnitData>>().WithEntityAccess())
            {
                if (unit.ValueRO.Owner != OwnerType.Enemy)
                    continue;
                if (unit.ValueRO.State != UnitState.Idle)
                    continue;

                unit.ValueRW.TargetPosition = mapSettings.PlayerCastlePos;
                unit.ValueRW.State = UnitState.Moving;
                unit.ValueRW.MoveTimer = 0f;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
