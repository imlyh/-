using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionLogicSystem))]
public partial struct SoldierSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (sdRef, ltwRef, ltxRef, parentRef) in
            SystemAPI.Query<RefRW<SoldierData>, RefRO<LocalToWorld>, RefRW<LocalTransform>, RefRO<Parent>>())
        {
            var sd = sdRef.ValueRW;
            if (sd.cooldownRemaining > 0) sd.cooldownRemaining -= dt;

            var batEntity = parentRef.ValueRO.Value;
            var batData = SystemAPI.GetComponent<BattalionData>(batEntity);
            var batLTW = SystemAPI.GetComponent<LocalToWorld>(batEntity);
            var invBat = math.inverse(batLTW.Value);
            float3 worldPos = ltwRef.ValueRO.Position;

            switch (sd.actionState)
            {
                case SoldierActionState.Idle:
                    ltxRef.ValueRW.Position = sd.formationOffset;
                    CheckForTarget(ref sd, worldPos, batData, ref state);
                    break;

                case SoldierActionState.AttackingForward:
                    sd.attackT += dt / sd.dashTotalTime;
                    if (sd.attackT >= 1f)
                    {
                        sd.actionState = SoldierActionState.AttackingBack;
                        sd.attackT = 0;
                    }
                    else
                    {
                        float t = math.clamp(sd.attackT, 0, 1);
                        float3 wPos = math.lerp(sd.attackOrigin, sd.attackTarget, EaseOut(t));
                        wPos.y += sd.dashHeight * math.sin(t * math.PI);
                        ltxRef.ValueRW.Position = math.transform(invBat, wPos);
                    }
                    break;

                case SoldierActionState.AttackingBack:
                    sd.attackT += dt / sd.dashTotalTime;
                    if (sd.attackT >= 1f)
                    {
                        ltxRef.ValueRW.Position = sd.formationOffset;
                        sd.actionState = SoldierActionState.Idle;
                    }
                    else
                    {
                        float t = math.clamp(sd.attackT, 0, 1);
                        float3 wPos = math.lerp(sd.attackTarget, sd.attackOrigin, EaseOut(t));
                        wPos.y += sd.dashHeight * math.sin(t * math.PI);
                        ltxRef.ValueRW.Position = math.transform(invBat, wPos);
                    }
                    break;
            }

            sdRef.ValueRW = sd;
        }
    }

    void CheckForTarget(ref SoldierData sd, float3 worldPos, BattalionData batData, ref SystemState state)
    {
        if (sd.cooldownRemaining > 0) return;

        if (batData.state == BattalionState.Attacking)
        {
            var hits = Physics.OverlapSphere(new Vector3(worldPos.x, 0, worldPos.z), sd.attackRange);
            float closestDist = float.MaxValue;
            Vector3 closestPos = Vector3.zero;

            foreach (var h in hits)
            {
                var otherBat = GetBattalionOwner(h.gameObject, ref state);
                if (otherBat.HasValue && otherBat.Value != batData.owner)
                {
                    float d = Vector3.Distance(new Vector3(worldPos.x, 0, worldPos.z), h.transform.position);
                    if (d < closestDist) { closestDist = d; closestPos = h.transform.position; }
                }
            }
            if (closestDist < float.MaxValue)
                StartDash(ref sd, worldPos, closestPos);
        }
        else if (batData.state == BattalionState.Mining)
        {
            var hits = Physics.OverlapSphere(new Vector3(worldPos.x, 0, worldPos.z), sd.attackRange);
            foreach (var h in hits)
            {
                if (!h.name.StartsWith("GoldMine")) continue;
                Vector3 target = h.transform.position;
                target += h.transform.right * 0.4f * (sd.formationOffset.x > 0 ? 1 : -1);
                target += h.transform.forward * 0.4f * (sd.formationOffset.z > 0 ? 1 : -1);
                StartDash(ref sd, worldPos, target);
                break;
            }
        }
    }

    void StartDash(ref SoldierData sd, float3 origin, Vector3 target)
    {
        sd.actionState = SoldierActionState.AttackingForward;
        sd.attackOrigin = new float3(origin.x, 0, origin.z);
        sd.attackTarget = new float3(target.x, 0, target.z);
        sd.attackT = 0;
        sd.cooldownRemaining = sd.attackCooldown;
        float dist = math.distance(sd.attackOrigin, sd.attackTarget);
        sd.dashTotalTime = math.max(0.08f, dist / sd.dashSpeed);
    }

    BattalionOwner? GetBattalionOwner(GameObject go, ref SystemState stateRef)
    {
        int id = go.GetInstanceID();
        foreach (var (link, parent) in SystemAPI.Query<RefRO<EntityLink>, RefRO<Parent>>())
        {
            if (link.ValueRO.goInstanceID == id)
            {
                if (SystemAPI.HasComponent<BattalionData>(parent.ValueRO.Value))
                    return SystemAPI.GetComponent<BattalionData>(parent.ValueRO.Value).owner;
            }
        }
        return null;
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
