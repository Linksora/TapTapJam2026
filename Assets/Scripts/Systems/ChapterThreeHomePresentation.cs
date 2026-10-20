using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>第三章家中调查的局部演出。推演随对话推进，始终保留现场小毛。</summary>
public sealed class ChapterThreeHomePresentation : MonoBehaviour
{
    private const string PhoneArt = "Art/场景level3/第三章场景/06守宫家中/06守宫家中-手机低电量";
    private GameObject replay;
    private Text timeLabel;
    private Image sister;
    private Image replayBackdrop;
    private SpriteRenderer phoneRenderer;
    private Sprite phoneBeforeCharge;
    private WorldItemHighlight phoneHighlight;

    public void Apply(string action)
    {
        if (action == "TakeAlleyPaper")
        {
            var paper = FindObjectsByType<WorldItem>(FindObjectsInactive.Include)
                .FirstOrDefault(item => item.gameObject.scene == gameObject.scene
                    && item.WorldPath == "窄巷长凳/TR07_纸片");
            if (paper != null) paper.gameObject.SetActive(false);
            return;
        }
        if (action == "PhoneCharge")
        {
            var sprite = Resources.LoadAll<Sprite>(PhoneArt).FirstOrDefault();
            var phone = FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.scene == gameObject.scene
                    && item.WorldPath == "守宫家/书桌区/TR02_手机与插座");
            if (sprite == null || phone == null)
            {
                Debug.LogWarning("第三章手机通电演出缺少手机物件或低电量图片。", this);
                return;
            }
            if (phoneRenderer == null)
            {
                phoneRenderer = phone.GetComponent<SpriteRenderer>();
                phoneBeforeCharge = phoneRenderer.sprite;
                phoneHighlight = phone.GetComponent<WorldItemHighlight>();
            }
            if (phoneHighlight != null) phoneHighlight.SetNormalSprite(sprite);
            else phoneRenderer.sprite = sprite;
            return;
        }
        if (action == "Clear")
        {
            if (replay != null) Destroy(replay);
            replay = null;
            return;
        }
        if (action != "ReplayStart" && action != "Replay0826" && action != "Replay0831"
            && action != "AlleyReplay" && action != "Footage0834") return;
        EnsureReplay();
        if (replay == null) return;
        timeLabel.text = action == "Footage0834" ? "猫头鹰展示的路口录像 · 今天 08:34"
            : action == "AlleyReplay" ? "小毛的推演 · 今天 08:35—08:39"
            : action == "Replay0826" ? "小毛的推演 · 今天 08:26"
            : action == "Replay0831" ? "小毛的推演 · 今天 08:31"
            : "小毛的推演 · 今天 08:10—09:20";
        if (action == "Footage0834")
            replayBackdrop.sprite = Resources.LoadAll<Sprite>("Art/场景level3/第三章场景/07楼下街口/07楼下街口-背景").FirstOrDefault();
        sister.gameObject.SetActive(action != "ReplayStart");
        var rect = sister.rectTransform;
        rect.anchorMin = new Vector2(action == "Replay0831" ? .66f : .37f, .08f);
        rect.anchorMax = new Vector2(action == "Replay0831" ? .83f : .54f, .82f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>调查对话结束即收起临时展示，场景物件恢复原图。</summary>
    public void EndInspection()
    {
        if (phoneRenderer != null)
        {
            if (phoneHighlight != null && phoneBeforeCharge != null)
                phoneHighlight.SetNormalSprite(phoneBeforeCharge);
            else phoneRenderer.sprite = phoneBeforeCharge;
        }
        phoneRenderer = null;
        phoneHighlight = null;
        phoneBeforeCharge = null;
        if (replay != null) Destroy(replay);
        replay = null;
    }

    private void EnsureReplay()
    {
        if (replay != null) return;
        var canvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.gameObject.scene == gameObject.scene && item.name == "Canvas");
        if (canvas == null) return;
        replay = new GameObject("Chapter3HomeReplay", typeof(RectTransform), typeof(Image));
        replay.transform.SetParent(canvas.transform, false);
        var rect = replay.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.26f, .52f);
        rect.anchorMax = new Vector2(.75f, .94f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var frame = replay.GetComponent<Image>();
        frame.color = new Color(.12f, .17f, .16f, .98f);
        frame.raycastTarget = false;
        var backdrop = AddImage("现场锚点", new Vector2(.02f, .04f), new Vector2(.98f, .84f));
        replayBackdrop = backdrop;
        var controller = GetComponent<BackgroundController>();
        backdrop.sprite = controller != null && controller.CurrentRenderer != null ? controller.CurrentRenderer.sprite : null;
        backdrop.preserveAspect = true;
        backdrop.color = new Color(.83f, .84f, .83f);
        sister = AddImage("推演中的妹妹", new Vector2(.37f, .08f), new Vector2(.54f, .82f));
        sister.sprite = PortraitLoader.Load("守宫妹妹");
        sister.preserveAspect = true;
        var label = new GameObject("推演时间", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(replay.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(.02f, .85f);
        labelRect.anchorMax = new Vector2(.98f, .99f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        timeLabel = label.GetComponent<Text>();
        timeLabel.font = GameFont.Primary;
        timeLabel.fontSize = 20;
        timeLabel.resizeTextForBestFit = true;
        timeLabel.resizeTextMinSize = 12;
        timeLabel.resizeTextMaxSize = 20;
        timeLabel.alignment = TextAnchor.MiddleCenter;
        timeLabel.color = Color.white;
        timeLabel.raycastTarget = false;
    }

    private Image AddImage(string name, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(replay.transform, false);
        var image = obj.GetComponent<Image>();
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        return image;
    }

    private void OnDisable()
    {
        EndInspection();
    }
}
