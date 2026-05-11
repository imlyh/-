using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class GoldUI : MonoBehaviour
{
    private Text goldText;

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var go = new GameObject("GoldText");
        go.transform.SetParent(transform);
        goldText = go.AddComponent<Text>();
        goldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goldText.fontSize = 28;
        goldText.color = new Color(1f, 0.85f, 0.2f, 1f);
        goldText.alignment = TextAnchor.UpperLeft;
        var rt = goldText.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(20, -20);
        rt.sizeDelta = new Vector2(300, 40);
    }

    void Update()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        var em = world.EntityManager;
        var q = em.CreateEntityQuery(typeof(PlayerGoldData));
        if (q.TryGetSingleton<PlayerGoldData>(out var gold))
            goldText.text = $"金币: {gold.gold}";
    }
}
