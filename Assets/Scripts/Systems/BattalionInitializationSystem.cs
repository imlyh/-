
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Opsive.BehaviorDesigner.Runtime;
#if UNITY_EDITOR
using UnityEditor;
#endif

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class BattalionInitializationSystem : SystemBase
{
    public static readonly Dictionary<int, GameObject> GOMap = new();
    public static BehaviorTree EnemyBTTemplate;

    protected override void OnCreate()
    {
        EntityManager.CreateSingleton<PlayerCommandData>();
        var goldEntity = EntityManager.CreateSingleton<PlayerGoldData>();
        EntityManager.SetComponentData(goldEntity, new PlayerGoldData { gold = 100 });
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
        var ebnA = CreateBattalion(new float3(25,0,10), BattalionOwner.Enemy, "EBN_A", mat, layer, aiA, cfg);
        var ebnB = CreateBattalion(new float3(25,0,13), BattalionOwner.Enemy, "EBN_B", mat, layer, aiB, cfg);

        if (EnemyBTTemplate != null)
        {
            var bt1 = Object.Instantiate(EnemyBTTemplate);
            bt1.StartWhenEnabled = false;
            bt1.StartBehavior(World, ebnA);

            var bt2 = Object.Instantiate(EnemyBTTemplate);
            bt2.StartWhenEnabled = false;
            bt2.StartBehavior(World, ebnB);

            Debug.Log("[ECS] Behavior Trees started for EBN_A and EBN_B");
        }
        else
        {
            Debug.LogWarning("[ECS] EnemyBTTemplate 未设置，敌军将不使用 Behavior Designer AI");
        }

        CreateCastle(new float3(2,0,10), BattalionOwner.Player, "PlayerCastle");
        CreateCastle(new float3(27,0,10), BattalionOwner.Enemy, "EnemyCastle");

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

    Entity CreateBattalion(float3 pos, BattalionOwner owner, string name, Material mat, int layer,
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
            moveSpeed=cfg.moveSpeed, detectionRange=cfg.attackRange
        });
        em.AddBuffer<BattalionPathPoint>(e);
        if (aiData.HasValue) em.AddComponentData(e, aiData.Value);

        for (int i = 0; i < 4; i++)
        {
            var se = em.CreateEntity();
            em.SetName(se, $"{name}_S{i}");
            float3 off = new float3(0, 0, 0);
            em.AddComponentData(se, LocalTransform.FromPosition(off));
            em.AddComponentData(se, new SoldierData{
                battalionEntity=e, attackRange=cfg.attackRange, attackCooldown=cfg.attackCooldown,
                dashSpeed=cfg.dashSpeed, dashHeight=cfg.dashHeight,
                maxSpeed=cfg.moveSpeed * 1.2f, maxForce=cfg.moveSpeed * 3f,
                neighborRadius=2.5f, separationRadius=0.8f,
                currentHP=20, maxHP=20
            });
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{name}_S{i}_GO";
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = new Vector3(pos.x, 0, pos.z);
            go.layer = layer;
            go.tag = owner==BattalionOwner.Player?"PlayerUnit":"EnemyUnit";
            go.GetComponent<MeshRenderer>().material = mat;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = cfg.agentRadius;
            agent.height = cfg.agentHeight;
            agent.speed = cfg.moveSpeed;
            agent.acceleration = 50f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            GOMap[go.GetInstanceID()] = go;
            em.AddComponentData(se, new EntityLink{goInstanceID=go.GetInstanceID()});
        }
        return e;
    }

    void CreateCastle(float3 pos, BattalionOwner owner, string name)
    {
        var em = EntityManager;
        var e = em.CreateEntity();
        em.SetName(e, name);
        em.AddComponentData(e, LocalTransform.FromPosition(pos));
        em.AddComponentData(e, new LocalToWorld{Value=float4x4.Translate(pos)});
        em.AddComponentData(e, new HealthData{currentHP=200, maxHP=200});
        em.AddComponentData(e, new EntityLink{goInstanceID = GameObject.Find(name)?.GetInstanceID() ?? 0});
    }
}
