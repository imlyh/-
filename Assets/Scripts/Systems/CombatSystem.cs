using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// 士兵级战斗系统：自动索敌、攻击、归队
/// - 有目标 → 攻击
/// - 目标死亡 → 自动切换下一个
/// - 无敌人 → 清除目标（BoidTargetSystem 将其带回营）
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BoidMovementSystem))]
public partial class CombatSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var em = EntityManager;

        // Build enemy soldier list for efficient targeting
        var allEntities = new NativeList<Entity>(256, Allocator.Temp);
        var allSd = new NativeList<SoldierData>(256, Allocator.Temp);
        var allLtw = new NativeList<LocalToWorld>(256, Allocator.Temp);
        var allHp = new NativeList<HealthData>(256, Allocator.Temp);

        foreach (var (sd, ltw, hp, entity) in
            SystemAPI.Query<RefRO<SoldierData>, RefRO<LocalToWorld>, RefRO<HealthData>>().WithEntityAccess())
        {
            allEntities.Add(entity);
            allSd.Add(sd.ValueRO);
            allLtw.Add(ltw.ValueRO);
            allHp.Add(hp.ValueRO);
        }

        // Castle data for targeting
        var castleEntities = new NativeList<Entity>(8, Allocator.Temp);
        var castleLtw = new NativeList<LocalToWorld>(8, Allocator.Temp);
        var castleHp = new NativeList<HealthData>(8, Allocator.Temp);
        var castleOwner = new NativeList<BattalionOwner>(8, Allocator.Temp);

        foreach (var (castle, hp, ltw, entity) in
            SystemAPI.Query<RefRO<CastleTag>, RefRO<HealthData>, RefRO<LocalToWorld>>().WithEntityAccess())
        {
            castleEntities.Add(entity);
            castleLtw.Add(ltw.ValueRO);
            castleHp.Add(hp.ValueRO);
            castleOwner.Add(castle.ValueRO.owner);
        }

        // Process each soldier
        foreach (var (sdRef, ltxRef, entity) in
            SystemAPI.Query<RefRW<SoldierData>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            var sd = sdRef.ValueRW;

            // Cooldown
            if (sd.cooldownRemaining > 0)
                sd.cooldownRemaining -= dt;

            // Get battalion state
            if (!em.Exists(sd.battalionEntity) || !em.HasComponent<BattalionData>(sd.battalionEntity))
                continue;
            var batData = em.GetComponentData<BattalionData>(sd.battalionEntity);

            if (batData.state == BattalionState.InCombat)
            {
                float3 worldPos = ltxRef.ValueRO.Position;
                worldPos.y = 0;

                // Check if current target is still valid
                bool targetValid = sd.currentTarget != Entity.Null &&
                    em.Exists(sd.currentTarget) &&
                    em.HasComponent<HealthData>(sd.currentTarget) &&
                    em.GetComponentData<HealthData>(sd.currentTarget).currentHP > 0;

                if (!targetValid)
                {
                    sd.currentTarget = FindClosestEnemy(
                        worldPos, batData.owner, sd.attackRange * 3f,
                        allEntities, allSd, allLtw, allHp,
                        castleEntities, castleLtw, castleHp, castleOwner
                    );
                }

                // Attack if ready
                if (sd.currentTarget != Entity.Null && sd.attackState == 0 && sd.cooldownRemaining <= 0)
                {
                    float3 targetPos = float3.zero;
                    bool foundTargetPos = false;

                    for (int i = 0; i < allEntities.Length; i++)
                    {
                        if (allEntities[i] == sd.currentTarget)
                        {
                            targetPos = allLtw[i].Position;
                            foundTargetPos = true;
                            break;
                        }
                    }
                    if (!foundTargetPos)
                    {
                        for (int i = 0; i < castleEntities.Length; i++)
                        {
                            if (castleEntities[i] == sd.currentTarget)
                            {
                                targetPos = castleLtw[i].Position;
                                foundTargetPos = true;
                                break;
                            }
                        }
                    }

                    if (foundTargetPos)
                    {
                        float dist = math.distance(worldPos, targetPos);
                        if (dist < sd.attackRange * 1.5f)
                        {
                            sd.attackState = 1;
                            sd.attackOrigin = worldPos;
                            sd.attackTarget = targetPos;
                            sd.attackTarget.y = 0;
                            sd.attackT = 0;
                            float dashDist = math.distance(sd.attackOrigin, sd.attackTarget);
                            sd.dashTotalTime = math.max(0.08f, dashDist / sd.dashSpeed);
                            sd.cooldownRemaining = sd.attackCooldown;
                        }
                    }
                }
            }

            // Process dash animation
            if (sd.attackState != 0)
            {
                ProcessDash(ref sd, ref ltxRef.ValueRW, dt, em);
            }

            sdRef.ValueRW = sd;
        }

        allEntities.Dispose();
        allSd.Dispose();
        allLtw.Dispose();
        allHp.Dispose();
        castleEntities.Dispose();
        castleLtw.Dispose();
        castleHp.Dispose();
        castleOwner.Dispose();
    }

    Entity FindClosestEnemy(
        float3 worldPos, BattalionOwner owner, float searchRadius,
        NativeList<Entity> entities, NativeList<SoldierData> sds,
        NativeList<LocalToWorld> ltws, NativeList<HealthData> hps,
        NativeList<Entity> castleEntities, NativeList<LocalToWorld> castleLtws,
        NativeList<HealthData> castleHps, NativeList<BattalionOwner> castleOwners)
    {
        Entity closest = Entity.Null;
        float minDistSq = searchRadius * searchRadius;

        // Search enemy soldiers
        for (int i = 0; i < entities.Length; i++)
        {
            if (hps[i].currentHP <= 0) continue;
            if (!EntityManager.Exists(sds[i].battalionEntity) ||
                !EntityManager.HasComponent<BattalionData>(sds[i].battalionEntity))
                continue;
            var otherBat = EntityManager.GetComponentData<BattalionData>(sds[i].battalionEntity);
            if (otherBat.owner == owner) continue;

            float d = math.distancesq(worldPos, ltws[i].Position);
            if (d < minDistSq)
            {
                minDistSq = d;
                closest = entities[i];
            }
        }

        // Search enemy castles if no close soldier
        if (closest == Entity.Null)
        {
            for (int i = 0; i < castleEntities.Length; i++)
            {
                if (castleHps[i].currentHP <= 0) continue;
                if (castleOwners[i] == owner) continue;

                float d = math.distancesq(worldPos, castleLtws[i].Position);
                if (d < minDistSq)
                {
                    minDistSq = d;
                    closest = castleEntities[i];
                }
            }
        }

        return closest;
    }

    void ProcessDash(ref SoldierData sd, ref LocalTransform ltx, float dt, EntityManager em)
    {
        if (sd.attackState == 1) // Forward dash
        {
            sd.attackT += dt / sd.dashTotalTime;
            if (sd.attackT >= 1f)
            {
                ApplyDamageToTarget(sd.currentTarget, 5, em);
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
        else if (sd.attackState == 2) // Return dash
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

    void ApplyDamageToTarget(Entity target, int damage, EntityManager em)
    {
        if (!em.Exists(target) || !em.HasComponent<HealthData>(target)) return;
        var hp = em.GetComponentData<HealthData>(target);
        hp.currentHP -= damage;
        if (hp.currentHP < 0) hp.currentHP = 0;
        em.SetComponentData(target, hp);
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
