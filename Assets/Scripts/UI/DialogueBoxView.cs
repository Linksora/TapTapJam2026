using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>
/// 常驻主角 + 持续显示的对话对象 + 说话人 + 台词 + Options。
/// 复用场景 Options 下的按钮和布局；选项超出按钮数量时在原区域翻页。
/// 用法：
///   Show("小毛", "你好", portrait);                                 纯台词
///   Show("侦探", "选一个？", portrait,                               带任意数量选项
///        ("质疑", () => ...), ("附和", () => ...));
/// </summary>
public sealed class DialogueBoxView : MonoBehaviour
{
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
    [SerializeField] private GameObject optionsRoot;
    [SerializeField] private Button[] optionButtons;

    private Font chineseFont;
    private readonly List<Button> choiceButtons = new();
    private (string text, Action onClick)[] currentChoices;
    private int choicePage;
    private Sprite conversationPortrait;
    private ItemReceiver evidenceReceiver;
    private Action<ItemData> evidenceHandler;
    private bool characterRaycastTarget;
    private DeductionBoardView deductionBoard;
    private bool showingBoard;
    private Button avatarButton;
    public string ConversationPartnerId { get; private set; }
    public bool IsShowing { get; private set; }
    public bool IsWaitingForEvidence { get; private set; }
    public string CurrentMainCharacterId => mainCharacters.Count > 0
        ? mainCharacters[currentMainCharacterIndex].characterId : string.Empty;
    public event Action<string> MainCharacterChanged;
    public event Action MainCharacterClicked;

    // 没有主角菜单时让鼠标穿过立绘区域，避免挡住左下方的场景调查物。
    public void SetMainCharacterInteractionEnabled(bool enabled)
    {
        if (mainCharacterImage != null) mainCharacterImage.raycastTarget = enabled;
        if (avatarButton != null) avatarButton.interactable = enabled;
    }

    private void Awake()
    {
        // 兼容已有场景，按钮只在 MainCharacter 内查找，避免误连其他左右按钮。
        if (mainCharacterImage == null)
            mainCharacterImage = FindChildRecursive(transform, "MainCharacter")?.GetComponent<Image>();
        if (characterImage == null)
            characterImage = FindChildRecursive(transform, "Character")?.GetComponent<Image>();
        if (characterImage != null) characterRaycastTarget = characterImage.raycastTarget;
        if (dialogueBackground == null)
            dialogueBackground = FindChildRecursive(transform, "DialogueBackground")?.gameObject;
        if (mainCharacterImage != null)
        {
            // 预制件的立绘默认不挡鼠标；成为可点击主角后必须启用射线接收。
            mainCharacterImage.raycastTarget = true;
            avatarButton = mainCharacterImage.GetComponent<Button>();
            if (avatarButton == null) avatarButton = mainCharacterImage.gameObject.AddComponent<Button>();
            avatarButton.targetGraphic = mainCharacterImage;
            avatarButton.transition = Selectable.Transition.None;
            avatarButton.onClick.AddListener(OnMainCharacterClicked);
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

        chineseFont = GameFont.Primary;
        if (chineseFont != null)
        {
            if (speakerText != null) speakerText.font = chineseFont;
            if (contentText != null) contentText.font = chineseFont;
        }

        ResolveOptions();
        SetChoices(null);
    }

    /// <summary>显示纯台词（无选项，需手动 Hide 或再次 Show）。</summary>
    public void Show(string speaker, string content, Sprite portrait = null)
    {
        HideBoard();
        StopWaitingForEvidence();
        gameObject.SetActive(true);
        Render(speaker, content, portrait);
        SetChoices(null);
    }

    /// <summary>显示台词和 Options（超过场景按钮数量时翻页）。</summary>
    public void Show(string speaker, string content, Sprite portrait,
        params (string text, Action onClick)[] options)
    {
        HideBoard();
        StopWaitingForEvidence();
        gameObject.SetActive(true);
        Render(speaker, content, portrait);
        SetChoices(options);
    }

    /// <summary>结束本次对话显示，保留 DialogueBox 和当前主角。</summary>
    public void Hide()
    {
        HideBoard();
        StopWaitingForEvidence();
        gameObject.SetActive(true);
        SetChoices(null);
        ClearConversationPartner();
        SetDialogueVisible(false);
    }

    /// <summary>对方保持沉默，保留立绘和对话区域，接受从笔记本拖来的证物。</summary>
    public void ShowConfrontation(string characterId, Sprite portrait, string narration, Action<ItemData> onEvidence)
    {
        Show(characterId, narration, portrait);
        if (speakerText != null) speakerText.gameObject.SetActive(false);
        if (contentText != null) contentText.gameObject.SetActive(true);
        if (evidenceReceiver == null)
        {
            evidenceReceiver = GetComponent<ItemReceiver>();
            if (evidenceReceiver == null) evidenceReceiver = gameObject.AddComponent<ItemReceiver>();
            evidenceReceiver.OnItemReceived.AddListener(OnEvidenceReceived);
        }
        IsWaitingForEvidence = true;
        evidenceHandler = onEvidence;
        evidenceReceiver.enabled = true;
        if (characterImage != null) characterImage.raycastTarget = true;
    }

    public void StopWaitingForEvidence()
    {
        IsWaitingForEvidence = false;
        evidenceHandler = null;
        if (evidenceReceiver != null) evidenceReceiver.enabled = false;
        if (characterImage != null) characterImage.raycastTarget = characterRaycastTarget;
    }

    private void OnEvidenceReceived(ItemData item)
    {
        if (!IsWaitingForEvidence || item == null) return;
        Action<ItemData> handler = evidenceHandler;
        StopWaitingForEvidence();
        handler?.Invoke(item);
    }

    public void PreviousMainCharacter() => SwitchMainCharacter(-1);
    public void NextMainCharacter() => SwitchMainCharacter(1);

    private void OnMainCharacterClicked()
    {
        if (MainCharacterClicked != null) MainCharacterClicked.Invoke();
        else NextMainCharacter();
    }

    public void SetMainCharacters(params string[] ids)
    {
        string current = CurrentMainCharacterId;
        mainCharacters.Clear();
        foreach (string id in ids)
            if (!string.IsNullOrWhiteSpace(id)) mainCharacters.Add(new MainCharacterEntry { characterId = id.Trim(), portrait = PortraitLoader.Load(id.Trim()) });
        if (mainCharacters.Count == 0) mainCharacters.Add(new MainCharacterEntry());
        currentMainCharacterIndex = Mathf.Max(0, mainCharacters.FindIndex(entry => entry.characterId == current));
        RefreshMainCharacter(); RefreshCharacter();
        MainCharacterChanged?.Invoke(CurrentMainCharacterId);
    }

    public void SelectMainCharacter(string id)
    {
        int index = mainCharacters.FindIndex(entry => entry.characterId == id);
        if (index < 0) return;
        currentMainCharacterIndex = index; RefreshMainCharacter(); RefreshCharacter();
        MainCharacterChanged?.Invoke(id);
    }

    private void HideBoard() { deductionBoard?.Hide(); showingBoard = false; }

    public void ShowBoard(DeductionData data, IReadOnlyList<string> selected, bool[] errors, Action<int> choose, Action confirm)
    {
        Show(CurrentMainCharacterId, data.title);
        ClearConversationPartner();
        if (optionsRoot == null)
        {
            Debug.LogError("DialogueBoxView：找不到 Options，无法显示整理线索题目。", this);
            return;
        }
        deductionBoard ??= new DeductionBoardView((RectTransform)optionsRoot.transform);
        deductionBoard.Show(data, selected, errors, choose, confirm);
        showingBoard = true;
    }

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
        bool visible = IsShowing && !string.IsNullOrWhiteSpace(ConversationPartnerId)
            && ConversationPartnerId != CurrentMainCharacterId;
        characterImage.sprite = visible ? conversationPortrait : null;
        characterImage.enabled = visible && conversationPortrait != null;
        characterImage.gameObject.SetActive(visible);
    }

    public void ClearConversationPartner()
    {
        StopWaitingForEvidence();
        ConversationPartnerId = null;
        conversationPortrait = null;
        RefreshCharacter();
    }

    private void OnDestroy()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(PreviousMainCharacter);
        if (rightButton != null) rightButton.onClick.RemoveListener(NextMainCharacter);
        if (avatarButton != null) avatarButton.onClick.RemoveListener(OnMainCharacterClicked);
        if (evidenceReceiver != null) evidenceReceiver.OnItemReceived.RemoveListener(OnEvidenceReceived);
    }

    public bool IsShowingOptions => showingBoard || (optionsRoot != null && optionsRoot.activeSelf
        && currentChoices != null && currentChoices.Length > 0);

    private void Render(string speaker, string content, Sprite portrait)
    {
        if (string.IsNullOrWhiteSpace(speaker))
            ClearConversationPartner();
        else if (speaker != CurrentMainCharacterId)
        {
            ConversationPartnerId = speaker;
            conversationPortrait = portrait;
        }
        // 主角接话或内心独白不清除当前交谈对象。
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

    private void ResolveOptions()
    {
        if (optionsRoot == null)
            optionsRoot = FindChildRecursive(transform, "Options")?.gameObject;
        if (optionsRoot == null) return;

        // 以当前场景层级为准，包含手动新增的按钮，不依赖旧序列化数组的数量/顺序。
        foreach (Button button in optionsRoot.GetComponentsInChildren<Button>(true))
            choiceButtons.Add(button);
        if (choiceButtons.Count == 1)
        {
            Button extra = Instantiate(choiceButtons[0], choiceButtons[0].transform.parent);
            extra.name = "OptionButton2";
            choiceButtons.Add(extra);
        }
        optionButtons = choiceButtons.ToArray();
    }

    private void SetChoices((string text, Action onClick)[] options)
    {
        currentChoices = options;
        choicePage = 0;
        if (optionsRoot == null || choiceButtons.Count == 0)
        {
            if (options != null && options.Length > 0)
                Debug.LogError("DialogueBoxView：Options 下没有可用的选项按钮。", this);
            return;
        }

        RenderChoices();
    }

    private void RenderChoices()
    {
        int count = currentChoices?.Length ?? 0;
        bool paged = count > choiceButtons.Count;
        int pageSize = paged ? choiceButtons.Count - 1 : choiceButtons.Count;
        int start = choicePage * pageSize;
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            Button button = choiceButtons[i];
            button.onClick.RemoveAllListeners();
            if (!button.TryGetComponent<GameAudioButton>(out var clickAudio))
                clickAudio = button.gameObject.AddComponent<GameAudioButton>();
            clickAudio.Bind();
            bool navigation = paged && i == choiceButtons.Count - 1;
            int index = start + i;
            bool visible = count > 0 && (navigation || index < count);
            button.gameObject.SetActive(visible);
            if (!visible) continue;

            button.interactable = true;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null && chineseFont != null) label.font = chineseFont;
            if (navigation)
            {
                int pageCount = (count + pageSize - 1) / pageSize;
                if (label != null) label.text = $"下一页（{choicePage + 1}/{pageCount}）";
                button.onClick.AddListener(() =>
                {
                    choicePage = (choicePage + 1) % pageCount;
                    RenderChoices();
                });
            }
            else
            {
                var option = currentChoices[index];
                if (label != null) label.text = option.text;
                button.onClick.AddListener(() => option.onClick?.Invoke());
            }
        }
        optionsRoot.SetActive(count > 0);
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
