
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 每帧为每个士兵设置目标位置：
/// - Moving: 目标 = 营中心前方一点（营移动方向）
/// - InCombat: 目标 = currentTarget 的位置
/// - Idle: 目标 = 营中心
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionStateSystem))]
[UpdateBefore(typeof(BoidMovementSystem))]
public partial class BoidTargetSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (sdRef, ltxRef) in SystemAPI.Query<RefRW<SoldierData>, RefRO<LocalTransform>>())
        {
            var sd = sdRef.ValueRW;
            var batData = SystemAPI.GetComponent<BattalionData>(sd.battalionEntity);
            var batPos = EntityManager.GetComponentData<LocalToWorld>(sd.battalionEntity).Position;

            if (batData.state == BattalionState.InCombat && sd.currentTarget != Entity.Null)
            {
                // Attracted to target
                if (EntityManager.Exists(sd.currentTarget) && EntityManager.HasComponent<LocalToWorld>(sd.currentTarget))
                    sd.targetPosition = EntityManager.GetComponentData<LocalToWorld>(sd.currentTarget).Position;
                else
                {
                    sd.currentTarget = Entity.Null;
                    sd.targetPosition = batPos;
                }
            }
            else
            {
                // Boids around battalion center
                sd.targetPosition = batPos;
                sd.currentTarget = Entity.Null;
            }

            sdRef.ValueRW = sd;
        }
    }
}
