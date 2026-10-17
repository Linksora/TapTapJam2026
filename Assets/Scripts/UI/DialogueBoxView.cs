using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>
/// 常驻主角 + 对话对象 + 说话人 + 台词 + 动态选项列表。
/// 选项数量不限（拼凑题 3 个、出示证物可能 7+ 个），超出可视区域可滚动；
/// 选项列表运行时在对话框上方自建（ChoiceList），不依赖场景里预先摆放，
/// 旧版 Options 横排两个按钮保留但不再使用。
/// 用法：
///   Show("小毛", "你好", portrait);                                 纯台词
///   Show("侦探", "选一个？", portrait,                               带任意数量选项
///        ("质疑", () => ...), ("附和", () => ...));
/// </summary>
public sealed class DialogueBoxView : MonoBehaviour
{
    private const float ChoicePanelWidth = 760f;
    private const float ChoicePanelHeight = 460f;
    private const float ChoiceButtonHeight = 62f;

    [Serializable]
    public sealed class MainCharacterEntry
    {
        public string characterId = "小毛";
        public Sprite portrait;
    }

    [FormerlySerializedAs("portraitImage")]
    [SerializeField] private Image mainCharacterImage;
    [SerializeField] private Image characterImage;
    [SerializeField] private GameObject dialogueBackground;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private List<MainCharacterEntry> mainCharacters = new();
    [SerializeField, Min(0)] private int currentMainCharacterIndex;
    [SerializeField] private Text speakerText;
    [SerializeField] private Text contentText;
    [SerializeField] private Button[] optionButtons;

    private Font chineseFont;
    private GameObject choiceList;
    private RectTransform choiceContent;
    private GameObject choiceTemplate;
    private string displayedSpeaker;
    private Sprite displayedPortrait;

    public bool IsShowing { get; private set; }
    public string CurrentMainCharacterId => mainCharacters.Count > 0
        ? mainCharacters[currentMainCharacterIndex].characterId : string.Empty;
    public event Action<string> MainCharacterChanged;

    private void Awake()
    {
        // 兼容已有场景，按钮只在 MainCharacter 内查找，避免误连其他左右按钮。
        if (mainCharacterImage == null)
            mainCharacterImage = FindChildRecursive(transform, "MainCharacter")?.GetComponent<Image>();
        if (characterImage == null)
            characterImage = FindChildRecursive(transform, "Character")?.GetComponent<Image>();
        if (dialogueBackground == null)
            dialogueBackground = FindChildRecursive(transform, "DialogueBackground")?.gameObject;
        if (mainCharacterImage != null)
        {
            if (leftButton == null)
                leftButton = FindChildRecursive(mainCharacterImage.transform, "LeftButton")?.GetComponent<Button>();
            if (rightButton == null)
                rightButton = FindChildRecursive(mainCharacterImage.transform, "RightButton")?.GetComponent<Button>();
        }

        mainCharacters.RemoveAll(character => character == null || string.IsNullOrWhiteSpace(character.characterId));
        if (mainCharacters.Count == 0)
            mainCharacters.Add(new MainCharacterEntry { portrait = mainCharacterImage != null ? mainCharacterImage.sprite : null });
        currentMainCharacterIndex = Mathf.Clamp(currentMainCharacterIndex, 0, mainCharacters.Count - 1);
        if (leftButton != null) leftButton.onClick.AddListener(PreviousMainCharacter);
        if (rightButton != null) rightButton.onClick.AddListener(NextMainCharacter);
        RefreshMainCharacter();
        SetDialogueVisible(false);

        chineseFont = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial" }, 32);
        if (chineseFont != null)
        {
            if (speakerText != null) speakerText.font = chineseFont;
            if (contentText != null) contentText.font = chineseFont;
        }

        // 旧版写死的两个按钮整体停用，选项统一走动态列表。
        Transform legacyOptions = FindChildRecursive(transform, "Options");
        if (legacyOptions != null)
        {
            legacyOptions.gameObject.SetActive(false);
        }
    }

    /// <summary>显示纯台词（无选项，需手动 Hide 或再次 Show）。</summary>
    public void Show(string speaker, string content, Sprite portrait = null)
    {
        gameObject.SetActive(true);
        Render(speaker, content, portrait);
        SetChoices(null);
    }

    /// <summary>显示台词并弹出选项（数量不限，纵向排列可滚动）。</summary>
    public void Show(string speaker, string content, Sprite portrait,
        params (string text, Action onClick)[] options)
    {
        gameObject.SetActive(true);
        Render(speaker, content, portrait);
        SetChoices(options);
    }

    /// <summary>结束本次对话显示，保留 DialogueBox 和当前主角。</summary>
    public void Hide()
    {
        gameObject.SetActive(true);
        SetChoices(null);
        SetDialogueVisible(false);
    }

    public void PreviousMainCharacter() => SwitchMainCharacter(-1);
    public void NextMainCharacter() => SwitchMainCharacter(1);

    private void SwitchMainCharacter(int direction)
    {
        if (mainCharacters.Count == 0) return;
        currentMainCharacterIndex = (currentMainCharacterIndex + direction + mainCharacters.Count) % mainCharacters.Count;
        RefreshMainCharacter();
        RefreshCharacter();
        MainCharacterChanged?.Invoke(CurrentMainCharacterId);
    }

    private void RefreshMainCharacter()
    {
        if (mainCharacterImage != null)
        {
            MainCharacterEntry character = mainCharacters[currentMainCharacterIndex];
            mainCharacterImage.sprite = character.portrait != null
                ? character.portrait : PortraitLoader.Load(character.characterId);
            mainCharacterImage.enabled = true;
            mainCharacterImage.gameObject.SetActive(true);
        }
        if (leftButton != null) leftButton.interactable = mainCharacters.Count > 0;
        if (rightButton != null) rightButton.interactable = mainCharacters.Count > 0;
    }

    private void SetDialogueVisible(bool visible)
    {
        IsShowing = visible;
        if (dialogueBackground != null) dialogueBackground.SetActive(visible);
        if (speakerText != null) speakerText.gameObject.SetActive(visible);
        if (contentText != null) contentText.gameObject.SetActive(visible);
        Image panel = GetComponent<Image>();
        if (panel != null) panel.enabled = visible;
        RefreshCharacter();
    }

    private void RefreshCharacter()
    {
        if (characterImage == null) return;
        bool visible = IsShowing && !string.IsNullOrWhiteSpace(displayedSpeaker)
            && displayedSpeaker != CurrentMainCharacterId;
        characterImage.sprite = visible ? displayedPortrait : null;
        characterImage.enabled = visible && displayedPortrait != null;
        characterImage.gameObject.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(PreviousMainCharacter);
        if (rightButton != null) rightButton.onClick.RemoveListener(NextMainCharacter);
    }

    public bool IsShowingOptions => choiceList != null && choiceList.activeSelf;

    private void Render(string speaker, string content, Sprite portrait)
    {
        displayedSpeaker = speaker;
        displayedPortrait = portrait;
        SetDialogueVisible(true);
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

    }

    private void SetChoices((string text, Action onClick)[] options)
    {
        EnsureChoiceList();

        if (options == null || options.Length == 0)
        {
            choiceList.SetActive(false);
            return;
        }

        for (int i = choiceContent.childCount - 1; i >= 0; i--)
        {
            Destroy(choiceContent.GetChild(i).gameObject);
        }

        foreach ((string text, Action onClick) option in options)
        {
            Button button = Instantiate(choiceTemplate, choiceContent).GetComponent<Button>();
            button.gameObject.name = "Choice";
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = option.text;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => option.onClick?.Invoke());
            button.gameObject.SetActive(true);
        }

        choiceList.SetActive(true);
    }

    /// <summary>
    /// 首次使用时在对话框上方构建：半透明底 + ScrollRect + 纵向布局 Content + 一个隐藏按钮模板。
    /// </summary>
    private void EnsureChoiceList()
    {
        if (choiceList != null)
        {
            return;
        }

        choiceList = new GameObject("ChoiceList",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        choiceList.transform.SetParent(transform, false);

        RectTransform listRect = choiceList.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 1f);
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.pivot = new Vector2(0.5f, 0f);
        listRect.anchoredPosition = new Vector2(0f, 12f);
        listRect.sizeDelta = new Vector2(ChoicePanelWidth, ChoicePanelHeight);

        Image listImage = choiceList.GetComponent<Image>();
        listImage.color = new Color(0.03f, 0.08f, 0.06f, 0.92f);
        listImage.raycastTarget = true;

        // Viewport + Mask
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(RectMask2D));
        viewport.transform.SetParent(choiceList.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12f, 12f);
        viewportRect.offsetMax = new Vector2(-12f, -12f);

        // Content
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        choiceContent = content.GetComponent<RectTransform>();
        choiceContent.anchorMin = new Vector2(0f, 1f);
        choiceContent.anchorMax = new Vector2(1f, 1f);
        choiceContent.pivot = new Vector2(0.5f, 1f);
        choiceContent.anchoredPosition = Vector2.zero;
        choiceContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = choiceList.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = choiceContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        choiceTemplate = BuildChoiceTemplate();
        // 模板不放进 Content，避免下一次刷新选项时被一起销毁。
        choiceTemplate.transform.SetParent(choiceList.transform, false);
        choiceTemplate.SetActive(false);

        choiceList.SetActive(false);
    }

    private GameObject BuildChoiceTemplate()
    {
        var template = new GameObject("ChoiceTemplate",
            typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer), typeof(Image), typeof(Button));

        LayoutElement layoutElement = template.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = ChoiceButtonHeight;
        layoutElement.minHeight = ChoiceButtonHeight;

        Image image = template.GetComponent<Image>();
        image.color = new Color(0.22f, 0.31f, 0.26f, 1f);
        image.raycastTarget = true;

        Button button = template.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.32f, 0.43f, 0.36f, 1f);
        colors.pressedColor = new Color(0.16f, 0.23f, 0.19f, 1f);
        button.colors = colors;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(template.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 4f);
        labelRect.offsetMax = new Vector2(-12f, -4f);

        Text label = labelObject.GetComponent<Text>();
        label.font = chineseFont != null ? chineseFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.color = new Color(0.98f, 0.94f, 0.82f);
        label.raycastTarget = false;

        return template;
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
}
