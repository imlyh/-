
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BoidTargetSystem))]
[UpdateBefore(typeof(CombatSystem))]
public partial class BoidMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        float cohesionW = 1.5f, separationW = 3.0f, alignmentW = 1.0f, targetW = 2.5f;

        var em = EntityManager;

        // Collect all soldiers into arrays
        var queryDesc = new EntityQueryDesc { All = new[] { ComponentType.ReadWrite<SoldierData>(), ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<LocalToWorld>() } };
        var q = em.CreateEntityQuery(queryDesc);
        var entities = q.ToEntityArray(Allocator.TempJob);
        var sds = q.ToComponentDataArray<SoldierData>(Allocator.TempJob);
        var ltws = q.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        var ltxs = q.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        for (int i = 0; i < entities.Length; i++)
        {
            var sd = sds[i];
            if (sd.attackState != 0) continue;

            float3 pos = ltws[i].Position;
            float3 vel = sd.velocity;
            Entity batEnt = sd.battalionEntity;

            float3 cohesion = float3.zero;
            float3 separation = float3.zero;
            float3 alignment = float3.zero;
            int count = 0;

            for (int j = 0; j < entities.Length; j++)
            {
                if (i == j) continue;
                if (sds[j].battalionEntity != batEnt) continue;
                float3 diff = ltws[j].Position - pos;
                float dist = math.length(diff);
                if (dist < sd.neighborRadius)
                {
                    cohesion += ltws[j].Position;
                    alignment += sds[j].velocity;
                    count++;
                    if (dist < sd.separationRadius && dist > 0.01f)
                        separation -= math.normalize(diff) / dist;
                    else if (dist < 0.01f && i > j)
                        // Same position: add random push
                        separation += new float3(UnityEngine.Random.Range(-1f,1f), 0, UnityEngine.Random.Range(-1f,1f));
                }
            }

            if (count > 0)
            {
                cohesion = cohesion / count - pos;
                alignment = alignment / count - vel;
            }

            float3 toTarget = sd.targetPosition - pos;
            toTarget.y = 0;
            float3 targetForce = math.normalizesafe(toTarget) * targetW;

            float3 force = cohesion * cohesionW + separation * separationW +
                          alignment * alignmentW + targetForce;
            force.y = 0;

            float fMag = math.length(force);
            if (fMag > sd.maxForce) force = math.normalize(force) * sd.maxForce;

            vel += force * dt;
            float vMag = math.length(vel);
            if (vMag > sd.maxSpeed) vel = math.normalize(vel) * sd.maxSpeed;
            if (vMag < 0.1f && count > 0) vel = math.normalizesafe(cohesion) * 0.5f;

            sd.velocity = vel;

            float3 newPos = pos + vel * dt;
            float bob = math.sin(newPos.x * 4f + newPos.z * 4f + (float)SystemAPI.Time.ElapsedTime * 5f) * 0.12f;
            newPos.y = math.max(0, bob);

            sds[i] = sd;
            var ltx = ltxs[i];
            ltx.Position = newPos;
            ltxs[i] = ltx;
        }

        // Write back
        for (int i = 0; i < entities.Length; i++)
        {
            em.SetComponentData(entities[i], sds[i]);
            em.SetComponentData(entities[i], ltxs[i]);
        }

        entities.Dispose(); sds.Dispose(); ltws.Dispose(); ltxs.Dispose();
    }
}
