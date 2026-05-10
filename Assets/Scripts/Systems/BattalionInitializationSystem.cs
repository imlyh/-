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
        var cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        int layer = LayerMask.NameToLayer("Unit");

        CreateBattalion(new float3(4, 0, 10), BattalionOwner.Player, "PlayerBattalion_A", cubeMesh, mat, layer);
        CreateBattalion(new float3(4, 0, 7), BattalionOwner.Player, "PlayerBattalion_B", cubeMesh, mat, layer);
        CreateBattalion(new float3(25, 0, 10), BattalionOwner.Enemy, "EnemyBattalion_A", cubeMesh, mat, layer);
        CreateBattalion(new float3(25, 0, 13), BattalionOwner.Enemy, "EnemyBattalion_B", cubeMesh, mat, layer);
    }

    void CreateBattalion(float3 pos, BattalionOwner owner, string name, Mesh mesh, Material mat, int layer)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var entity = ecb.CreateEntity();
        ecb.SetName(entity, name);
        ecb.AddComponent(entity, new LocalTransform { Position = pos, Scale = 1, Rotation = quaternion.identity });
        ecb.AddComponent(entity, new BattalionData
        {
            owner = owner, state = BattalionState.Idle, moveSpeed = 4f,
            detectionRange = 1.3f, bobHeight = 0.2f, bobFrequency = 8f,
            bobPhase = UnityEngine.Random.Range(0f, 100f)
        });
        ecb.AddBuffer<BattalionPathPoint>(entity);

        float s = 0.55f;
        var offsets = new float3[]{
            new(-s/2,0,-s/2), new(s/2,0,-s/2), new(-s/2,0,s/2), new(s/2,0,s/2) };

        for (int i = 0; i < 4; i++)
        {
            var soldier = ecb.CreateEntity();
            ecb.SetName(soldier, $"{name}_S{i}");
            ecb.AddComponent(soldier, new Parent { Value = entity });
            ecb.AddComponent(soldier, LocalTransform.FromPosition(offsets[i]));
            ecb.AddComponent(soldier, new SoldierData
            {
                attackRange = 1.5f, attackCooldown = 1.5f, dashSpeed = 10f, dashHeight = 0.25f,
                formationOffset = offsets[i]
            });

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{name}_S{i}_GO";
            go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            go.layer = layer;
            go.tag = owner == BattalionOwner.Player ? "PlayerUnit" : "EnemyUnit";
            go.GetComponent<MeshRenderer>().material = mat;
            go.hideFlags = HideFlags.HideAndDontSave;
            ecb.AddComponent(soldier, new EntityLink { goInstanceID = go.GetInstanceID() });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
