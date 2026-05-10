using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct BattalionPresentationSystem : ISystem
{
    private Dictionary<int, GameObject> goMap;

    public void OnCreate(ref SystemState state)
    {
        goMap = new Dictionary<int, GameObject>();
        state.RequireForUpdate<PlayerCommandData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var cmd = SystemAPI.GetSingleton<PlayerCommandData>();

        // Build GO map lazily
        foreach (var (link, ltw, entity) in
            SystemAPI.Query<RefRO<EntityLink>, RefRO<LocalToWorld>>().WithEntityAccess())
        {
            int id = link.ValueRO.goInstanceID;
            if (!goMap.TryGetValue(id, out var go))
            {
                go = FindGO(id);
                if (go == null) continue;
                goMap[id] = go;
            }

            // Sync transform: ECS world position → GameObject world position
            var pos = ltw.ValueRO.Position;
            go.transform.position = new Vector3(pos.x, pos.y, pos.z);
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        foreach (var kv in goMap) Object.Destroy(kv.Value);
        goMap.Clear();
    }

    GameObject FindGO(int instanceID)
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var g in all)
            if (g.GetInstanceID() == instanceID) return g;
        return null;
    }
}
