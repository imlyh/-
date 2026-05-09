using Unity.Entities;

namespace ConquestGame
{
    /// <summary>
    /// 单例组件 —— 敌方 AI 出兵计时器
    /// </summary>
    public struct EnemySpawnerData : IComponentData
    {
        public float Timer;
        public float Interval;
    }
}
