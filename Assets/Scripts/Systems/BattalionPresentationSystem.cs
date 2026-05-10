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
        Entities.WithoutBurst().ForEach((in EntityLink link, in LocalToWorld ltw) =>
        {
            if (map.TryGetValue(link.goInstanceID, out var go))
                go.transform.position = ltw.Position;
        }).Run();
    }
}
