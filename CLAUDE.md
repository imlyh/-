# 项目：征服与抉择 - SLG+Roguelike 原型

## 技术栈
- Unity 6000.1+ (DOTS 1.3+) / C#
- **架构：ECS + Job System + Burst**
- 2D俯视角 / 低多边形风格
- 六边形地图（Entity表示格子）

## 核心规则
- **实时时间流动**，1天=24秒（1x），支持2x/4x加速
- **起始资金**：1000金，1支战士
- **经济循环**：招募战士 → 占领金矿 → 获得金币 → 继续招募
- **肉鸽升级**：每占领一个金矿触发三选一（攻击/防御/生命加成）
- **胜利**：攻占敌方城堡
- **失败**：主城被毁 或 全部队阵亡+不够钱招募

## 简化范围（原型阶段）
- 地形：仅平原（六边形网格）
- 资源点：仅金矿
- 兵种：仅战士
- 守军：仅敌方战士（数据镜像）
- 升级：仅数值加成（攻/防/血）
- 敌方AI：每60秒派1支巡逻队进攻

## ECS架构规范
- **严禁使用MonoBehaviour存储运行时数据**
- MonoBehaviour仅用于：UI引用、Prefab引用、编辑器配置、MonoBehaviour入口桥接
- 所有游戏数据存储在IComponentData/IBufferElementData中
- 所有逻辑写在System中（ISystem/SystemBase）
- 计算密集型代码用IJobEntity/IJobChunk + Burst编译
- 六边形地图数据用NativeArray/NativeHashMap存储
- 事件通知用EntityCommandBuffer或单例Entity

## 核心ECS设计

### Entity类型
- **HexCell**: 格子（坐标、类型、占据者）
- **Unit**: 战士单位（属性、位置、目标）
- **GoldMine**: 金矿（产量、所属、守军）
- **Player**: 单例（金币、人口上限、全局加成）
- **EnemySpawner**: 单例（出兵计时器）

### System执行顺序
1. GameClockSystem - 时间推进+每日事件
2. UnitMovementSystem - 寻路+移动
3. BattleSystem - 战斗检测+结算
4. OccupationSystem - 占领+触发升级
5. ProductionSystem - 资源产出
6. EnemyAISystem - 出兵+敌方移动
7. VictoryCheckSystem - 胜负判定
