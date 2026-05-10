using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;

    [Description("等待指定秒数后返回Success（使用内置Wait更简单，此任务用于演示）")]
    [Category("征服与抉择/敌军AI")]
    public class EnemyWaitDuration : ECSActionTask<EnemyWaitSystem, EnemyWaitComponent>
    {
        [Tooltip("等待时长（秒）")]
        [SerializeField] private float m_Duration = 5f;

        public override ComponentType Flag => typeof(EnemyWaitFlag);

        public override EnemyWaitComponent GetBufferElement() => new()
        {
            Index = RuntimeIndex,
            Duration = m_Duration,
            StartTime = -1f
        };
    }

    public struct EnemyWaitComponent : IBufferElementData
    {
        public ushort Index;
        public float Duration;
        public float StartTime;
    }

    public struct EnemyWaitFlag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    public partial struct EnemyWaitSystem : ISystem
    {
        private void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (taskBuf, compBuf) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<EnemyWaitComponent>>()
                    .WithAll<EnemyWaitFlag, EvaluateFlag>())
            {
                var tasks = taskBuf;
                var comps = compBuf.AsNativeArray();
                for (int i = 0; i < comps.Length; ++i)
                {
                    var comp = comps[i];
                    var task = tasks[comp.Index];

                    if (task.Status == TaskStatus.Queued)
                    {
                        comp.StartTime = time;
                        task.Status = TaskStatus.Running;
                        tasks[comp.Index] = task;
                        comps[i] = comp;
                    }
                    else if (task.Status == TaskStatus.Running)
                    {
                        if (time - comp.StartTime >= comp.Duration)
                        {
                            task.Status = TaskStatus.Success;
                            tasks[comp.Index] = task;
                        }
                    }
                }
            }
        }
    }
}
