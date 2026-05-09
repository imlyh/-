using Unity.Entities;
using Unity.Mathematics;

namespace ConquestGame
{
    /// <summary>
    /// 第1个执行的System —— 时间推进 + 每日事件触发
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MapGenerationSystem))]
    public partial struct GameClockSystem : ISystem
    {
        private const float DayDuration = 24f;

        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(entity, "GameTimeData");
            state.EntityManager.AddComponentData(entity, new GameTimeData
            {
                ElapsedTime = 0f,
                CurrentDay = 1,
                SpeedMultiplier = 1f,
                IsNewDay = false
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameTime = SystemAPI.GetSingletonRW<GameTimeData>();
            float dt = SystemAPI.Time.DeltaTime;

            gameTime.ValueRW.ElapsedTime += dt * gameTime.ValueRO.SpeedMultiplier;

            int expectedDay = (int)math.floor(gameTime.ValueRO.ElapsedTime / DayDuration) + 1;
            if (expectedDay > gameTime.ValueRO.CurrentDay)
            {
                gameTime.ValueRW.CurrentDay = expectedDay;
                gameTime.ValueRW.IsNewDay = true;
            }
            else
            {
                gameTime.ValueRW.IsNewDay = false;
            }
        }
    }
}
