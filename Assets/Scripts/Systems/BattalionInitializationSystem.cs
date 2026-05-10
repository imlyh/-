using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        var cfg = LoadConfig();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        int layer = LayerMask.NameToLayer("Unit");

        CreateBattalion(new float3(4,0,10), BattalionOwner.Player, "PBN_A", mat, layer, null, cfg);
        CreateBattalion(new float3(4,0,7),  BattalionOwner.Player, "PBN_B", mat, layer, null, cfg);

        var aiA = new EnemyAIData{ phase=EnemyAIPhase.GoMine, mineTarget=new float3(20,0,6),
            castlePos=new float3(27,0,10), enemyCastlePos=new float3(2,0,10), miningDuration=cfg.miningDuration };
        var aiB = new EnemyAIData{ phase=EnemyAIPhase.GoMine, mineTarget=new float3(22,0,14),
            castlePos=new float3(27,0,10), enemyCastlePos=new float3(2,0,10), miningDuration=cfg.miningDuration };
        CreateBattalion(new float3(25,0,10), BattalionOwner.Enemy, "EBN_A", mat, layer, aiA, cfg);
        CreateBattalion(new float3(25,0,13), BattalionOwner.Enemy, "EBN_B", mat, layer, aiB, cfg);
        Debug.Log("[ECS] Battalions created");
    }

    BattalionConfig LoadConfig()
    {
#if UNITY_EDITOR
        var guids = AssetDatabase.FindAssets("t:BattalionConfig");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<BattalionConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
        return ScriptableObject.CreateInstance<BattalionConfig>();
    }

    void CreateBattalion(float3 pos, BattalionOwner owner, string name, Material mat, int layer,
        EnemyAIData? aiData, BattalionConfig cfg)
    {
        var em = EntityManager;
        var e = em.CreateEntity();
        em.SetName(e, name);
        em.AddComponentData(e, LocalTransform.FromPosition(pos));
        em.AddComponentData(e, new LocalToWorld{Value=float4x4.Translate(pos)});
        em.AddComponentData(e, new BattalionData
        {
            owner=owner, state=BattalionState.Idle,
            moveSpeed=cfg.moveSpeed, detectionRange=cfg.attackRange,
            bobHeight=cfg.bobHeight, bobFrequency=cfg.bobFrequency,
            bobPhase=UnityEngine.Random.Range(0f,100f)
        });
        em.AddBuffer<BattalionPathPoint>(e);
        if (aiData.HasValue) em.AddComponentData(e, aiData.Value);

        float s = cfg.formationSpacing;
        var off = new float3[]{ new(-s/2,0,-s/2),new(s/2,0,-s/2),new(-s/2,0,s/2),new(s/2,0,s/2) };
        for (int i = 0; i < 4; i++)
        {
            var se = em.CreateEntity();
            em.SetName(se, $"{name}_S{i}");
            em.AddComponentData(se, LocalTransform.FromPosition(off[i]));
            em.AddComponentData(se, new SoldierData{
                battalionEntity=e, formationOffset=off[i],
                attackRange=cfg.attackRange, attackCooldown=cfg.attackCooldown,
                dashSpeed=cfg.dashSpeed, dashHeight=cfg.dashHeight
            });
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{name}_S{i}_GO";
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = new Vector3(pos.x+off[i].x, pos.y, pos.z+off[i].z);
            go.layer = layer;
            go.tag = owner==BattalionOwner.Player?"PlayerUnit":"EnemyUnit";
            go.GetComponent<MeshRenderer>().material = mat;
            var obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.size = new Vector3(0.35f, 1f, 0.35f);
            obs.carving = true;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = cfg.agentRadius;
            agent.height = cfg.agentHeight;
            agent.speed = cfg.moveSpeed;
            agent.acceleration = 50f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            GOMap[go.GetInstanceID()] = go;
            em.AddComponentData(se, new EntityLink{goInstanceID=go.GetInstanceID()});
        }
    }
}
