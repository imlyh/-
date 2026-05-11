
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class BattalionPresentationSystem : SystemBase
{
    protected override void OnCreate() { RequireForUpdate<PlayerCommandData>(); }

    protected override void OnUpdate()
    {
        var map = BattalionInitializationSystem.GOMap;
        foreach (var (sdRef, ltxRef, linkRef) in SystemAPI.Query<RefRO<SoldierData>, RefRO<LocalTransform>, RefRO<EntityLink>>())
        {
            if (!map.TryGetValue(linkRef.ValueRO.goInstanceID, out var go)) continue;
            var batLTW = SystemAPI.GetComponent<LocalToWorld>(sdRef.ValueRO.battalionEntity);
            go.transform.position = (Vector3)batLTW.Position + (Vector3)ltxRef.ValueRO.Position;
        }
    }
}
