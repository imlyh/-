using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class BattalionPresentationSystem : SystemBase
{
    private bool logged;

    protected override void OnCreate() { RequireForUpdate<PlayerCommandData>(); }

    protected override void OnUpdate()
    {
        var map = BattalionInitializationSystem.GOMap;
        int count = 0;
        Entities.WithoutBurst().ForEach((in EntityLink link, in LocalToWorld ltw) =>
        {
            count++;
            if (map.TryGetValue(link.goInstanceID, out var go))
                go.transform.position = ltw.Position;
        }).Run();

        if (!logged && count > 0)
        {
            logged = true;
            Debug.Log($"[PRES] Synced {count} entities, GOMap has {map.Count} entries");
        }
    }
}
