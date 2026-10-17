using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>物品搜证交互与临时反馈；搜齐必需证据后由 StoryManager 自动继续剧情。</summary>
public sealed class SearchStage : MonoBehaviour
{
    private Text feedback;

    public void BeginSearch(DialogueData dialogue, LevelDatabase database, InventoryManager inventory)
    {
        if (inventory == null) { Debug.LogError("[SearchStage] 缺少 InventoryManager。", this); return; }
        Camera camera = Camera.main;
        if (camera != null && camera.GetComponent<Physics2DRaycaster>() == null) camera.gameObject.AddComponent<Physics2DRaycaster>();
        HideFeedback();
        foreach (var rule in database.WorldInteractions.Where(rule => rule.searchNodeId == dialogue.DialogueId))
        {
            Transform target = FindWorld(rule.worldPath);
            if (target == null) { Debug.LogError($"[SearchStage] 找不到调查物 {rule.worldPath}"); continue; }
            if (target.GetComponent<WorldItem>() == null) target.gameObject.AddComponent<WorldItem>();
            if (rule.requiredCharacter == "小狗") InvestigationNoseMarker.Show(target);
        }
        foreach (string id in dialogue.SearchClueIds)
            if (!database.WorldInteractions.Any(rule => rule.searchNodeId == dialogue.DialogueId && rule.clueId == id)
                && !inventory.Items.Any(item => item.ItemId == id))
                Debug.LogError($"[SearchStage] 必需证据 {id} 尚未配置调查入口。", this);
    }

    private Transform FindWorld(string path)
    {
        foreach (Transform root in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (root.gameObject.scene == gameObject.scene && root.name == "world") return root.Find(path);
        return null;
    }

    private void EnsureUI()
    {
        if (feedback != null) return;
        Canvas canvas = FindObjectsByType<Canvas>().FirstOrDefault(value => value.gameObject.scene == gameObject.scene && value.name == "Canvas");
        if (canvas == null) return;
        feedback = CreateText("InvestigationFeedback", canvas.transform, new Vector2(.27f,.77f), new Vector2(.78f,.87f));
        feedback.transform.parent.gameObject.SetActive(false);
    }

    private static Text CreateText(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(1,.96f,.83f,.94f); go.GetComponent<Image>().raycastTarget = false;
        var outer = go.GetComponent<RectTransform>(); outer.anchorMin = min; outer.anchorMax = max; outer.offsetMin = outer.offsetMax = Vector2.zero;
        var child = new GameObject("Text", typeof(RectTransform), typeof(Text)); child.transform.SetParent(go.transform, false);
        var rect = child.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(8,2); rect.offsetMax = new Vector2(-8,-2);
        Text text = child.GetComponent<Text>(); text.font = GameFont.Primary; text.fontSize = 18; text.color = new Color(.06f,.25f,.16f); text.raycastTarget = false;
        return text;
    }

    public void ShowFeedback(string message)
    {
        EnsureUI(); if (feedback == null) return;
        feedback.transform.parent.gameObject.SetActive(true); feedback.gameObject.SetActive(true); feedback.text = message;
    }
    public void HideFeedback() { if (feedback != null) feedback.transform.parent.gameObject.SetActive(false); }
    public void EndSearch()
    {
        HideFeedback();
        foreach (InvestigationNoseMarker marker in FindObjectsByType<InvestigationNoseMarker>(FindObjectsInactive.Include))
            if (marker.gameObject.scene == gameObject.scene) marker.gameObject.SetActive(false);
    }
}
