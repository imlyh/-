using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 营移动系统：处理玩家指令和敌方AI指令的路径移动
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionInputSystem))]
[UpdateAfter(typeof(BattalionStateSystem))]
public partial class BattalionLogicSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerCommandData>();
    }

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

            // Process player command for player-owned battalions
            if (b.owner == BattalionOwner.Player &&
                cmd.ValueRW.pending && cmd.ValueRW.selectedBattalion == entity)
            {
                b.targetCell = cmd.ValueRW.targetCell;
                b.commandType = cmd.ValueRW.commandType;
                BuildPath(tx.Position, b.targetCell, pathBuf);
                b.pathIndex = 0;
                b.targetPosition = b.targetCell;
                b.state = BattalionState.Moving;
                cmd.ValueRW.pending = false;
            }

            // --- Movement ---
            if (b.state == BattalionState.Moving)
            {
                float3 flat = tx.Position; flat.y = 0;

                if (pathBuf.Length == 0 || b.pathIndex >= pathBuf.Length)
                {
                    BuildPath(tx.Position, b.targetCell, pathBuf);
                    b.pathIndex = 0;
                }

                if (b.pathIndex < pathBuf.Length)
                {
                    float3 target = pathBuf[b.pathIndex].position;
                    float3 dir = target - flat;
                    float dist = math.length(dir);
                    float step = b.moveSpeed * dt;

                    if (step >= dist)
                    {
                        flat = target;
                        b.pathIndex++;
                    }
                    else if (dist > 0.01f)
                    {
                        flat += math.normalize(dir) * step;
                    }

                    tx.Position = new float3(flat.x, 0, flat.z);
                    b.targetPosition = flat;
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
        var f = new Vector3(from.x, 0, from.z);
        var t = new Vector3(to.x, 0, to.z);
        if (NavMesh.CalculatePath(f, t, NavMesh.AllAreas, navPath) &&
            navPath.status == NavMeshPathStatus.PathComplete)
        {
            for (int i = 0; i < navPath.corners.Length; i++)
                buf.Add(new BattalionPathPoint { position = new float3(navPath.corners[i].x, 0, navPath.corners[i].z) });
        }
        if (buf.Length == 0 || math.distance(buf[buf.Length - 1].position, to) > 0.01f)
            buf.Add(new BattalionPathPoint { position = to });
    }
}
