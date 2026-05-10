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

        Entities.WithoutBurst().ForEach((
            ref SoldierData sd, ref LocalTransform ltx,
            in LocalToWorld ltw, in Parent parent) =>
        {
            if (sd.cooldownRemaining > 0) sd.cooldownRemaining -= dt;

            var batEntity = parent.Value;
            var batData = EntityManager.GetComponentData<BattalionData>(batEntity);
            var invBat = math.inverse(EntityManager.GetComponentData<LocalToWorld>(batEntity).Value);
            float3 worldPos = ltw.Position;

            switch (sd.actionState)
            {
                case SoldierActionState.Idle:
                    ltx.Position = sd.formationOffset;
                    CheckForTarget(ref sd, worldPos, batData);
                    break;

                case SoldierActionState.AttackingForward:
                    sd.attackT += dt / sd.dashTotalTime;
                    if (sd.attackT >= 1f) { sd.actionState = SoldierActionState.AttackingBack; sd.attackT = 0; }
                    else
                    {
                        float t = math.clamp(sd.attackT, 0, 1);
                        float3 wp = math.lerp(sd.attackOrigin, sd.attackTarget, EaseOut(t));
                        wp.y += sd.dashHeight * math.sin(t * math.PI);
                        ltx.Position = math.transform(invBat, wp);
                    }
                    break;

                case SoldierActionState.AttackingBack:
                    sd.attackT += dt / sd.dashTotalTime;
                    if (sd.attackT >= 1f) { ltx.Position = sd.formationOffset; sd.actionState = SoldierActionState.Idle; }
                    else
                    {
                        float t = math.clamp(sd.attackT, 0, 1);
                        float3 wp = math.lerp(sd.attackTarget, sd.attackOrigin, EaseOut(t));
                        wp.y += sd.dashHeight * math.sin(t * math.PI);
                        ltx.Position = math.transform(invBat, wp);
                    }
                    break;
            }
        }).Run();
    }

    void CheckForTarget(ref SoldierData sd, float3 worldPos, BattalionData batData)
    {
        if (sd.cooldownRemaining > 0) return;

        if (batData.state == BattalionState.Attacking)
        {
            var hits = Physics.OverlapSphere(new Vector3(worldPos.x, 0, worldPos.z), sd.attackRange);
            float closestDist = float.MaxValue; Vector3 closestPos = Vector3.zero;
            foreach (var h in hits)
            {
                var owner = GetBattalionOwner(h.gameObject);
                if (owner.HasValue && owner.Value != batData.owner)
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

    BattalionOwner? GetBattalionOwner(GameObject go)
    {
        int id = go.GetInstanceID();
        foreach (var (link, p) in SystemAPI.Query<RefRO<EntityLink>, RefRO<Parent>>())
            if (link.ValueRO.goInstanceID == id && EntityManager.HasComponent<BattalionData>(p.ValueRO.Value))
                return EntityManager.GetComponentData<BattalionData>(p.ValueRO.Value).owner;
        return null;
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
