using Unity.Entities;

namespace ConquestGame
{
    /// <summary>
    /// 游戏时钟单例 —— 管理时间推进速度和天数
    /// </summary>
    public struct GameTimeData : IComponentData
    {
        public float ElapsedTime;
        public int CurrentDay;
        public float SpeedMultiplier;
        public bool IsNewDay;
    }
}
