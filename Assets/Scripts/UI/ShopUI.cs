using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }
    private GameObject panel;
    private Text goldText;
    private Button buyBtn;

    void Start()
    {
        Instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        panel = new GameObject("ShopPanel");
        panel.transform.SetParent(transform);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(300, 200);
        prt.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        var title = MakeText(panel.transform, "城堡商店", 22, Color.white, TextAnchor.MiddleCenter);
        var tr = title.rectTransform;
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
        tr.sizeDelta = new Vector2(0, 40); tr.anchoredPosition = new Vector2(0, -20);

        goldText = MakeText(panel.transform, "", 16, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter);
        var gr = goldText.rectTransform;
        gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.7f);
        gr.sizeDelta = new Vector2(200, 30); gr.anchoredPosition = Vector2.zero;

        buyBtn = MakeButton(panel.transform, "招募一营 (40G)", new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.5f),
            new Color(0.2f, 0.5f, 0.2f), OnBuy);

        MakeButton(panel.transform, "X", new Vector2(0.85f, 0.85f), new Vector2(1f, 1f),
            new Color(0.6f, 0.1f, 0.1f), () => panel.SetActive(false));

        panel.SetActive(false);
    }

    Text MakeText(Transform parent, string txt, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(parent);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.color = color; t.alignment = anchor;
        return t;
    }

    Button MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, Color col, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("Btn"); go.transform.SetParent(parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.sizeDelta = Vector2.zero;
        go.AddComponent<Image>().color = col;
        var b = go.AddComponent<Button>(); b.onClick.AddListener(cb);
        var t = MakeText(go.transform, label, 18, Color.white, TextAnchor.MiddleCenter);
        var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        t.fontSize = label == "X" ? 18 : 16;
        return b;
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        var w = World.DefaultGameObjectInjectionWorld;
        if (w == null || !w.IsCreated) return;
        var q = w.EntityManager.CreateEntityQuery(typeof(PlayerGoldData));
        if (q.TryGetSingleton<PlayerGoldData>(out var gold))
        {
            goldText.text = $"持有: {gold.gold} G";
            buyBtn.interactable = gold.gold >= 40;
        }
    }

    void OnBuy()
    {
        var w = World.DefaultGameObjectInjectionWorld;
        var em = w.EntityManager;
        var q = em.CreateEntityQuery(typeof(PlayerGoldData));
        if (!q.TryGetSingleton<PlayerGoldData>(out var gold) || gold.gold < 40) return;
        var goldEnt = q.GetSingletonEntity();

        gold.gold -= 40;
        em.SetComponentData(goldEnt, gold);

        SpawnBattalion(em);
        panel.SetActive(false);
    }

    void SpawnBattalion(EntityManager em)
    {
        var spawnPos = new float3(2f, 0, 12f);
        int layer = LayerMask.NameToLayer("Unit");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.white };

        BattalionConfig cfg = null;
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets("t:BattalionConfig");
        if (guids.Length > 0)
            cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<BattalionConfig>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
        if (cfg == null) cfg = ScriptableObject.CreateInstance<BattalionConfig>();

        var e = em.CreateEntity();
        em.SetName(e, $"PBN_Shop_{Time.frameCount}");
        em.AddComponentData(e, LocalTransform.FromPosition(spawnPos));
        em.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(spawnPos) });
        em.AddComponentData(e, new BattalionData
        {
            owner = BattalionOwner.Player, state = BattalionState.Idle,
            moveSpeed = cfg.moveSpeed, detectionRange = cfg.attackRange,
            bobHeight = cfg.bobHeight, bobFrequency = cfg.bobFrequency,
            bobPhase = Random.Range(0f, 100f)
        });
        em.AddBuffer<BattalionPathPoint>(e);

        float s = cfg.formationSpacing;
        var off = new float3[] { new(-s / 2, 0, -s / 2), new(s / 2, 0, -s / 2), new(-s / 2, 0, s / 2), new(s / 2, 0, s / 2) };
        for (int i = 0; i < 4; i++)
        {
            var se = em.CreateEntity();
            em.SetName(se, $"PBN_Shop_S{i}");
            em.AddComponentData(se, LocalTransform.FromPosition(off[i]));
            em.AddComponentData(se, new SoldierData
            {
                battalionEntity = e, formationOffset = off[i],
                attackRange = cfg.attackRange, attackCooldown = cfg.attackCooldown,
                dashSpeed = cfg.dashSpeed, dashHeight = cfg.dashHeight
            });
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Shop_S{i}_GO";
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = new Vector3(spawnPos.x + off[i].x, 0, spawnPos.z + off[i].z);
            go.layer = layer; go.tag = "PlayerUnit";
            go.GetComponent<MeshRenderer>().material = mat;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = cfg.agentRadius; agent.height = cfg.agentHeight;
            agent.speed = cfg.moveSpeed; agent.acceleration = 50f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            BattalionInitializationSystem.GOMap[go.GetInstanceID()] = go;
            em.AddComponentData(se, new EntityLink { goInstanceID = go.GetInstanceID() });
        }
    }
}
