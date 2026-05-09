using Unity.Entities;

namespace ConquestGame
{
    /// <summary>
    /// 地图配置单例 —— 控制地图大小和关键位置
    /// </summary>
    public struct MapSettingsData : IComponentData
    {
        public int MapRadius;
        public HexCoordinates PlayerCastlePos;
        public HexCoordinates EnemyCastlePos;
        public int GoldMineCount;
    }
}
