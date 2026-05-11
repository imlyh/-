using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// 为每个士兵设置Boids目标位置：
/// - Idle: 营中心 + 随机偏移（松散编队）
/// - Moving: 营移动方向前方（松散跟随）
/// - InCombat: 如果士兵有 currentTarget，冲向目标；否则归队
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionStateSystem))]
[UpdateBefore(typeof(BoidMovementSystem))]
public partial class BoidTargetSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var em = EntityManager;

        foreach (var (sdRef, ltxRef, entity) in
            SystemAPI.Query<RefRW<SoldierData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            var sd = sdRef.ValueRW;
            if (!em.Exists(sd.battalionEntity) ||
                !em.HasComponent<BattalionData>(sd.battalionEntity) ||
                !em.HasComponent<LocalToWorld>(sd.battalionEntity))
                continue;

            var batData = em.GetComponentData<BattalionData>(sd.battalionEntity);
            var batPos = em.GetComponentData<LocalToWorld>(sd.battalionEntity).Position;
            batPos.y = 0;

            // Per-soldier deterministic random offset for loose formation
            uint seed = (uint)(entity.Index * 2654435761u);
            float randomOffsetX = ((seed & 0xFF) / 255f - 0.5f) * 1.2f;
            float randomOffsetZ = (((seed >> 8) & 0xFF) / 255f - 0.5f) * 1.2f;
            float3 looseOffset = new float3(randomOffsetX, 0, randomOffsetZ);

            if (batData.state == BattalionState.InCombat && sd.currentTarget != Entity.Null)
            {
                // Target enemy position
                if (em.Exists(sd.currentTarget) && em.HasComponent<LocalToWorld>(sd.currentTarget))
                {
                    sd.targetPosition = em.GetComponentData<LocalToWorld>(sd.currentTarget).Position;
                    sd.targetPosition.y = 0;
                }
                else
                {
                    sd.currentTarget = Entity.Null;
                    sd.targetPosition = batPos + looseOffset;
                }
            }
            else if (batData.state == BattalionState.Moving)
            {
                // Spread around the battalion in direction of movement
                float3 toTarget = batData.targetCell - batPos;
                toTarget.y = 0;
                float moveDist = math.length(toTarget);
                if (moveDist > 0.5f)
                {
                    float3 moveDir = math.normalize(toTarget);
                    sd.targetPosition = batPos + moveDir * 1.0f + looseOffset;
                }
                else
                {
                    sd.targetPosition = batPos + looseOffset;
                }
                sd.currentTarget = Entity.Null;
            }
            else
            {
                // Idle: loose formation
                sd.targetPosition = batPos + looseOffset;
                sd.currentTarget = Entity.Null;
            }

            sdRef.ValueRW = sd;
        }
    }
}
