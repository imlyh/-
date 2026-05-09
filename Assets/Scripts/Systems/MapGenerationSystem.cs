using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 地图生成系统 —— 首次运行生成六边形网格、城堡、金矿、起始单位
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct MapGenerationSystem : ISystem
    {
        private bool hasGenerated;

        public void OnCreate(ref SystemState state)
        {
            hasGenerated = false;

            // 创建 MapSettingsData 单例
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(entity, "MapSettingsData");
            state.EntityManager.AddComponentData(entity, new MapSettingsData
            {
                MapRadius = 5,
                PlayerCastlePos = new HexCoordinates(-4, 0),
                EnemyCastlePos = new HexCoordinates(4, 0),
                GoldMineCount = 3
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (hasGenerated)
                return;
            hasGenerated = true;

            var mapSettings = SystemAPI.GetSingletonRW<MapSettingsData>();
            int r = mapSettings.ValueRO.MapRadius;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // 遍历六边形范围内的所有坐标
            for (int q = -r; q <= r; q++)
            {
                int rMin = math.max(-r, -q - r);
                int rMax = math.min(r, -q + r);
                for (int row = rMin; row <= rMax; row++)
                {
                    var coords = new HexCoordinates(q, row);
                    var cellType = GetCellType(coords, mapSettings.ValueRO);
                    OwnerType owner = GetInitialOwner(coords, mapSettings.ValueRO);

                    var cell = ecb.CreateEntity();
                    ecb.SetName(cell, $"HexCell_{coords}");
                    ecb.AddComponent(cell, new HexCellData
                    {
                        Coordinates = coords,
                        CellType = cellType,
                        IsOccupied = false,
                        Owner = owner
                    });
                    ecb.AddComponent(cell, LocalTransform.FromPosition(
                        HexUtils.ToWorldPosition(coords)));

                    // 金矿格子额外添加 GoldMineData
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

            // 创建玩家城堡实体（与 HexCell 重叠，但独立 Entity 便于查询）
            var playerCastle = ecb.CreateEntity();
            ecb.SetName(playerCastle, "PlayerCastle");
            ecb.AddComponent(playerCastle, new HexCellData
            {
                Coordinates = mapSettings.ValueRO.PlayerCastlePos,
                CellType = CellType.Castle,
                IsOccupied = true,
                Owner = OwnerType.Player
            });
            ecb.AddComponent(playerCastle, LocalTransform.FromPosition(
                HexUtils.ToWorldPosition(mapSettings.ValueRO.PlayerCastlePos)));

            // 创建敌方城堡实体
            var enemyCastle = ecb.CreateEntity();
            ecb.SetName(enemyCastle, "EnemyCastle");
            ecb.AddComponent(enemyCastle, new HexCellData
            {
                Coordinates = mapSettings.ValueRO.EnemyCastlePos,
                CellType = CellType.Castle,
                IsOccupied = true,
                Owner = OwnerType.Enemy
            });
            ecb.AddComponent(enemyCastle, LocalTransform.FromPosition(
                HexUtils.ToWorldPosition(mapSettings.ValueRO.EnemyCastlePos)));

            // 创建玩家起始战士（放在城堡旁边）
            var spawnPos = mapSettings.ValueRO.PlayerCastlePos + new HexCoordinates(1, 0);
            var startUnit = ecb.CreateEntity();
            ecb.SetName(startUnit, "PlayerWarrior_1");
            ecb.AddComponent(startUnit, new UnitData
            {
                Attack = 10,
                Defense = 5,
                Health = 100,
                MaxHealth = 100,
                MoveSpeed = 1f,
                Owner = OwnerType.Player,
                State = UnitState.Idle,
                CurrentPosition = spawnPos,
                TargetPosition = spawnPos,
                MoveTimer = 0f
            });
            ecb.AddComponent(startUnit, LocalTransform.FromPosition(
                HexUtils.ToWorldPosition(spawnPos)));

            // 初始化 PlayerData 单例
            var playerData = ecb.CreateEntity();
            ecb.SetName(playerData, "PlayerData");
            ecb.AddComponent(playerData, new PlayerData
            {
                Gold = 1000,
                PopulationCap = 10,
                AttackBonus = 0,
                DefenseBonus = 0,
                HealthBonus = 0
            });

            // 初始化 EnemySpawnerData 单例
            var spawnerData = ecb.CreateEntity();
            ecb.SetName(spawnerData, "EnemySpawnerData");
            ecb.AddComponent(spawnerData, new EnemySpawnerData
            {
                Timer = 0f,
                Interval = 60f
            });

            ecb.Playback(state.EntityManager);
        }

        private CellType GetCellType(HexCoordinates coords, MapSettingsData settings)
        {
            if (coords == settings.PlayerCastlePos || coords == settings.EnemyCastlePos)
                return CellType.Castle;

            // 金矿位置：中间地带分布
            if (coords == new HexCoordinates(-1, 0) ||
                coords == new HexCoordinates(1, 0) ||
                coords == new HexCoordinates(0, 2))
                return CellType.GoldMine;

            return CellType.Plain;
        }

        private OwnerType GetInitialOwner(HexCoordinates coords, MapSettingsData settings)
        {
            if (coords == settings.PlayerCastlePos)
                return OwnerType.Player;
            if (coords == settings.EnemyCastlePos)
                return OwnerType.Enemy;
            return OwnerType.None;
        }
    }
}
