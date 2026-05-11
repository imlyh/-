using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    private GameObject panel;
    private bool over;

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        panel = new GameObject("GameOverPanel");
        panel.transform.SetParent(transform);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(400, 200);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0.1f, 0, 0, 0.9f);

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(panel.transform);
        var txt = txtGo.AddComponent<Text>();
        txt.text = "城堡被摧毁\n游戏失败";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 32;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        var tr = txt.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;

        panel.SetActive(false);
    }

    void Update()
    {
        if (over) return;
        var w = World.DefaultGameObjectInjectionWorld;
        if (w == null || !w.IsCreated) return;
        var em = w.EntityManager;
        var q = em.CreateEntityQuery(typeof(HealthData), typeof(EntityLink));
        var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp);
        var hpArr = q.ToComponentDataArray<HealthData>(Unity.Collections.Allocator.Temp);
        var linkArr = q.ToComponentDataArray<EntityLink>(Unity.Collections.Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            var go = FindGO(linkArr[i].goInstanceID);
            if (go != null && go.name == "PlayerCastle" && hpArr[i].currentHP <= 0)
            {
                over = true;
                panel.SetActive(true);
                Time.timeScale = 0;
                break;
            }
        }
        entities.Dispose(); hpArr.Dispose(); linkArr.Dispose();
    }

    GameObject FindGO(int id)
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var g in all) if (g.GetInstanceID() == id) return g;
        return null;
    }
}
