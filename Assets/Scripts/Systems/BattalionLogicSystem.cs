using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionInputSystem))]
[UpdateBefore(typeof(SoldierSystem))]
public partial struct BattalionLogicSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCommandData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var cmd = SystemAPI.GetSingletonRW<PlayerCommandData>();

        foreach (var (bat, transform, pathBuf, entity) in
            SystemAPI.Query<RefRW<BattalionData>, RefRW<LocalTransform>, DynamicBuffer<BattalionPathPoint>>()
            .WithEntityAccess())
        {
            var b = bat.ValueRW;

            // Process pending command
            if (cmd.ValueRW.pending && cmd.ValueRW.selectedBattalion == entity)
            {
                b.targetCell = cmd.ValueRW.targetCell;
                b.commandType = cmd.ValueRW.commandType;
                BuildPath(ref state, transform.ValueRO.Position, b.targetCell, pathBuf);
                b.pathIndex = 0;
                b.state = BattalionState.Moving;
                cmd.ValueRW.pending = false;
            }

            switch (b.state)
            {
                case BattalionState.Idle:
                    if (EnemyInRange(transform.ValueRO.Position, b.owner, b.detectionRange, ref state))
                    { b.commandType = CommandType.Attack; b.state = BattalionState.Attacking; }
                    break;

                case BattalionState.Moving:
                    MoveTick(ref b, ref transform.ValueRW, pathBuf, dt, ref state);
                    break;

                case BattalionState.Mining:
                    if (!MineInRange(transform.ValueRO.Position, b.detectionRange))
                    { BuildPath(ref state, transform.ValueRO.Position, b.targetCell, pathBuf); b.pathIndex = 0; b.state = BattalionState.Moving; }
                    break;

                case BattalionState.Attacking:
                    if (!EnemyInRange(transform.ValueRO.Position, b.owner, b.detectionRange, ref state))
                    { BuildPath(ref state, transform.ValueRO.Position, b.targetCell, pathBuf); b.pathIndex = 0; b.state = BattalionState.Moving; }
                    break;
            }

            bat.ValueRW = b;
        }
    }

    void MoveTick(ref BattalionData b, ref LocalTransform t, DynamicBuffer<BattalionPathPoint> path, float dt, ref SystemState state)
    {
        float3 flat = t.Position; flat.y = 0;

        if (b.commandType == CommandType.Mine && MineInRange(flat, b.detectionRange))
        { b.state = BattalionState.Mining; return; }
        if (b.commandType == CommandType.Attack && EnemyInRange(flat, b.owner, b.detectionRange, ref state))
        { b.state = BattalionState.Attacking; return; }

        if (b.pathIndex >= path.Length) { b.state = BattalionState.Idle; return; }

        float3 target = path[b.pathIndex].position;
        float3 dir = target - flat;
        float dist = math.length(dir);
        float step = b.moveSpeed * dt;

        if (step >= dist)
        {
            flat = target;
            b.pathIndex++;
            b.bobPhase += dist * b.bobFrequency;
        }
        else
        {
            flat += math.normalize(dir) * step;
            b.bobPhase += step * b.bobFrequency;
        }

        float yBob = math.abs(math.sin(b.bobPhase)) * b.bobHeight;
        t.Position = new float3(flat.x, yBob, flat.z);
    }

    void BuildPath(ref SystemState s, float3 from, float3 to, DynamicBuffer<BattalionPathPoint> buf)
    {
        buf.Clear();
        var navPath = new NavMeshPath();
        var f = new Vector3(from.x, 0, from.z);
        var t = new Vector3(to.x, 0, to.z);
        if (NavMesh.CalculatePath(f, t, NavMesh.AllAreas, navPath) && navPath.status == NavMeshPathStatus.PathComplete)
        {
            for (int i = 0; i < navPath.corners.Length; i++)
                buf.Add(new BattalionPathPoint { position = new float3(navPath.corners[i].x, 0, navPath.corners[i].z) });
        }
        if (buf.Length == 0 || !buf[buf.Length - 1].position.Equals(t))
            buf.Add(new BattalionPathPoint { position = t });
    }

    bool MineInRange(float3 pos, float range)
    {
        var hits = Physics.OverlapSphere(new Vector3(pos.x, 0, pos.z), range);
        foreach (var h in hits) if (h.name.StartsWith("GoldMine")) return true;
        return false;
    }

    bool EnemyInRange(float3 pos, BattalionOwner owner, float range, ref SystemState s)
    {
        var hits = Physics.OverlapSphere(new Vector3(pos.x, 0, pos.z), range);
        foreach (var h in hits)
        {
            int id = h.gameObject.GetInstanceID();
            foreach (var (link, parent) in SystemAPI.Query<RefRO<EntityLink>, RefRO<Parent>>())
            {
                if (link.ValueRO.goInstanceID == id)
                {
                    var bat = SystemAPI.GetComponent<BattalionData>(parent.ValueRO.Value);
                    if (bat.owner != owner) return true;
                }
            }
        }
        return false;
    }
}
