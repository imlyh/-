using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 第2个执行的System —— 单位沿六边形网格向目标移动
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameClockSystem))]
    public partial struct UnitMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var gameTime = SystemAPI.GetSingleton<GameTimeData>();

            foreach (var (unit, transform) in
                SystemAPI.Query<RefRW<UnitData>, RefRW<LocalTransform>>())
            {
                if (unit.ValueRO.State != UnitState.Moving)
                    continue;

                if (unit.ValueRO.CurrentPosition == unit.ValueRO.TargetPosition)
                {
                    unit.ValueRW.State = UnitState.Idle;
                    continue;
                }

                // 移动计时器累加
                unit.ValueRW.MoveTimer += dt * gameTime.SpeedMultiplier;

                float secondsPerHex = 1f / unit.ValueRO.MoveSpeed;
                if (unit.ValueRO.MoveTimer >= secondsPerHex)
                {
                    unit.ValueRW.MoveTimer -= secondsPerHex;

                    // 向目标前进一个格子
                    var nextHex = HexUtils.NextHexToward(
                        unit.ValueRO.CurrentPosition,
                        unit.ValueRO.TargetPosition
                    );

                    unit.ValueRW.CurrentPosition = nextHex;
                }

                // 平滑插值到当前格子的世界坐标
                float3 targetWorld = HexUtils.ToWorldPosition(unit.ValueRO.CurrentPosition);
                transform.ValueRW.Position = math.lerp(
                    transform.ValueRO.Position,
                    targetWorld,
                    math.saturate(unit.ValueRO.MoveTimer / secondsPerHex)
                );
            }
        }
    }
}
