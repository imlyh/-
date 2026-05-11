using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;

    [Description("设置敌军营的移动目标和命令类型，返回Success")]
    [Category("征服与抉择/敌军AI")]
    public class EnemySetCommand : ECSActionTask<EnemySetCommandSystem, EnemySetCommandComponent>
    {
        [Tooltip("命令类型：0=Move, 1=Mine, 2=Attack")]
        [SerializeField] private int m_CommandType;
        [Tooltip("目标格子X坐标")]
        [SerializeField] private float m_TargetX;
        [Tooltip("目标格子Z坐标")]
        [SerializeField] private float m_TargetZ;

        public override ComponentType Flag => typeof(EnemySetCommandFlag);

        public override EnemySetCommandComponent GetBufferElement() => new()
        {
            Index = RuntimeIndex,
            CommandType = m_CommandType,
            TargetX = m_TargetX,
            TargetZ = m_TargetZ
        };
    }

    public struct EnemySetCommandComponent : IBufferElementData
    {
        public ushort Index;
        public int CommandType;
        public float TargetX;
        public float TargetZ;
    }

    public struct EnemySetCommandFlag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    public partial struct EnemySetCommandSystem : ISystem
    {
        private void OnUpdate(ref SystemState state)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var q = em.CreateEntityQuery(typeof(EnemyAIData), typeof(BattalionData));
            var entities = q.ToEntityArray(Allocator.Temp);

            foreach (var (taskBuf, compBuf) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<EnemySetCommandComponent>>()
                    .WithAll<EnemySetCommandFlag, EvaluateFlag>())
            {
                var tasks = taskBuf;
                for (int i = 0; i < compBuf.Length; ++i)
                {
                    var comp = compBuf[i];
                    var task = tasks[comp.Index];
                    if (task.Status != TaskStatus.Queued) continue;

                    if (entities.Length == 0) { task.Status = TaskStatus.Failure; tasks[comp.Index] = task; break; }

                    // Apply to all enemy battalions
                    for (int j = 0; j < entities.Length; j++)
                    {
                        var bat = em.GetComponentData<BattalionData>(entities[j]);
                        bat.targetCell = new float3(comp.TargetX, 0, comp.TargetZ);
                        bat.commandType = (CommandType)comp.CommandType;
                        bat.state = BattalionState.Moving;
                        em.SetComponentData(entities[j], bat);
                    }

                    task.Status = TaskStatus.Success;
                    tasks[comp.Index] = task;
                }
            }
            entities.Dispose();
        }
    }
}
