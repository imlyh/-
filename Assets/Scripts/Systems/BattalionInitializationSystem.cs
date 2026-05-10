using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class BattalionInitializationSystem : SystemBase
{
    protected override void OnCreate()
    {
        EntityManager.CreateSingleton<PlayerCommandData>();
    }

    protected override void OnUpdate()
    {
        Enabled = false;
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        Object.DestroyImmediate(temp);

        int layer = LayerMask.NameToLayer("Unit");
        CreateBattalion(new float3(4,0,10), BattalionOwner.Player, "PBN_A", cubeMesh, mat, layer);
        CreateBattalion(new float3(4,0,7),  BattalionOwner.Player, "PBN_B", cubeMesh, mat, layer);
        CreateBattalion(new float3(25,0,10), BattalionOwner.Enemy, "EBN_A", cubeMesh, mat, layer);
        CreateBattalion(new float3(25,0,13), BattalionOwner.Enemy, "EBN_B", cubeMesh, mat, layer);
        Debug.Log("[ECS] Battalions created");
    }

    void CreateBattalion(float3 pos, BattalionOwner owner, string name, Mesh mesh, Material mat, int layer)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var e = ecb.CreateEntity();
        ecb.SetName(e, name);
        ecb.AddComponent(e, LocalTransform.FromPosition(pos));
        ecb.AddComponent(e, new BattalionData
        {
            owner=owner, state=BattalionState.Idle, moveSpeed=4f,
            detectionRange=1.3f, bobHeight=0.2f, bobFrequency=8f,
            bobPhase=UnityEngine.Random.Range(0f,100f)
        });
        ecb.AddBuffer<BattalionPathPoint>(e);
        var children = ecb.AddBuffer<Child>(e);

        float s = 0.55f;
        var off = new float3[]{ new(-s/2,0,-s/2),new(s/2,0,-s/2),new(-s/2,0,s/2),new(s/2,0,s/2) };
        for (int i = 0; i < 4; i++)
        {
            var se = ecb.CreateEntity();
            ecb.SetName(se, $"{name}_S{i}");
            ecb.AddComponent(se, new Parent{Value=e});
            ecb.AddComponent(se, LocalTransform.FromPosition(off[i]));
            ecb.AddComponent(se, new SoldierData{
                attackRange=1.5f,attackCooldown=1.5f,dashSpeed=10f,dashHeight=0.25f,formationOffset=off[i]
            });
            children.Add(new Child{Value=se});

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{name}_S{i}_GO";
            go.transform.localScale = Vector3.one * 0.35f;
            go.layer = layer;
            go.tag = owner == BattalionOwner.Player ? "PlayerUnit" : "EnemyUnit";
            go.GetComponent<MeshRenderer>().material = mat;
            go.hideFlags = HideFlags.HideAndDontSave;
            ecb.AddComponent(se, new EntityLink{goInstanceID=go.GetInstanceID()});
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
