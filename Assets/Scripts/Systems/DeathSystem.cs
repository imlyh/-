using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SoldierSystem))]
public partial class DeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var em = EntityManager;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (hpRef, linkRef, ltxRef, entity) in
            SystemAPI.Query<RefRO<HealthData>, RefRO<EntityLink>, RefRW<LocalTransform>>()
            .WithEntityAccess())
        {
            if (hpRef.ValueRO.currentHP > 0) continue;

            // Shrink
            var s = ltxRef.ValueRW.Scale;
            s -= SystemAPI.Time.DeltaTime * 2f;
            if (s <= 0.05f)
            {
                // Destroy GO
                int id = linkRef.ValueRO.goInstanceID;
                if (BattalionInitializationSystem.GOMap.TryGetValue(id, out var go))
                {
                    Object.Destroy(go);
                    BattalionInitializationSystem.GOMap.Remove(id);
                }
                ecb.DestroyEntity(entity);
            }
            else
            {
                ltxRef.ValueRW.Scale = s;
            }
        }

        ecb.Playback(em);
        ecb.Dispose();
    }
}
