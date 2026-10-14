using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一对话 UI：说话人 + 台词 + 立绘 + 最多两个选项按钮。
/// 用法：
///   Show("小毛", "你好", portrait);                                 纯台词
///   Show("侦探", "选一个？", portrait,                               带选项
///        ("质疑", () => ...), ("附和", () => ...));
/// </summary>
public sealed class DialogueBoxView : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private Text speakerText;
    [SerializeField] private Text contentText;
    [SerializeField] private Button[] optionButtons;

    private Action[] callbacks;
    private GameObject optionsRoot;

    private void Awake()
    {
        ResolveReferences();

        Font chineseFont = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial" }, 32);
        if (chineseFont != null)
        {
            if (speakerText != null) speakerText.font = chineseFont;
            if (contentText != null) contentText.font = chineseFont;
            if (optionButtons != null)
            {
                foreach (Button button in optionButtons)
                {
                    if (button != null)
                    {
                        Text label = button.GetComponentInChildren<Text>();
                        if (label != null)
                        {
                            label.font = chineseFont;
                        }
                    }
                }
            }
        }

        int count = optionButtons != null ? optionButtons.Length : 0;
        callbacks = new Action[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            if (optionButtons[i] != null)
            {
                optionButtons[i].onClick.AddListener(() => HandleOption(index));
            }
        }

        // 选项容器已在 ResolveReferences 中确定（优先按名字 Options，兜底取按钮父节点）。

        SetOptionCount(0);
    }

    /// <summary>显示纯台词（无选项，需手动 Hide 或再次 Show）。</summary>
    public void Show(string speaker, string content, Sprite portrait = null)
    {
        // 先激活：若面板此前处于 inactive，Awake 会在此刻立即执行，保证字体/回调就绪。
        gameObject.SetActive(true);
        Render(speaker, content, portrait);
        SetOptionCount(0);
    }

    /// <summary>显示台词并弹出选项（最多两个，超出忽略）。</summary>
    public void Show(string speaker, string content, Sprite portrait,
        params (string text, Action onClick)[] options)
    {
        gameObject.SetActive(true);
        Render(speaker, content, portrait);

        int count = Mathf.Min(options != null ? options.Length : 0,
            optionButtons != null ? optionButtons.Length : 0);
        for (int i = 0; i < (callbacks?.Length ?? 0); i++)
        {
            callbacks[i] = i < count ? options[i].onClick : null;
        }

        SetOptionCount(count);
        for (int i = 0; i < count; i++)
        {
            Text label = optionButtons[i].GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = options[i].text;
            }
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public bool IsShowingOptions
    {
        get
        {
            if (optionButtons == null)
            {
                return false;
            }

            foreach (Button button in optionButtons)
            {
                if (button != null && button.gameObject.activeSelf)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Render(string speaker, string content, Sprite portrait)
    {
        if (speakerText != null)
        {
            speakerText.text = speaker ?? string.Empty;
            speakerText.gameObject.SetActive(true);
        }

        if (contentText != null)
        {
            contentText.text = content ?? string.Empty;
            contentText.gameObject.SetActive(true);
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }
    }

    private void SetOptionCount(int count)
    {
        if (optionButtons == null)
        {
            return;
        }

        // 父容器随选项数量开关：有选项才显示整个选项区。
        if (optionsRoot != null)
        {
            optionsRoot.SetActive(count > 0);
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(i < count);
            }
        }
    }

    /// <summary>
    /// 引用兜底：用户在编辑器里重排 UI 可能让 Inspector 序列化引用掉线（None）。
    /// 先信任 Inspector 引用；缺失时按约定名字 Options/OptionButton1/OptionButton2 递归查找
    /// （按层级名字，与坐标无关；inactive 子节点也能找到）。
    /// </summary>
    private void ResolveReferences()
    {
        if (optionsRoot == null)
        {
            Transform found = FindChildRecursive(transform, "Options");
            if (found != null)
            {
                optionsRoot = found.gameObject;
            }
        }

        bool missing = optionButtons == null || optionButtons.Length == 0;
        if (!missing)
        {
            foreach (Button button in optionButtons)
            {
                if (button == null)
                {
                    missing = true;
                    break;
                }
            }
        }

        if (missing)
        {
            var buttons = new List<Button>(2);
            foreach (string childName in new[] { "OptionButton1", "OptionButton2" })
            {
                Transform found = FindChildRecursive(transform, childName);
                Button button = found != null ? found.GetComponent<Button>() : null;
                if (button != null)
                {
                    buttons.Add(button);
                }
            }

            if (buttons.Count > 0)
            {
                optionButtons = buttons.ToArray();
            }
        }

        if (optionsRoot == null && optionButtons != null && optionButtons.Length > 0 && optionButtons[0] != null)
        {
            optionsRoot = optionButtons[0].transform.parent.gameObject;
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform deeper = FindChildRecursive(child, childName);
            if (deeper != null)
            {
                return deeper;
            }
        }

        return null;
    }

    private void HandleOption(int index)
    {
        if (callbacks != null && index >= 0 && index < callbacks.Length)
        {
            callbacks[index]?.Invoke();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Preview 纯台词")]
    private void PreviewLine()
    {
        Show("小毛", "侦探，你终于来了，找我有什么事？");
    }

    [ContextMenu("Preview 带选项")]
    private void PreviewOptions()
    {
        Show("侦探", "需要你当场解释清楚。", null,
            ("质疑他的说法", () => Debug.Log("选择了：质疑")),
            ("先顺着他的话说", () => Debug.Log("选择了：附和")));
    }
#endif
}
