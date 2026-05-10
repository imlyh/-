using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionInputSystem))]
[UpdateBefore(typeof(SoldierSystem))]
public partial class BattalionLogicSystem : SystemBase
{
    protected override void OnCreate() { RequireForUpdate<PlayerCommandData>(); }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var cmd = SystemAPI.GetSingletonRW<PlayerCommandData>();

        foreach (var (bRef, txRef, pathBuf, entity) in
            SystemAPI.Query<RefRW<BattalionData>, RefRW<LocalTransform>, DynamicBuffer<BattalionPathPoint>>()
            .WithEntityAccess())
        {
            var b = bRef.ValueRW;
            var tx = txRef.ValueRW;

            if (cmd.ValueRW.pending && cmd.ValueRW.selectedBattalion == entity)
            {
                b.targetCell = cmd.ValueRW.targetCell;
                b.commandType = cmd.ValueRW.commandType;
                BuildPath(tx.Position, b.targetCell, pathBuf);
                b.pathIndex = 0;
                b.state = BattalionState.Moving;
                cmd.ValueRW.pending = false;
            }

            float3 flat = tx.Position; flat.y = 0;

            switch (b.state)
            {
                case BattalionState.Idle:
                    if (EnemyInRange(flat, b.owner, b.detectionRange))
                    { b.commandType = CommandType.Attack; b.state = BattalionState.Attacking; }
                    break;
                case BattalionState.Moving:
                    MoveTick(ref b, ref tx, pathBuf, dt, flat);
                    break;
                case BattalionState.Mining:
                    if (!MineInRange(flat, b.detectionRange))
                    { BuildPath(tx.Position, b.targetCell, pathBuf); b.pathIndex = 0; b.state = BattalionState.Moving; }
                    break;
                case BattalionState.Attacking:
                    if (!EnemyInRange(flat, b.owner, b.detectionRange))
                    { BuildPath(tx.Position, b.targetCell, pathBuf); b.pathIndex = 0; b.state = BattalionState.Moving; }
                    break;
            }

            bRef.ValueRW = b;
            txRef.ValueRW = tx;
        }
    }

    void MoveTick(ref BattalionData b, ref LocalTransform t, DynamicBuffer<BattalionPathPoint> path, float dt, float3 flat)
    {
        if (b.commandType == CommandType.Mine && MineInRange(flat, b.detectionRange))
        { b.state = BattalionState.Mining; return; }
        if (b.commandType == CommandType.Attack && EnemyInRange(flat, b.owner, b.detectionRange))
        { b.state = BattalionState.Attacking; return; }
        if (b.pathIndex >= path.Length) { b.state = BattalionState.Idle; return; }

        float3 target = path[b.pathIndex].position;
        float3 dir = target - flat;
        float dist = math.length(dir);
        float step = b.moveSpeed * dt;

        if (step >= dist) { flat = target; b.pathIndex++; b.bobPhase += dist * b.bobFrequency; }
        else { flat += math.normalize(dir) * step; b.bobPhase += step * b.bobFrequency; }

        t.Position = new float3(flat.x, math.abs(math.sin(b.bobPhase)) * b.bobHeight, flat.z);
    }

    void BuildPath(float3 from, float3 to, DynamicBuffer<BattalionPathPoint> buf)
    {
        buf.Clear();
        var navPath = new NavMeshPath();
        var f = new Vector3(from.x, 0, from.z); var t = new Vector3(to.x, 0, to.z);
        if (NavMesh.CalculatePath(f, t, NavMesh.AllAreas, navPath) && navPath.status == NavMeshPathStatus.PathComplete)
            for (int i = 0; i < navPath.corners.Length; i++)
                buf.Add(new BattalionPathPoint { position = new float3(navPath.corners[i].x, 0, navPath.corners[i].z) });
        if (buf.Length == 0 || math.distance(buf[buf.Length - 1].position, to) > 0.01f)
            buf.Add(new BattalionPathPoint { position = to });
    }

    bool MineInRange(float3 pos, float range)
    {
        foreach (var h in Physics.OverlapSphere(new Vector3(pos.x, 0, pos.z), range))
            if (h.name.StartsWith("GoldMine")) return true;
        return false;
    }

    bool EnemyInRange(float3 pos, BattalionOwner owner, float range)
    {
        foreach (var h in Physics.OverlapSphere(new Vector3(pos.x, 0, pos.z), range))
        {
            int id = h.gameObject.GetInstanceID();
            foreach (var (link, parent) in SystemAPI.Query<RefRO<EntityLink>, RefRO<Parent>>())
                if (link.ValueRO.goInstanceID == id)
                    return EntityManager.GetComponentData<BattalionData>(parent.ValueRO.Value).owner != owner;
        }
        return false;
    }
}
