using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct BattalionInitializationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
        state.EntityManager.CreateSingleton<PlayerCommandData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false; // Run once
        var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        var cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        int unitLayer = LayerMask.NameToLayer("Unit");

        CreateBattalion(ref state, ecb, cubeMesh, mat, unitLayer,
            new float3(4, 0, 10), BattalionOwner.Player, "PlayerBattalion_A");
        CreateBattalion(ref state, ecb, cubeMesh, mat, unitLayer,
            new float3(4, 0, 7), BattalionOwner.Player, "PlayerBattalion_B");
        CreateBattalion(ref state, ecb, cubeMesh, mat, unitLayer,
            new float3(25, 0, 10), BattalionOwner.Enemy, "EnemyBattalion_A");
        CreateBattalion(ref state, ecb, cubeMesh, mat, unitLayer,
            new float3(25, 0, 13), BattalionOwner.Enemy, "EnemyBattalion_B");
    }

    void CreateBattalion(ref SystemState state, EntityCommandBuffer ecb,
        Mesh cubeMesh, Material mat, int unitLayer,
        float3 pos, BattalionOwner owner, string name)
    {
        var entity = ecb.CreateEntity();
        ecb.SetName(entity, name);
        ecb.AddComponent(entity, new LocalTransform
        {
            Position = pos, Scale = 1, Rotation = quaternion.identity
        });
        ecb.AddComponent(entity, new BattalionData
        {
            owner = owner, state = BattalionState.Idle,
            moveSpeed = 4f, detectionRange = 1.3f,
            bobHeight = 0.2f, bobFrequency = 8f, bobPhase = UnityEngine.Random.Range(0f, 100f)
        });
        ecb.AddBuffer<BattalionPathPoint>(entity);

        float s = 0.55f;
        var offsets = new float3[]
        {
            new(-s / 2, 0, -s / 2), new( s / 2, 0, -s / 2),
            new(-s / 2, 0,  s / 2), new( s / 2, 0,  s / 2),
        };

        for (int i = 0; i < 4; i++)
        {
            var soldier = ecb.CreateEntity();
            ecb.SetName(soldier, $"Soldier_{i}");
            ecb.AddComponent(soldier, new Parent { Value = entity });
            ecb.AddComponent(soldier, LocalTransform.FromPosition(offsets[i]));
            ecb.AddComponent(soldier, new SoldierData
            {
                attackRange = 1.5f, attackCooldown = 1.5f, dashSpeed = 10f, dashHeight = 0.25f,
                formationOffset = offsets[i]
            });

            // Create GameObject link for rendering
            var go = CreateSoldierGO(cubeMesh, mat, unitLayer, name, i);
            ecb.AddComponent(soldier, new EntityLink { goInstanceID = go.GetInstanceID() });
        }
    }

    GameObject CreateSoldierGO(Mesh mesh, Material mat, int layer, string parentName, int idx)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"SoldierGO_{parentName}_{idx}";
        go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        go.layer = layer;
        go.tag = "PlayerUnit"; // will be updated later
        go.GetComponent<MeshRenderer>().material = mat;
        // Keep collider for physics detection
        go.hideFlags = HideFlags.HideAndDontSave;
        return go;
    }
}
