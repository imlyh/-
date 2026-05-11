using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;

    [Description("检查任意敌军营是否处于指定状态")]
    [Category("征服与抉择/敌军AI")]
    public class EnemyCheckState : ECSConditionalTask<EnemyCheckStateSystem, EnemyCheckStateComponent>
    {
        [Tooltip("期望状态：0=Idle, 1=Moving, 2=Mining, 3=Attacking")]
        [SerializeField] private int m_ExpectedState;

        public override ComponentType Flag => typeof(EnemyCheckStateFlag);

        public override EnemyCheckStateComponent GetBufferElement() => new()
        {
            Index = RuntimeIndex,
            ExpectedState = m_ExpectedState
        };
    }

    public struct EnemyCheckStateComponent : IBufferElementData
    {
        public ushort Index;
        public int ExpectedState;
    }

    public struct EnemyCheckStateFlag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    public partial struct EnemyCheckStateSystem : ISystem
    {
        private void OnUpdate(ref SystemState state)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            foreach (var (taskBuf, compBuf) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<EnemyCheckStateComponent>>()
                    .WithAll<EnemyCheckStateFlag, EvaluateFlag>())
            {
                var tasks = taskBuf;
                for (int i = 0; i < compBuf.Length; ++i)
                {
                    var comp = compBuf[i];
                    var task = tasks[comp.Index];
                    if (task.Status != TaskStatus.Queued) continue;

                    var q = em.CreateEntityQuery(typeof(EnemyAIData), typeof(BattalionData));
                    var entities = q.ToEntityArray(Allocator.Temp);
                    bool match = false;
                    for (int j = 0; j < entities.Length; j++)
                    {
                        var bat = em.GetComponentData<BattalionData>(entities[j]);
                        if ((int)bat.state == comp.ExpectedState) { match = true; break; }
                    }
                    entities.Dispose();

                    task.Status = match ? TaskStatus.Success : TaskStatus.Failure;
                    tasks[comp.Index] = task;
                }
            }
        }
    }
}
