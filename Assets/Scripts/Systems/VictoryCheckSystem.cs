using Unity.Entities;
using Unity.Collections;

namespace ConquestGame
{
    /// <summary>
    /// 第7个执行的System —— 每帧检查胜负条件
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyAISystem))]
    public partial struct VictoryCheckSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var mapSettings = SystemAPI.GetSingleton<MapSettingsData>();
            var playerData = SystemAPI.GetSingleton<PlayerData>();

            bool playerCastleAlive = false;
            bool enemyCastleAlive = false;
            int playerUnitCount = 0;

            // 检查城堡状态
            foreach (var cell in SystemAPI.Query<RefRO<HexCellData>>())
            {
                if (cell.ValueRO.Coordinates == mapSettings.PlayerCastlePos
                    && cell.ValueRO.Owner == OwnerType.Player)
                {
                    playerCastleAlive = true;
                }
                if (cell.ValueRO.Coordinates == mapSettings.EnemyCastlePos
                    && cell.ValueRO.Owner == OwnerType.Enemy)
                {
                    enemyCastleAlive = true;
                }
            }

            // 统计存活的玩家单位
            foreach (var unit in SystemAPI.Query<RefRO<UnitData>>())
            {
                if (unit.ValueRO.Owner == OwnerType.Player && unit.ValueRO.Health > 0)
                {
                    playerUnitCount++;
                }
            }

            // 胜利：攻占敌方城堡
            if (!enemyCastleAlive)
            {
                UnityEngine.Debug.Log("胜利！敌方城堡已被攻占！");
                state.Enabled = false;
                return;
            }

            // 失败：主城被毁
            if (!playerCastleAlive)
            {
                UnityEngine.Debug.Log("失败！我方城堡已被摧毁！");
                state.Enabled = false;
                return;
            }

            // 失败：全部队阵亡 + 不够钱招募
            if (playerUnitCount == 0 && playerData.Gold < 100)
            {
                UnityEngine.Debug.Log("失败！全军覆没且资金不足以招募新兵！");
                state.Enabled = false;
                return;
            }
        }
    }
}
