using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// ============================================================================
/// MapGenerationSystem — 首次运行时生成方形网格地图
/// ============================================================================
///
/// 【设计思路】
///   创建一个 col×row 的矩形地图（默认 10×8）。在这个矩形范围外还有一个
///   额外一圈用于放置双方城堡（地图外左侧/右侧）。
///   在地图中央均匀散布金矿。
///
/// 【布局（默认 10×8）】
///   玩家城堡 (-1, 4)       敌方城堡 (10, 4)
///   金矿 (2, 3), (5, 5), (8, 3)
///   玩家起始战士 (-1, 4) → 城堡旁
///
/// 【执行顺序】
///   SimulationSystemGroup.OrderFirst —— 确保在所有其他 System 前生成地图
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
                MapRadius = 5,                             // 兼容旧字段，这里当 gridWidth 用
                PlayerCastlePos = new HexCoordinates(-1, 4),
                EnemyCastlePos = new HexCoordinates(10, 4),
                GoldMineCount = 3
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (hasGenerated)
                return;
            hasGenerated = true;

            var map = SystemAPI.GetSingletonRW<MapSettingsData>();
            int w = 10; // col 范围
            int h = 8;  // row 范围
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // === 生成矩形网格 ===
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
                        IsOccupied = false,
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

            // === 城堡格子（地图外左右两侧）===
            var playerCastleCoord = map.ValueRO.PlayerCastlePos;
            var enemyCastleCoord = map.ValueRO.EnemyCastlePos;

            var pc = ecb.CreateEntity();
            ecb.SetName(pc, "PlayerCastle");
            ecb.AddComponent(pc, new HexCellData
            {
                Coordinates = playerCastleCoord,
                CellType = CellType.Castle,
                IsOccupied = true,
                Owner = OwnerType.Player
            });
            ecb.AddComponent(pc, LocalTransform.FromPosition(
                HexUtils.ToWorldPosition(playerCastleCoord)));

            var ec = ecb.CreateEntity();
            ecb.SetName(ec, "EnemyCastle");
            ecb.AddComponent(ec, new HexCellData
            {
                Coordinates = enemyCastleCoord,
                CellType = CellType.Castle,
                IsOccupied = true,
                Owner = OwnerType.Enemy
            });
            ecb.AddComponent(ec, LocalTransform.FromPosition(
                HexUtils.ToWorldPosition(enemyCastleCoord)));

            // === 玩家起始战士（城堡旁）===
            var spawnCoord = new HexCoordinates(playerCastleCoord.q + 1, playerCastleCoord.r);
            var startUnit = ecb.CreateEntity();
            ecb.SetName(startUnit, "PlayerWarrior_1");
            ecb.AddComponent(startUnit, new UnitData
            {
                Attack = 10,
                Defense = 5,
                Health = 100,
                MaxHealth = 100,
                MoveSpeed = 2f,
                Owner = OwnerType.Player,
                State = UnitState.Idle,
                CurrentPosition = spawnCoord,
                TargetPosition = spawnCoord,
                MoveTimer = 0f,
                CombatTimer = 0f
            });
            ecb.AddComponent(startUnit, LocalTransform.FromPosition(
                HexUtils.ToWorldPosition(spawnCoord)));

            // === 初始单例 ===
            var pd = ecb.CreateEntity();
            ecb.SetName(pd, "PlayerData");
            ecb.AddComponent(pd, new PlayerData
            {
                Gold = 1000,
                PopulationCap = 10,
                AttackBonus = 0,
                DefenseBonus = 0,
                HealthBonus = 0,
                PendingUpgradeCount = 0
            });

            var sd = ecb.CreateEntity();
            ecb.SetName(sd, "EnemySpawnerData");
            ecb.AddComponent(sd, new EnemySpawnerData
            {
                Timer = 0f,
                Interval = 60f
            });

            ecb.Playback(state.EntityManager);
        }

        private CellType GetCellType(HexCoordinates coord, MapSettingsData s)
        {
            // 金矿散布在网格中
            if (coord == new HexCoordinates(2, 3) ||
                coord == new HexCoordinates(5, 5) ||
                coord == new HexCoordinates(8, 3))
                return CellType.GoldMine;
            return CellType.Plain;
        }

        private OwnerType GetInitialOwner(HexCoordinates coord, MapSettingsData s)
        {
            return OwnerType.None;
        }
    }
}
