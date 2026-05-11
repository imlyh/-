using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionInputSystem))]
[UpdateAfter(typeof(BattalionStateSystem))]
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

            // Process player command
            if (cmd.ValueRW.pending && cmd.ValueRW.selectedBattalion == entity)
            {
                b.targetCell = cmd.ValueRW.targetCell;
                b.commandType = cmd.ValueRW.commandType;
                BuildPath(tx.Position, b.targetCell, pathBuf);
                b.pathIndex = 0;
                b.targetPosition = b.targetCell;
                b.state = BattalionState.Moving;
                cmd.ValueRW.pending = false;
            }

            float3 flat = tx.Position; flat.y = 0;

            if (b.state == BattalionState.Moving)
            {
                if (pathBuf.Length == 0 || b.pathIndex >= pathBuf.Length)
                    BuildPath(tx.Position, b.targetCell, pathBuf);

                var sd = pathBuf;
                if (b.pathIndex < sd.Length)
                {
                    float3 target = sd[b.pathIndex].position;
                    float3 dir = target - flat;
                    float dist = math.length(dir);
                    float step = b.moveSpeed * dt;

                    if (step >= dist) { flat = target; b.pathIndex++; }
                    else flat += math.normalize(dir) * step;

                    b.targetPosition = flat;
                    tx.Position = new float3(flat.x, 0, flat.z);
                }
            }

            if (b.state == BattalionState.InCombat)
            {
                // Keep targetPosition updated
                if (b.targetEnemy != Entity.Null && EntityManager.Exists(b.targetEnemy)
                    && EntityManager.HasComponent<LocalToWorld>(b.targetEnemy))
                {
                    b.targetPosition = EntityManager.GetComponentData<LocalToWorld>(b.targetEnemy).Position;
                }
            }

            bRef.ValueRW = b;
            txRef.ValueRW = tx;
        }
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
}
