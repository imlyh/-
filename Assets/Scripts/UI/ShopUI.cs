
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
    private Text buyBtnLabel;

    void Start()
    {
        Instance = this;

        // --- Canvas ---
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // --- Ensure EventSystem exists (required for UI clicks) ---
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // --- Panel ---
        panel = new GameObject("ShopPanel");
        panel.transform.SetParent(transform, false);
        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(340, 260);
        panelRT.anchoredPosition = Vector2.zero;
        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        panelBg.raycastTarget = false; // background only

        // --- Title bar ---
        var titleBar = new GameObject("TitleBar");
        titleBar.transform.SetParent(panel.transform, false);
        var tbRT = titleBar.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.pivot = new Vector2(0.5f, 1f);
        tbRT.sizeDelta = new Vector2(0, 44);
        tbRT.anchoredPosition = Vector2.zero;
        titleBar.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 1f);
        titleBar.GetComponent<Image>().raycastTarget = false; // background only

        MakeText(titleBar.transform, "城 堡 商 店", 22, new Color(1f, 0.9f, 0.5f),
            TextAnchor.MiddleCenter).rectTransform.anchoredPosition = Vector2.zero;

        // --- Close button (top-right of title bar) ---
        var closeBtn = new GameObject("CloseBtn");
        closeBtn.transform.SetParent(titleBar.transform, false);
        var cbRT = closeBtn.AddComponent<RectTransform>();
        cbRT.anchorMin = new Vector2(1, 0.5f); cbRT.anchorMax = new Vector2(1, 0.5f);
        cbRT.pivot = new Vector2(1, 0.5f);
        cbRT.sizeDelta = new Vector2(36, 36);
        cbRT.anchoredPosition = new Vector2(-6, 0);
        closeBtn.AddComponent<Image>().color = new Color(0.7f, 0.15f, 0.15f, 1f);
        var cb = closeBtn.AddComponent<Button>();
        cb.onClick.AddListener(() => panel.SetActive(false));
        MakeText(closeBtn.transform, "X", 18, Color.white, TextAnchor.MiddleCenter);

        // --- Separator below title ---
        var sep = new GameObject("Separator");
        sep.transform.SetParent(panel.transform, false);
        var sepRT = sep.AddComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0.05f, 1); sepRT.anchorMax = new Vector2(0.95f, 1);
        sepRT.pivot = new Vector2(0.5f, 1f);
        sepRT.sizeDelta = new Vector2(0, 2);
        sepRT.anchoredPosition = new Vector2(0, -46);
        sep.AddComponent<Image>().color = new Color(0.4f, 0.35f, 0.2f, 0.8f);
        sep.GetComponent<Image>().raycastTarget = false;

        // --- Gold text ---
        var goldGo = new GameObject("GoldText");
        goldGo.transform.SetParent(panel.transform, false);
        var goldRT = goldGo.AddComponent<RectTransform>();
        goldRT.anchorMin = new Vector2(0, 1); goldRT.anchorMax = new Vector2(1, 1);
        goldRT.pivot = new Vector2(0.5f, 1f);
        goldRT.sizeDelta = new Vector2(-40, 24);
        goldRT.anchoredPosition = new Vector2(0, -58);
        goldText = goldGo.AddComponent<Text>();
        goldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goldText.fontSize = 18;
        goldText.color = new Color(1f, 0.85f, 0.2f);
        goldText.alignment = TextAnchor.MiddleCenter;

        // --- Info text ---
        var infoGo = new GameObject("InfoText");
        infoGo.transform.SetParent(panel.transform, false);
        var infoRT = infoGo.AddComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0, 1); infoRT.anchorMax = new Vector2(1, 1);
        infoRT.pivot = new Vector2(0.5f, 1f);
        infoRT.sizeDelta = new Vector2(-40, 20);
        infoRT.anchoredPosition = new Vector2(0, -84);
        var infoText = infoGo.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        infoText.fontSize = 13;
        infoText.color = new Color(0.6f, 0.6f, 0.65f);
        infoText.alignment = TextAnchor.MiddleCenter;
        infoText.text = "招募一营 4 名战士 · 点击地图指挥移动";

        // --- Separator 2 ---
        var sep2 = new GameObject("Separator2");
        sep2.transform.SetParent(panel.transform, false);
        var sep2RT = sep2.AddComponent<RectTransform>();
        sep2RT.anchorMin = new Vector2(0.2f, 1); sep2RT.anchorMax = new Vector2(0.8f, 1);
        sep2RT.pivot = new Vector2(0.5f, 1f);
        sep2RT.sizeDelta = new Vector2(0, 1);
        sep2RT.anchoredPosition = new Vector2(0, -110);
        sep2.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 0.5f);
        sep2.GetComponent<Image>().raycastTarget = false;

        // --- Buy button ---
        var buyGo = new GameObject("BuyBtn");
        buyGo.transform.SetParent(panel.transform, false);
        var buyRT = buyGo.AddComponent<RectTransform>();
        buyRT.anchorMin = new Vector2(0.15f, 1); buyRT.anchorMax = new Vector2(0.85f, 1);
        buyRT.pivot = new Vector2(0.5f, 1f);
        buyRT.sizeDelta = new Vector2(0, 52);
        buyRT.anchoredPosition = new Vector2(0, -176);
        buyGo.AddComponent<Image>().color = new Color(0.15f, 0.55f, 0.2f, 1f);
        buyBtn = buyGo.AddComponent<Button>();
        buyBtn.onClick.AddListener(OnBuy);

        buyBtnLabel = MakeText(buyGo.transform, "招募 -40G", 20, Color.white, TextAnchor.MiddleCenter);
        buyBtnLabel.rectTransform.anchoredPosition = Vector2.zero;
        // Ensure label fills button
        buyBtnLabel.rectTransform.anchorMin = Vector2.zero;
        buyBtnLabel.rectTransform.anchorMax = Vector2.one;
        buyBtnLabel.rectTransform.sizeDelta = Vector2.zero;

        // --- Footer ---
        var footer = new GameObject("Footer");
        footer.transform.SetParent(panel.transform, false);
        var ftRT = footer.AddComponent<RectTransform>();
        ftRT.anchorMin = new Vector2(0, 0); ftRT.anchorMax = new Vector2(1, 0);
        ftRT.pivot = new Vector2(0.5f, 0f);
        ftRT.sizeDelta = new Vector2(0, 24);
        ftRT.anchoredPosition = Vector2.zero;
        var ftText = footer.AddComponent<Text>();
        ftText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ftText.fontSize = 11;
        ftText.color = new Color(0.35f, 0.35f, 0.4f);
        ftText.alignment = TextAnchor.MiddleCenter;
        ftText.text = "点击城堡可再次打开商店";

        panel.SetActive(false);
    }

    Text MakeText(Transform parent, string txt, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200, size + 8);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.color = color; t.alignment = anchor;
        t.raycastTarget = false;
        return t;
    }

    public void SetOpen(bool open)
    {
        panel.SetActive(open);
        if (open) RefreshGold();
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        RefreshGold();
    }

    void RefreshGold()
    {
        var w = World.DefaultGameObjectInjectionWorld;
        if (w == null || !w.IsCreated) return;
        var q = w.EntityManager.CreateEntityQuery(typeof(PlayerGoldData));
        if (q.TryGetSingleton<PlayerGoldData>(out var gold))
        {
            goldText.text = $"持有 {gold.gold} G";
            bool canBuy = gold.gold >= 40;
            buyBtn.interactable = canBuy;
            buyBtnLabel.text = canBuy ? "招募一营 -40G" : $"招募一营 -40G (不足{(int)(40 - gold.gold)}G)";
            buyBtn.GetComponent<Image>().color = canBuy
                ? new Color(0.15f, 0.55f, 0.2f, 1f)
                : new Color(0.3f, 0.3f, 0.3f, 0.8f);
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
        });
        em.AddBuffer<BattalionPathPoint>(e);

        for (int i = 0; i < 4; i++)
        {
            var se = em.CreateEntity();
            em.SetName(se, $"PBN_Shop_S{i}");
            em.AddComponentData(se, LocalTransform.FromPosition(0));
            em.AddComponentData(se, new SoldierData
            {
                battalionEntity = e,
                attackRange = cfg.attackRange, attackCooldown = cfg.attackCooldown,
                dashSpeed = cfg.dashSpeed, dashHeight = cfg.dashHeight,
                maxSpeed = cfg.moveSpeed * 1.2f, maxForce = cfg.moveSpeed * 3f,
                neighborRadius = 2.5f, separationRadius = 0.8f,
                currentHP = 20, maxHP = 20
            });
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Shop_S{i}_GO";
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = spawnPos;
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
