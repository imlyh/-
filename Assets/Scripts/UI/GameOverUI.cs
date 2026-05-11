
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    private GameObject panel;
    private Text titleText;
    private Text messageText;
    private Button restartBtn;
    private bool over;

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // --- EventSystem ---
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // --- Full-screen dim overlay ---
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(transform, false);
        var ovRT = overlay.AddComponent<RectTransform>();
        ovRT.anchorMin = Vector2.zero; ovRT.anchorMax = Vector2.one;
        ovRT.sizeDelta = Vector2.zero;
        var ovImg = overlay.AddComponent<Image>();
        ovImg.color = new Color(0, 0, 0, 0.7f);
        ovImg.raycastTarget = false;

        // --- Panel ---
        panel = new GameObject("GameOverPanel");
        panel.transform.SetParent(transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(380, 240);
        rt.anchoredPosition = Vector2.zero;
        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        panelBg.raycastTarget = false;

        // --- Title ---
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        var tRT = titleGo.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 1); tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.sizeDelta = new Vector2(-40, 50);
        tRT.anchoredPosition = new Vector2(0, -16);
        titleText = titleGo.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 30;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.raycastTarget = false;

        // --- Separator ---
        var sep = new GameObject("Separator");
        sep.transform.SetParent(panel.transform, false);
        var sepRT = sep.AddComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0.15f, 1); sepRT.anchorMax = new Vector2(0.85f, 1);
        sepRT.pivot = new Vector2(0.5f, 1f);
        sepRT.sizeDelta = new Vector2(0, 2);
        sepRT.anchoredPosition = new Vector2(0, -70);
        var sepImg = sep.AddComponent<Image>();
        sepImg.color = new Color(0.4f, 0.35f, 0.2f, 0.8f);
        sepImg.raycastTarget = false;

        // --- Message ---
        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(panel.transform, false);
        var mRT = msgGo.AddComponent<RectTransform>();
        mRT.anchorMin = new Vector2(0, 1); mRT.anchorMax = new Vector2(1, 1);
        mRT.pivot = new Vector2(0.5f, 1f);
        mRT.sizeDelta = new Vector2(-40, 40);
        mRT.anchoredPosition = new Vector2(0, -82);
        messageText = msgGo.AddComponent<Text>();
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = 16;
        messageText.color = new Color(0.7f, 0.7f, 0.7f);
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.raycastTarget = false;

        // --- Restart button ---
        var btnGo = new GameObject("RestartBtn");
        btnGo.transform.SetParent(panel.transform, false);
        var bRT = btnGo.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0.2f, 1); bRT.anchorMax = new Vector2(0.8f, 1);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.sizeDelta = new Vector2(0, 48);
        bRT.anchoredPosition = new Vector2(0, -148);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.4f, 0.6f, 1f);
        restartBtn = btnGo.AddComponent<Button>();
        restartBtn.onClick.AddListener(() =>
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });

        var btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(btnGo.transform, false);
        var blRT = btnLabel.AddComponent<RectTransform>();
        blRT.anchorMin = Vector2.zero; blRT.anchorMax = Vector2.one;
        blRT.sizeDelta = Vector2.zero;
        var blText = btnLabel.AddComponent<Text>();
        blText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        blText.fontSize = 20;
        blText.color = Color.white;
        blText.alignment = TextAnchor.MiddleCenter;
        blText.text = "重新开始";
        blText.raycastTarget = false;

        // --- Exit button ---
        var exitGo = new GameObject("ExitBtn");
        exitGo.transform.SetParent(panel.transform, false);
        var eRT = exitGo.AddComponent<RectTransform>();
        eRT.anchorMin = new Vector2(0.2f, 1); eRT.anchorMax = new Vector2(0.8f, 1);
        eRT.pivot = new Vector2(0.5f, 1f);
        eRT.sizeDelta = new Vector2(0, 36);
        eRT.anchoredPosition = new Vector2(0, -202);
        var eImg = exitGo.AddComponent<Image>();
        eImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        var exitBtn = exitGo.AddComponent<Button>();
        exitBtn.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        var eLabel = new GameObject("Label");
        eLabel.transform.SetParent(exitGo.transform, false);
        var elRT = eLabel.AddComponent<RectTransform>();
        elRT.anchorMin = Vector2.zero; elRT.anchorMax = Vector2.one;
        elRT.sizeDelta = Vector2.zero;
        var elText = eLabel.AddComponent<Text>();
        elText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        elText.fontSize = 16;
        elText.color = new Color(0.7f, 0.7f, 0.7f);
        elText.alignment = TextAnchor.MiddleCenter;
        elText.text = "退出游戏";
        elText.raycastTarget = false;

        panel.SetActive(false);
    }

    void Update()
    {
        if (over) return;
        var w = World.DefaultGameObjectInjectionWorld;
        if (w == null || !w.IsCreated) return;
        var em = w.EntityManager;
        var q = em.CreateEntityQuery(typeof(HealthData), typeof(EntityLink));
        var hpArr = q.ToComponentDataArray<HealthData>(Unity.Collections.Allocator.Temp);
        var linkArr = q.ToComponentDataArray<EntityLink>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < hpArr.Length; i++)
        {
            var go = FindGO(linkArr[i].goInstanceID);
            if (go == null) continue;

            if (go.name == "PlayerCastle" && hpArr[i].currentHP <= 0)
            {
                over = true;
                titleText.text = "游戏失败";
                titleText.color = new Color(1f, 0.3f, 0.3f);
                messageText.text = "你的城堡已被摧毁";
                panel.SetActive(true);
                Time.timeScale = 0;
                break;
            }
            if (go.name == "EnemyCastle" && hpArr[i].currentHP <= 0)
            {
                over = true;
                titleText.text = "胜 利 !";
                titleText.color = new Color(0.3f, 1f, 0.4f);
                messageText.text = "敌方城堡已被攻占";
                panel.SetActive(true);
                Time.timeScale = 0;
                break;
            }
        }
        hpArr.Dispose(); linkArr.Dispose();
    }

    GameObject FindGO(int id)
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var g in all) if (g.GetInstanceID() == id) return g;
        return null;
    }
}
