
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BoidMovementSystem))]
public partial class CombatSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var em = EntityManager;

        foreach (var (sdRef, ltxRef) in SystemAPI.Query<RefRW<SoldierData>, RefRW<LocalTransform>>())
        {
            var sd = sdRef.ValueRW;

            // Cooldown
            if (sd.cooldownRemaining > 0) sd.cooldownRemaining -= dt;

            var batData = SystemAPI.GetComponent<BattalionData>(sd.battalionEntity);
            float3 worldPos = ltxRef.ValueRO.Position;

            if (batData.state == BattalionState.InCombat)
            {
                // Find target if none or dead
                if (sd.currentTarget == Entity.Null || !em.Exists(sd.currentTarget) ||
                    (em.HasComponent<HealthData>(sd.currentTarget) &&
                     em.GetComponentData<HealthData>(sd.currentTarget).currentHP <= 0))
                {
                    sd.currentTarget = FindClosestEnemy(worldPos, batData.owner, sd.attackRange * 3);
                }

                // Attack if in range and cooldown ready
                if (sd.currentTarget != Entity.Null && sd.attackState == 0 && sd.cooldownRemaining <= 0)
                {
                    float3 targetPos = em.GetComponentData<LocalToWorld>(sd.currentTarget).Position;
                    float dist = math.distance(worldPos, targetPos);
                    if (dist < sd.attackRange)
                    {
                        sd.attackState = 1;
                        sd.attackOrigin = worldPos;
                        sd.attackOrigin.y = 0;
                        sd.attackTarget = targetPos;
                        sd.attackTarget.y = 0;
                        sd.attackT = 0;
                        float dashDist = math.distance(sd.attackOrigin, sd.attackTarget);
                        sd.dashTotalTime = math.max(0.08f, dashDist / sd.dashSpeed);
                        sd.cooldownRemaining = sd.attackCooldown;
                    }
                }
            }

            // Attack animation
            if (sd.attackState != 0)
            {
                ProcessDash(ref sd, ref ltxRef.ValueRW, dt, em);
            }

            sdRef.ValueRW = sd;
        }
    }

    Entity FindClosestEnemy(float3 worldPos, BattalionOwner owner, float searchRadius)
    {
        var em = EntityManager;
        var query = em.CreateEntityQuery(typeof(SoldierData), typeof(HealthData), typeof(LocalToWorld));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        var sds = query.ToComponentDataArray<SoldierData>(Unity.Collections.Allocator.Temp);
        var ltws = query.ToComponentDataArray<LocalToWorld>(Unity.Collections.Allocator.Temp);

        Entity closest = Entity.Null;
        float minDist = searchRadius * searchRadius;
        for (int i = 0; i < entities.Length; i++)
        {
            if (sds[i].battalionEntity == Entity.Null) continue;
            if (!em.Exists(sds[i].battalionEntity)) continue;
            var otherBat = em.GetComponentData<BattalionData>(sds[i].battalionEntity);
            if (otherBat.owner == owner) continue;

            float d = math.distancesq(worldPos, ltws[i].Position);
            if (d < minDist) { minDist = d; closest = entities[i]; }
        }

        entities.Dispose(); sds.Dispose(); ltws.Dispose();
        return closest;
    }

    void ProcessDash(ref SoldierData sd, ref LocalTransform ltx, float dt, EntityManager em)
    {
        if (sd.attackState == 1) // Forward
        {
            sd.attackT += dt / sd.dashTotalTime;
            if (sd.attackT >= 1f)
            {
                // Hit!
                ApplyDamage(sd.attackTarget, sd.battalionEntity);
                sd.attackState = 2;
                sd.attackT = 0;
            }
            else
            {
                float t = math.clamp(sd.attackT, 0, 1);
                float3 pos = math.lerp(sd.attackOrigin, sd.attackTarget, EaseOut(t));
                pos.y = sd.dashHeight * math.sin(t * math.PI);
                ltx.Position = pos;
            }
        }
        else if (sd.attackState == 2) // Back
        {
            sd.attackT += dt / sd.dashTotalTime;
            if (sd.attackT >= 1f)
            {
                ltx.Position = sd.attackOrigin;
                sd.attackState = 0;
            }
            else
            {
                float t = math.clamp(sd.attackT, 0, 1);
                float3 pos = math.lerp(sd.attackTarget, sd.attackOrigin, EaseOut(t));
                pos.y = sd.dashHeight * math.sin(t * math.PI);
                ltx.Position = pos;
            }
        }
    }

    void ApplyDamage(float3 targetPos, Entity attackerBattalion)
    {
        // Apply to any entity with HealthData at the impact position
        foreach (var (link, sd) in SystemAPI.Query<RefRO<EntityLink>, RefRO<SoldierData>>())
            if (math.distancesq(targetPos, SystemAPI.GetComponent<LocalToWorld>(sd.ValueRO.battalionEntity).Position) < 2f
                && sd.ValueRO.battalionEntity == attackerBattalion)
                return; // skip friendly

        foreach (var (link, hpRef, ltw) in SystemAPI.Query<RefRO<EntityLink>, RefRW<HealthData>, RefRO<LocalToWorld>>())
        {
            if (math.distancesq(targetPos, ltw.ValueRO.Position) < 2f)
            {
                var hp = hpRef.ValueRW;
                hp.currentHP -= 5;
                if (hp.currentHP < 0) hp.currentHP = 0;
                hpRef.ValueRW = hp;
                return;
            }
        }
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
