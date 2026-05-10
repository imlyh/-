using UnityEngine;

/// <summary>
/// 全局数值配置——所有营和士兵共享同一份参数。
/// 运行时由 BattalionInitializationSystem 加载并注入到实体。
/// 路径: Assets/Config/BattalionConfig.asset
/// 编辑器窗口: Conquest → Battalion Config
/// </summary>
[CreateAssetMenu(menuName = "Conquest/Battalion Config")]
public class BattalionConfig : ScriptableObject
{
    [Header("士兵（个体战斗）")]
    [Tooltip("检测/攻击范围——士兵单体检测距离，营级也同步此值作为切换Mining/Attacking的触发距离，默认1.5")]
    [Range(0.5f, 5f)] public float attackRange = 1.5f;

    [Tooltip("两次攻击冷却时间（秒），默认1.5")]
    [Range(0.1f, 5f)] public float attackCooldown = 1.5f;

    [Tooltip("冲刺速度，默认10")]
    [Range(1f, 30f)] public float dashSpeed = 10f;

    [Tooltip("冲刺弹跳高度，默认0.25")]
    [Range(0.05f, 1f)] public float dashHeight = 0.25f;

    [Header("营（移动 & 阵型）")]
    [Tooltip("移动速度（单位/秒），默认4")]
    [Range(1f, 10f)] public float moveSpeed = 4f;

    [Tooltip("移动弹跳幅度，默认0.2")]
    [Range(0.05f, 1f)] public float bobHeight = 0.2f;

    [Tooltip("移动弹跳频率（越大越密集），默认8")]
    [Range(1f, 20f)] public float bobFrequency = 8f;

    [Tooltip("士兵间距，默认0.55")]
    [Range(0.2f, 1f)] public float formationSpacing = 0.55f;

    [Header("敌方AI")]
    [Tooltip("采矿持续时长（秒），默认8")]
    [Range(1f, 30f)] public float miningDuration = 8f;

    [Tooltip("攻城持续时长（秒），超时后返回采矿，默认15")]
    [Range(1f, 30f)] public float attackDuration = 15f;

    [Header("NavMeshAgent")]
    [Tooltip("Agent 碰撞半径，默认0.2")]
    [Range(0.5f, 5f)] public float agentRadius = 0.2f;

    [Tooltip("Agent 碰撞高度，默认0.5")]
    [Range(0.5f, 5f)] public float agentHeight = 0.5f;
}
