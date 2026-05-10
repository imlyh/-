using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class BattalionInitializationSystem : SystemBase
{
    public static readonly Dictionary<int, GameObject> GOMap = new();

    protected override void OnCreate()
    {
        EntityManager.CreateSingleton<PlayerCommandData>();
    }

    protected override void OnUpdate()
    {
        Enabled = false;
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        Object.DestroyImmediate(temp);
        int layer = LayerMask.NameToLayer("Unit");

        CreateBattalion(new float3(4,0,10), BattalionOwner.Player, "PBN_A", mat, layer);
        CreateBattalion(new float3(4,0,7),  BattalionOwner.Player, "PBN_B", mat, layer);
        CreateBattalion(new float3(25,0,10), BattalionOwner.Enemy, "EBN_A", mat, layer);
        CreateBattalion(new float3(25,0,13), BattalionOwner.Enemy, "EBN_B", mat, layer);
        Debug.Log("[ECS] Battalions created");
    }

    void CreateBattalion(float3 pos, BattalionOwner owner, string name, Material mat, int layer)
    {
        var em = EntityManager;
        var e = em.CreateEntity();
        em.SetName(e, name);
        em.AddComponentData(e, LocalTransform.FromPosition(pos));
        em.AddComponentData(e, new BattalionData
        {
            owner=owner, state=BattalionState.Idle, moveSpeed=4f,
            detectionRange=1.3f, bobHeight=0.2f, bobFrequency=8f,
            bobPhase=UnityEngine.Random.Range(0f,100f)
        });
        em.AddBuffer<BattalionPathPoint>(e);

        float s = 0.55f;
        var off = new float3[]{ new(-s/2,0,-s/2),new(s/2,0,-s/2),new(-s/2,0,s/2),new(s/2,0,s/2) };
        for (int i = 0; i < 4; i++)
        {
            var se = em.CreateEntity();
            em.SetName(se, $"{name}_S{i}");
            em.AddComponentData(se, LocalTransform.FromPosition(off[i]));
            em.AddComponentData(se, new SoldierData{
                battalionEntity=e, attackRange=1.5f,attackCooldown=1.5f,
                dashSpeed=10f,dashHeight=0.25f,formationOffset=off[i]
            });
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{name}_S{i}_GO";
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = new Vector3(pos.x+off[i].x, pos.y, pos.z+off[i].z);
            go.layer = layer;
            go.tag = owner==BattalionOwner.Player?"PlayerUnit":"EnemyUnit";
            go.GetComponent<MeshRenderer>().material = mat;
            GOMap[go.GetInstanceID()] = go;
            em.AddComponentData(se, new EntityLink{goInstanceID=go.GetInstanceID()});
        }
    }
}
