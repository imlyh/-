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
        foreach (var (sdRef, linkRef) in SystemAPI.Query<RefRO<SoldierData>, RefRO<EntityLink>>())
        {
            var sd = sdRef.ValueRO;
            if (!map.TryGetValue(linkRef.ValueRO.goInstanceID, out var go)) continue;
            var batLTW = SystemAPI.GetComponent<LocalToWorld>(sd.battalionEntity);
            go.transform.position = (Vector3)batLTW.Position + (Vector3)sd.formationOffset;
        }
    }
}
