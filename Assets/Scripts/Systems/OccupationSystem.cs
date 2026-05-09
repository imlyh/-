using Unity.Entities;
using Unity.Collections;

namespace ConquestGame
{
    /// <summary>
    /// 第4个执行的System —— 检测占领金矿并触发肉鸽升级
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleSystem))]
    public partial struct OccupationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // 收集玩家单位所在位置
            var playerPositions = new NativeHashSet<HexCoordinates>(64, state.WorldUpdateAllocator);

            foreach (var unit in SystemAPI.Query<RefRO<UnitData>>())
            {
                if (unit.ValueRO.Owner == OwnerType.Player && unit.ValueRO.Health > 0)
                {
                    playerPositions.Add(unit.ValueRO.CurrentPosition);
                }
            }

            // 检查无主金矿是否被玩家踩中
            int newOccupations = 0;
            foreach (var (goldMine, cell, entity) in
                SystemAPI.Query<RefRW<GoldMineData>, RefRW<HexCellData>>().WithEntityAccess())
            {
                if (goldMine.ValueRO.Owner != OwnerType.None)
                    continue;
                if (!playerPositions.Contains(cell.ValueRO.Coordinates))
                    continue;

                // 玩家占领！
                goldMine.ValueRW.Owner = OwnerType.Player;
                cell.ValueRW.Owner = OwnerType.Player;
                cell.ValueRW.IsOccupied = true;
                newOccupations++;
            }

            // 每占领一个金矿触发一次肉鸽升级（暂时自动随机选取，后续接UI替换）
            var playerData = SystemAPI.GetSingletonRW<PlayerData>();
            for (int i = 0; i < newOccupations; i++)
            {
                ApplyRandomUpgrade(ref playerData.ValueRW);
                playerData.ValueRW.PendingUpgradeCount++;
            }

            ecb.Playback(state.EntityManager);
        }

        private void ApplyRandomUpgrade(ref PlayerData playerData)
        {
            int roll = UnityEngine.Random.Range(0, 3);
            switch (roll)
            {
                case 0:
                    playerData.AttackBonus += 3;
                    break;
                case 1:
                    playerData.DefenseBonus += 2;
                    break;
                case 2:
                    playerData.HealthBonus += 10;
                    break;
            }
        }
    }
}
