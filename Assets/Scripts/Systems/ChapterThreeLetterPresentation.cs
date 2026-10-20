using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>在同一张松鼠家背景内区分外屋与里间，物件资料只在得到同意后展示。</summary>
public sealed class ChapterThreeLetterPresentation : MonoBehaviour
{
    private GameObject display;

    public void Apply(string action)
    {
        if (action == "Outside" || action == "Inside")
        {
            CloseDisplay();
            var panner = FindObjectsByType<ScenePanner>(FindObjectsInactive.Include)
                .FirstOrDefault(item => item.gameObject.scene == gameObject.scene);
            if (panner != null) panner.FocusAt(action == "Inside" ? .12f : .32f);
            return;
        }
        if (action == "Clear") { CloseDisplay(); return; }
        string path = string.Empty;
        if (action == "ShowLetter") path = "Art/场景level3/第三章场景/09松鼠家中/09松鼠家中-守宫妹妹的信1";
        else if (action == "ShowVisitNote") path = "Art/场景level3/第三章场景/09松鼠家中/09松鼠家中-松鼠的便条1";
        else if (action == "ShowSchedule") path = "Art/场景level3/第三章场景/06守宫家中/06守宫家中-姐姐的日程表1";
        if (string.IsNullOrEmpty(path)) return;
        var sprite = Resources.LoadAll<Sprite>(path).FirstOrDefault();
        var canvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include)
            .FirstOrDefault(item => item.gameObject.scene == gameObject.scene && item.name == "Canvas");
        if (sprite == null || canvas == null) return;
        CloseDisplay();
        display = new GameObject("LetterDialogueEvidence", typeof(RectTransform), typeof(Image));
        display.transform.SetParent(canvas.transform, false);
        var image = display.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(.31f, .54f);
        rect.anchorMax = new Vector2(.69f, .94f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public void CloseDisplay()
    {
        if (display != null) Destroy(display);
        display = null;
    }

    private void OnDisable() => CloseDisplay();
}
