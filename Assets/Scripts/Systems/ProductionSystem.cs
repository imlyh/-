using Unity.Entities;

namespace ConquestGame
{
    /// <summary>
    /// 第5个执行的System —— 每日金矿产金，计入玩家金币
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OccupationSystem))]
    public partial struct ProductionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var gameTime = SystemAPI.GetSingleton<GameTimeData>();
            if (!gameTime.IsNewDay)
                return;

            int dailyGold = 0;
            foreach (var goldMine in SystemAPI.Query<RefRO<GoldMineData>>())
            {
                if (goldMine.ValueRO.Owner == OwnerType.Player)
                {
                    dailyGold += goldMine.ValueRO.ProductionRate;
                }
            }

            if (dailyGold > 0)
            {
                var playerData = SystemAPI.GetSingletonRW<PlayerData>();
                playerData.ValueRW.Gold += dailyGold;
            }
        }
    }
}
