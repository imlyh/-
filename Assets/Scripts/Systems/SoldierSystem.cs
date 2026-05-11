using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionLogicSystem))]
public partial class SoldierSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (sdRef, ltxRef) in SystemAPI.Query<RefRW<SoldierData>, RefRW<LocalTransform>>())
        {
            var sd = sdRef.ValueRW;
            if (sd.cooldownRemaining > 0) sd.cooldownRemaining -= dt;

            var batLTW = SystemAPI.GetComponent<LocalToWorld>(sd.battalionEntity);
            var batData = SystemAPI.GetComponent<BattalionData>(sd.battalionEntity);
            var invBat = math.inverse(batLTW.Value);
            float3 worldPos = batLTW.Position + sd.formationOffset;

            switch (sd.actionState)
            {
                case SoldierActionState.Idle:
                    ltxRef.ValueRW.Position = sd.formationOffset;
                    CheckForTarget(ref sd, worldPos, batData);
                    break;

                case SoldierActionState.AttackingForward:
                    sd.attackT += dt / sd.dashTotalTime;
                    if (sd.attackT >= 1f)
                    {
                        sd.actionState = SoldierActionState.AttackingBack; sd.attackT = 0;
                        DealDamage(sd.attackTarget);
                    }
                    else
                    {
                        float t = math.clamp(sd.attackT, 0, 1);
                        float3 wp = math.lerp(sd.attackOrigin, sd.attackTarget, EaseOut(t));
                        wp.y += sd.dashHeight * math.sin(t * math.PI);
                        ltxRef.ValueRW.Position = math.transform(invBat, wp);
                    }
                    break;

                case SoldierActionState.AttackingBack:
                    sd.attackT += dt / sd.dashTotalTime;
                    if (sd.attackT >= 1f)
                    {
                        ltxRef.ValueRW.Position = sd.formationOffset;
                        sd.actionState = SoldierActionState.Idle;
                        if (batData.state == BattalionState.Mining && SystemAPI.TryGetSingletonRW<PlayerGoldData>(out var gold))
                        { gold.ValueRW.gold += 1; }
                    }
                    else
                    {
                        float t = math.clamp(sd.attackT, 0, 1);
                        float3 wp = math.lerp(sd.attackTarget, sd.attackOrigin, EaseOut(t));
                        wp.y += sd.dashHeight * math.sin(t * math.PI);
                        ltxRef.ValueRW.Position = math.transform(invBat, wp);
                    }
                    break;
            }

            sdRef.ValueRW = sd;
        }
    }

    void CheckForTarget(ref SoldierData sd, float3 worldPos, BattalionData batData)
    {
        if (sd.cooldownRemaining > 0) return;

        if (batData.state == BattalionState.Attacking)
        {
            var hits = Physics.OverlapSphere((Vector3)worldPos, sd.attackRange);
            float closestDist = float.MaxValue; Vector3 closestPos = Vector3.zero;
            foreach (var h in hits)
            {
                bool valid = false;
                // Enemy soldier
                var owner = GetBattalionOwner(h.gameObject);
                if (owner.HasValue && owner.Value != batData.owner) valid = true;
                // Castle (check if GO name contains "Castle" and belongs to enemy)
                if (h.name.Contains("Castle"))
                {
                    var castleOwner = GetGOOwner(h.gameObject);
                    if (castleOwner.HasValue && castleOwner.Value != batData.owner) valid = true;
                }
                if (valid)
                {
                    float d = Vector3.Distance((Vector3)worldPos, h.transform.position);
                    if (d < closestDist) { closestDist = d; closestPos = h.transform.position; }
                }
            }
            if (closestDist < float.MaxValue) StartDash(ref sd, worldPos, closestPos);
        }
        else if (batData.state == BattalionState.Mining)
        {
            var hits = Physics.OverlapSphere((Vector3)worldPos, sd.attackRange);
            foreach (var h in hits)
            {
                if (!h.name.StartsWith("GoldMine")) continue;
                Vector3 t = h.transform.position;
                t += h.transform.right * 0.4f * (sd.formationOffset.x > 0 ? 1 : -1);
                t += h.transform.forward * 0.4f * (sd.formationOffset.z > 0 ? 1 : -1);
                StartDash(ref sd, worldPos, t);
                break;
            }
        }
    }

    void StartDash(ref SoldierData sd, float3 origin, Vector3 target)
    {
        sd.actionState = SoldierActionState.AttackingForward;
        sd.attackOrigin = new float3(origin.x, 0, origin.z);
        sd.attackTarget = new float3(target.x, 0, target.z);
        sd.attackT = 0; sd.cooldownRemaining = sd.attackCooldown;
        sd.dashTotalTime = math.max(0.08f, math.distance(sd.attackOrigin, sd.attackTarget) / sd.dashSpeed);
    }

    BattalionOwner? GetGOOwner(GameObject go)
    {
        int id = go.GetInstanceID();
        foreach (var (link, hp) in SystemAPI.Query<RefRO<EntityLink>, RefRO<HealthData>>())
            if (link.ValueRO.goInstanceID == id)
            {
                if (go.name.Contains("Player")) return BattalionOwner.Player;
                if (go.name.Contains("Enemy")) return BattalionOwner.Enemy;
            }
        return null;
    }

    BattalionOwner? GetBattalionOwner(GameObject go)
    {
        int id = go.GetInstanceID();
        foreach (var (link, sd) in SystemAPI.Query<RefRO<EntityLink>, RefRO<SoldierData>>())
            if (link.ValueRO.goInstanceID == id && EntityManager.HasComponent<BattalionData>(sd.ValueRO.battalionEntity))
                return EntityManager.GetComponentData<BattalionData>(sd.ValueRO.battalionEntity).owner;
        return null;
    }

    void DealDamage(float3 targetPos)
    {
        var hits = Physics.OverlapSphere((Vector3)targetPos, 0.8f);
        foreach (var h in hits)
        {
            int id = h.gameObject.GetInstanceID();
            foreach (var (link, hpRef) in SystemAPI.Query<RefRO<EntityLink>, RefRW<HealthData>>())
            {
                if (link.ValueRO.goInstanceID == id)
                {
                    var hp = hpRef.ValueRW;
                    hp.currentHP -= 5;
                    if (hp.currentHP < 0) hp.currentHP = 0;
                    hpRef.ValueRW = hp;
                    return;
                }
            }
        }
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
