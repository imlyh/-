using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// ============================================================================
/// MapGenerationSystem — 首次运行时生成方形网格地图
/// ============================================================================
///
/// 【设计思路】
///   生成 col×row 矩形地图（默认 10×8），城堡和金矿都在地图内部。
///   玩家城堡在左侧、敌方城堡在右侧，金矿散布中央。
///
/// 【布局（默认 10×8）】
///   玩家城堡 (0, 4)         敌人城堡 (9, 4)
///   金矿 (2, 3), (5, 5), (8, 3)
///   玩家起始战士 (1, 4)  —— 城堡右边
/// ============================================================================
namespace ConquestGame
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct MapGenerationSystem : ISystem
    {
        private bool hasGenerated;

        public void OnCreate(ref SystemState state)
        {
            hasGenerated = false;

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(entity, "MapSettingsData");
            state.EntityManager.AddComponentData(entity, new MapSettingsData
            {
                MapRadius = 5,
                PlayerCastlePos = new HexCoordinates(0, 4),
                EnemyCastlePos = new HexCoordinates(9, 4),
                GoldMineCount = 3
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (hasGenerated)
                return;
            hasGenerated = true;

            var map = SystemAPI.GetSingletonRW<MapSettingsData>();
            int w = 100, h = 100;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // === 生成矩形网格（城堡和金矿都在网格内）===
            for (int col = 0; col < w; col++)
            {
                for (int row = 0; row < h; row++)
                {
                    var coord = new HexCoordinates(col, row);
                    var cellType = GetCellType(coord, map.ValueRO);
                    OwnerType owner = GetInitialOwner(coord, map.ValueRO);

                    var cell = ecb.CreateEntity();
                    ecb.SetName(cell, $"Cell_{coord}");
                    ecb.AddComponent(cell, new HexCellData
                    {
                        Coordinates = coord,
                        CellType = cellType,
                        IsOccupied = cellType == CellType.Castle,
                        Owner = owner
                    });
                    ecb.AddComponent(cell, LocalTransform.FromPosition(
                        HexUtils.ToWorldPosition(coord)));

                    if (cellType == CellType.GoldMine)
                    {
                        ecb.AddComponent(cell, new GoldMineData
                        {
                            ProductionRate = 50,
                            Owner = OwnerType.None
                        });
                    }
                }
            }

            // === 玩家起始战士 ===
            var spawnCoord = new HexCoordinates(1, 4);
            var startUnit = ecb.CreateEntity();
            ecb.SetName(startUnit, "PlayerWarrior_1");
            ecb.AddComponent(startUnit, new UnitData
            {
                Attack = 10, Defense = 5,
                Health = 100, MaxHealth = 100,
                MoveSpeed = 2f,
                Owner = OwnerType.Player,
                State = UnitState.Idle,
                CurrentPosition = spawnCoord,
                TargetPosition = spawnCoord,
                MoveTimer = 0f, CombatTimer = 0f
            });
            ecb.AddComponent(startUnit, LocalTransform.FromPosition(
                HexUtils.ToWorldPosition(spawnCoord)));

            // === 单例 ===
            var pd = ecb.CreateEntity();
            ecb.SetName(pd, "PlayerData");
            ecb.AddComponent(pd, new PlayerData
            {
                Gold = 1000, PopulationCap = 10,
                AttackBonus = 0, DefenseBonus = 0, HealthBonus = 0,
                PendingUpgradeCount = 0
            });

            var sd = ecb.CreateEntity();
            ecb.SetName(sd, "EnemySpawnerData");
            ecb.AddComponent(sd, new EnemySpawnerData
            {
                Timer = 0f, Interval = 60f
            });

            ecb.Playback(state.EntityManager);
        }

        private CellType GetCellType(HexCoordinates coord, MapSettingsData s)
        {
            if (coord == s.PlayerCastlePos || coord == s.EnemyCastlePos)
                return CellType.Castle;
            if (coord == new HexCoordinates(2, 3) ||
                coord == new HexCoordinates(5, 5) ||
                coord == new HexCoordinates(8, 3))
                return CellType.GoldMine;
            return CellType.Plain;
        }

        private OwnerType GetInitialOwner(HexCoordinates coord, MapSettingsData s)
        {
            if (coord == s.PlayerCastlePos) return OwnerType.Player;
            if (coord == s.EnemyCastlePos) return OwnerType.Enemy;
            return OwnerType.None;
        }
    }
}
