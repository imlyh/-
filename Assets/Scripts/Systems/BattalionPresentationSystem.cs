using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class BattalionPresentationSystem : SystemBase
{
    private Dictionary<int, GameObject> goMap = new();

    protected override void OnCreate() { RequireForUpdate<PlayerCommandData>(); }

    protected override void OnUpdate()
    {
        Entities.WithoutBurst().ForEach((in EntityLink link, in LocalToWorld ltw) =>
        {
            int id = link.goInstanceID;
            if (!goMap.TryGetValue(id, out var go))
            {
                go = FindGO(id);
                if (go == null) return;
                goMap[id] = go;
            }
            var p = ltw.Position;
            go.transform.position = new Vector3(p.x, p.y, p.z);
        }).Run();
    }

    protected override void OnDestroy()
    {
        foreach (var kv in goMap) if (kv.Value) Object.Destroy(kv.Value);
        goMap.Clear();
    }

    GameObject FindGO(int instanceID)
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var g in all) if (g.GetInstanceID() == instanceID) return g;
        return null;
    }
}
