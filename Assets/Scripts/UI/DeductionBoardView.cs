using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>在原 Options 区域中显示多句选填，选词菜单仍复用 DialogueBoxView 的 Options。</summary>
public sealed class DeductionBoardView
{
    private readonly RectTransform options;
    private RectTransform panel;
    private Vector2 anchorMin, anchorMax, position, size, pivot;
    private bool visible;

    public DeductionBoardView(RectTransform root) { options = root; }

    public void Hide()
    {
        if (panel != null) panel.gameObject.SetActive(false);
        if (!visible || options == null) return;
        options.anchorMin = anchorMin; options.anchorMax = anchorMax; options.pivot = pivot;
        options.anchoredPosition = position; options.sizeDelta = size;
        visible = false;
    }

    public void Show(DeductionData data, IReadOnlyList<string> selected, bool[] errors, Action<int> choose, Action confirm)
    {
        Hide();
        anchorMin = options.anchorMin; anchorMax = options.anchorMax; pivot = options.pivot;
        position = options.anchoredPosition; size = options.sizeDelta;
        visible = true;
        options.anchorMin = new Vector2(0.27f, 0.04f); options.anchorMax = new Vector2(0.80f, 0.52f);
        options.pivot = new Vector2(0.5f, 0.5f); options.offsetMin = options.offsetMax = Vector2.zero;
        options.gameObject.SetActive(true);
        if (panel != null) UnityEngine.Object.Destroy(panel.gameObject);
        panel = new GameObject("StatementBoard", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        panel.SetParent(options, false); panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 5; layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        AddText(data.title, 38, 19);
        for (int i = 0; i < data.statements.Length; i++)
        {
            int row = i;
            string word = string.IsNullOrEmpty(selected[i]) ? "请选择" : selected[i];
            string label = data.statements[i].sentence.Replace("{0}", $"【{word}】");
            if (errors != null && errors[i]) label += "（尚有矛盾）";
            AddButton(label, 52, () => choose(row), errors != null && errors[i]);
        }
        Button submit = AddButton(data.confirmText, 38, () => confirm(), false);
        bool complete = true;
        foreach (string word in selected) if (string.IsNullOrEmpty(word)) complete = false;
        submit.interactable = complete;
    }

    private Text AddText(string text, float height, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(panel, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        Text label = go.GetComponent<Text>(); label.text = text; label.font = GameFont.Primary;
        label.fontSize = fontSize; label.color = new Color(0.13f, 0.25f, 0.16f); label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft;
        return label;
    }

    private Button AddButton(string text, float height, UnityEngine.Events.UnityAction action, bool error)
    {
        var go = new GameObject("StatementButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(panel, false); go.GetComponent<LayoutElement>().preferredHeight = height;
        go.GetComponent<Image>().color = error ? new Color(0.52f, 0.2f, 0.15f) : new Color(0.06f, 0.32f, 0.21f);
        var label = new GameObject("Text", typeof(RectTransform), typeof(Text)); label.transform.SetParent(go.transform, false);
        var rect = label.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10, 2); rect.offsetMax = new Vector2(-10, -2);
        Text value = label.GetComponent<Text>(); value.text = text; value.font = GameFont.Primary; value.fontSize = 19;
        value.color = new Color(1, 0.95f, 0.82f); value.alignment = TextAnchor.MiddleLeft; value.raycastTarget = false;
        Button button = go.GetComponent<Button>(); button.onClick.AddListener(action);
        go.AddComponent<GameAudioButton>();
        return button;
    }
}
