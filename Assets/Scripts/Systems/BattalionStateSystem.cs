using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// 营级状态机：Idle↔Moving↔InCombat
/// 使用 ECS Query 检测敌人，维护 soldierCount
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionInputSystem))]
[UpdateBefore(typeof(BoidTargetSystem))]
public partial class BattalionStateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var em = EntityManager;

        // Build soldier lookup: positions + battalion + isEnemy
        var soldierPositions = new NativeList<float3>(256, Allocator.Temp);
        var soldierBattalionEntities = new NativeList<Entity>(256, Allocator.Temp);
        var soldierIsEnemy = new NativeList<byte>(256, Allocator.Temp);

        foreach (var (sd, ltw) in SystemAPI.Query<RefRO<SoldierData>, RefRO<LocalToWorld>>())
        {
            if (sd.ValueRO.currentHP <= 0) continue;
            soldierPositions.Add(ltw.ValueRO.Position);
            soldierBattalionEntities.Add(sd.ValueRO.battalionEntity);

            bool isEnemy = false;
            if (em.Exists(sd.ValueRO.battalionEntity) &&
                em.HasComponent<BattalionData>(sd.ValueRO.battalionEntity) &&
                em.GetComponentData<BattalionData>(sd.ValueRO.battalionEntity).owner == BattalionOwner.Enemy)
                isEnemy = true;
            soldierIsEnemy.Add(isEnemy ? (byte)1 : (byte)0);
        }

        // Process each battalion
        foreach (var (batRef, ltw, entity) in
            SystemAPI.Query<RefRW<BattalionData>, RefRO<LocalToWorld>>().WithEntityAccess())
        {
            var bat = batRef.ValueRW;
            float3 flatPos = ltw.ValueRO.Position;
            flatPos.y = 0;

            // Count alive soldiers for this battalion
            int aliveCount = 0;
            for (int i = 0; i < soldierPositions.Length; i++)
            {
                if (soldierBattalionEntities[i] == entity)
                    aliveCount++;
            }
            bat.soldierCount = aliveCount;

            // --- State transitions ---

            if (bat.state == BattalionState.Idle)
            {
                if (HasEnemyNearby(flatPos, entity, bat.owner, bat.detectionRange,
                    soldierPositions, soldierBattalionEntities, soldierIsEnemy))
                {
                    bat.state = BattalionState.InCombat;
                }
            }

            if (bat.state == BattalionState.Moving)
            {
                float3 toTarget = bat.targetCell - flatPos;
                toTarget.y = 0;
                bool arrived = math.lengthsq(toTarget) < 1.5f * 1.5f;
                bool enemiesNear = HasEnemyNearby(flatPos, entity, bat.owner, bat.engageRadius,
                    soldierPositions, soldierBattalionEntities, soldierIsEnemy);

                if (arrived)
                {
                    bat.state = enemiesNear ? BattalionState.InCombat : BattalionState.Idle;
                }
                else if (enemiesNear)
                {
                    // Attack-move: soldiers auto-engage
                    bat.state = BattalionState.InCombat;
                }
            }

            if (bat.state == BattalionState.InCombat)
            {
                if (!HasEnemyNearby(flatPos, entity, bat.owner, bat.detectionRange * 1.5f,
                    soldierPositions, soldierBattalionEntities, soldierIsEnemy))
                {
                    bat.state = BattalionState.Idle;
                    bat.targetEnemy = Entity.Null;
                }
            }

            batRef.ValueRW = bat;
        }

        soldierPositions.Dispose();
        soldierBattalionEntities.Dispose();
        soldierIsEnemy.Dispose();
    }

    static bool HasEnemyNearby(float3 pos, Entity myBattalion, BattalionOwner myOwner, float range,
        NativeList<float3> soldierPositions, NativeList<Entity> soldierBattalionEntities,
        NativeList<byte> soldierIsEnemy)
    {
        float rangeSq = range * range;
        for (int i = 0; i < soldierPositions.Length; i++)
        {
            if (soldierBattalionEntities[i] == myBattalion) continue;
            if (soldierIsEnemy[i] == 0) continue;

            float3 diff = soldierPositions[i] - pos;
            diff.y = 0;
            if (math.lengthsq(diff) < rangeSq)
                return true;
        }
        return false;
    }
}
