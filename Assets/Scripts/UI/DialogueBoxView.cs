using System;
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

        // 选项按钮共用同一个父容器，由 SetOptionCount 统一开关，不依赖场景里的默认状态。
        if (count > 0 && optionButtons[0] != null)
        {
            optionsRoot = optionButtons[0].transform.parent.gameObject;
        }

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
