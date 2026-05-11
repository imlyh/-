using UnityEngine;
using Opsive.BehaviorDesigner.Runtime;

/// <summary>
/// 桥接 MonoBehaviour，将 BehaviorTree 引用传递给 ECS 初始化系统
/// 挂到场景中任意 GameObject 上，拖入配置好的 BehaviorTree 组件即可
/// </summary>
public class EnemyBTBridge : MonoBehaviour
{
    [Tooltip("拖入配置好敌军行为树的 BehaviorTree 组件（需挂载在某个 GameObject 上）")]
    public BehaviorTree behaviorTreeTemplate;

    void Awake()
    {
        BattalionInitializationSystem.EnemyBTTemplate = behaviorTreeTemplate;
    }
}
