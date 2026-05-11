
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class CastleHPBar : MonoBehaviour
{
    public string castleName = "PlayerCastle";
    public Color barColor = Color.green;

    private Slider slider;
    private Image fillImg;
    private int castleGOId;

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.7f, 0.08f);

        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(transform);
        slider = sliderGo.AddComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        var srt = slider.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.sizeDelta = Vector2.zero;

        var bgGo = new GameObject("BG");
        bgGo.transform.SetParent(sliderGo.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f);
        bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.sizeDelta = Vector2.zero;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(sliderGo.transform, false);
        fillImg = fillGo.AddComponent<Image>();
        fillImg.color = barColor;
        fillImg.rectTransform.anchorMin = Vector2.zero; fillImg.rectTransform.anchorMax = Vector2.one;
        fillImg.rectTransform.sizeDelta = Vector2.zero;

        slider.fillRect = fillImg.rectTransform;
        slider.maxValue = 1;
        slider.value = 1;

        var castleGo = GameObject.Find(castleName);
        if (castleGo != null)
        {
            castleGOId = castleGo.GetInstanceID();
            transform.position = castleGo.transform.position + Vector3.up * 0.6f;
            transform.forward = Camera.main.transform.forward;
        }
    }

    void Update()
    {
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;

        var w = World.DefaultGameObjectInjectionWorld;
        if (w == null || !w.IsCreated) return;
        var em = w.EntityManager;
        var q = em.CreateEntityQuery(typeof(HealthData), typeof(EntityLink));
        var links = q.ToComponentDataArray<EntityLink>(Unity.Collections.Allocator.Temp);
        var hpArr = q.ToComponentDataArray<HealthData>(Unity.Collections.Allocator.Temp);
        for (int i = 0; i < links.Length; i++)
        {
            if (links[i].goInstanceID == castleGOId)
            {
                float pct = (float)hpArr[i].currentHP / hpArr[i].maxHP;
                slider.value = pct;
                fillImg.color = pct > 0.5f ? barColor : (pct > 0.25f ? Color.yellow : Color.red);
                break;
            }
        }
        links.Dispose(); hpArr.Dispose();
    }
}
