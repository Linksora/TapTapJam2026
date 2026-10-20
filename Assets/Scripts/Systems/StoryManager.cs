using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 关卡剧情总控（阶段状态机）：
/// 1) 对话阶段：对话对象持续在场，轮到主角说话时仍保留对方；
/// 2) 搜证阶段（对话行配了 SearchClueIDs）：隐藏对话内容与对手，保留主角，自由点场景取证，
///    物品栏集齐所需线索后自动跳到 NextTarget 推进剧情；
/// 3) 质询阶段（Q_ 题且 Options 为空）：对话框里的 Character 沉默在场，
///    玩家把笔记本证物拖到立绘或对话区域，拖错回应后回到等待，拖对进入结果；
/// 4) 有 Options 的 Q_ 题仍是拼凑选择题：选项判定、发线索、错误回流；
/// 5) 对话选项里的 "PRESENT:角色ID" 仍可作为点选式出示的备用入口。
/// </summary>
public sealed partial class StoryManager : MonoBehaviour
{
    private const string DefaultLevelId = "Level_01";
    private const string PresentPrefix = "PRESENT:";
    private const string ScenePrefix = "SCENE:";

    [SerializeField] private string overrideLevelId;
    [SerializeField] private bool playOnStart = true;

    private LevelDatabase database;
    private DialogueBoxView dialogueBox;
    private InventoryManager inventory;
    private BackgroundController background;
    private OpponentStage opponents;
    private SearchStage searchStage;
    private NotebookPanel notebook;

    private string pendingNextTarget;
    private string currentDialogueId;
    private string waitQuestionId;
    private bool inConfrontationWait;
    private float dialogueHoldUntil;

    // 搜证阶段状态
    private bool inSearchPhase;
    private HashSet<string> searchRequiredClues;
    private HashSet<string> searchAvailableClues;
    private string searchCompleteTarget;
    private bool searchAutoCompletes = true;

    public string LevelId { get; private set; }
    public bool IsSearching => inSearchPhase;
    public bool CanShowInvestigationCursor => inSearchPhase || inConfrontationWait;

    public ItemData GetClue(string clueId) => database != null ? database.GetClue(clueId) : null;

    // 必需证据决定剧情推进；可选证据在同一搜证阶段也可以收集。
    public bool CanCollectClue(string clueId) => inSearchPhase && searchAvailableClues != null
        && searchAvailableClues.Contains(clueId);

    private void Start()
    {
        LevelId = ResolveLevelId();
        database = Resources.Load<LevelDatabase>($"LevelData/{LevelId}/LevelDatabase");
        if (database == null)
        {
            Debug.LogError($"[StoryManager] 找不到关卡数据 Resources/LevelData/{LevelId}/LevelDatabase，" +
                           "请先执行 TapJam/Import All Level CSV。");
            enabled = false;
            return;
        }

        // 包含旧场景中默认 inactive 的 DialogueBox。
        dialogueBox = FindFirstObjectByType<DialogueBoxView>(FindObjectsInactive.Include);
        if (dialogueBox == null)
        {
            Debug.LogError("[StoryManager] 场景中没有 DialogueBoxView。");
            enabled = false;
            return;
        }

        // 点击对话框本体继续（运行时挂 Button，不改动 UI 结构）。
        Button clickCatcher = dialogueBox.GetComponent<Button>();
        if (clickCatcher == null)
        {
            clickCatcher = dialogueBox.gameObject.AddComponent<Button>();
            clickCatcher.transition = Selectable.Transition.None;
        }

        clickCatcher.onClick.AddListener(OnBoxClicked);
        if (LevelId == "Level_03") dialogueBox.MainCharacterClicked += OnExplorationCharacterClicked;
        dialogueBox.gameObject.SetActive(true);
        dialogueBox.Hide();

        inventory = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
        if (inventory != null)
        {
            inventory.ItemsChanged += OnInventoryChanged;
        }

        background = FindFirstObjectByType<BackgroundController>(FindObjectsInactive.Include);
        if (background == null)
        {
            // 场景未摆放时运行时自建，按约定名字查找背景图。
            background = gameObject.AddComponent<BackgroundController>();
        }

        opponents = OpponentStage.Ensure(database.CharacterIds);
        searchStage = GetComponent<SearchStage>();
        if (searchStage == null) searchStage = gameObject.AddComponent<SearchStage>();

        if (playOnStart)
        {
            Play(database.StartDialogueId);
        }
    }

    public void Play(string dialogueId)
    {
        Goto(dialogueId);
    }

    /// <summary>
    /// 面质入口：质询等待态把证物拖到角色身上（或对话里点选出示）时调用。
    /// 命中质询题（角色+TargetClue）→ 升级旧证据或发放 SuccessClue，并进成功对话；
    /// 角色有质询题但证物不对 → 进错误回流对话；角色当前没有可面质内容 → 内心一句后回到等待态。
    /// </summary>
    public void StartConfrontation(string characterId, ItemData clue)
    {
        if (database == null || dialogueBox == null || clue == null)
        {
            return;
        }

        // 等待态只判定当前质询题，避免被同角色其他题目提前判成功。
        if (inConfrontationWait)
        {
            QuestionData current = database.GetQuestion(waitQuestionId);
            if (current == null || database.GetInterrogationCharacterId(current) != characterId) return;
            bool correct = current.TargetClue != null && current.TargetClue.ItemId == clue.ItemId;
            if (correct) AwardQuestionClue(current);
            if (correct) CloseNotebookAfterConfrontation();
            string target = correct ? current.SuccessTarget : current.GetWrongTarget(clue.ItemId);
            Goto(string.IsNullOrEmpty(target) && !correct ? waitQuestionId : target);
            return;
        }

        QuestionData matched = database.FindInterrogation(characterId, clue);
        if (matched != null)
        {
            AwardQuestionClue(matched);

            waitQuestionId = null;
            CloseNotebookAfterConfrontation();
            Goto(matched.SuccessTarget);
            return;
        }

        QuestionData anyForCharacter = database.FindInterrogationForCharacter(characterId);
        if (anyForCharacter != null && !string.IsNullOrEmpty(anyForCharacter.WrongTarget))
        {
            Goto(anyForCharacter.WrongTarget);
            return;
        }

        // 这个角色当前没有配置任何面质：一句内心独白，点一下回到沉默等待继续拖。
        dialogueBox.Show(dialogueBox.CurrentMainCharacterId, $"（{clue.ItemName}和{characterId}应该没什么关系……）");
        pendingNextTarget = waitQuestionId;
    }

    private void AwardQuestionClue(QuestionData question)
    {
        if (question?.SuccessClue == null || inventory == null) return;
        if (question.TargetClue != null && question.TargetClue.NextClue == question.SuccessClue)
        {
            if (!inventory.ReplaceItem(question.TargetClue, question.SuccessClue))
                Debug.LogError($"[StoryManager:{LevelId}] 无法升级证据 {question.TargetClue.ItemId} → {question.SuccessClue.ItemId}。", this);
            return;
        }
        inventory.AddItem(question.SuccessClue);
    }

    private string ResolveLevelId()
    {
        if (!string.IsNullOrWhiteSpace(overrideLevelId))
        {
            return overrideLevelId.Trim();
        }

        Match match = Regex.Match(gameObject.scene.name, @"Level_\d+");
        return match.Success ? match.Value : DefaultLevelId;
    }

    private void OnBoxClicked()
    {
        if (delayedTransition != null) return;
        if (Time.unscaledTime < dialogueHoldUntil) return;
        if (inConfrontationWait || !dialogueBox.IsShowing || dialogueBox.IsShowingOptions)
        {
            return;
        }

        Goto(pendingNextTarget);
    }

    private void CloseNotebookAfterConfrontation()
    {
        if (notebook == null) notebook = FindAnyObjectByType<NotebookPanel>();
        if (notebook != null) notebook.Close();
    }

    private void Goto(string targetId, HashSet<string> transitions = null)
    {
        CancelDelayedTransition();
        if (LevelId == "Level_03")
        {
            dialogueBox.SetMainCharacterInteractionEnabled(false);
        }
        if (targetId == "RETURN_SEARCH") { FinishInspection(); return; }
        if (!string.IsNullOrWhiteSpace(targetId) && targetId.StartsWith(ScenePrefix))
        {
            string sceneName = targetId[ScenePrefix.Length..].Trim();
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[StoryManager:{LevelId}] 场景跳转目标为空：{targetId}");
                return;
            }

            SceneManager.LoadScene(sceneName);
            return;
        }
        inConfrontationWait = false;
        dialogueBox.StopWaitingForEvidence();
        // 任何跳转都先退出搜证等待态；进入搜证节点时会重新置位。
        inSearchPhase = false;
        SetScenePanningEnabled(false);
        // 搜证节点 ID 仅在搜证阶段有效；清空后旧世界物品不会再匹配该阶段的规则。
        currentSearchId = null;
        searchRequiredClues = null;
        searchAvailableClues = null;
        navigator?.End();

        if (string.IsNullOrEmpty(targetId) || targetId == "-1")
        {
            dialogueBox.Hide();
            opponents.HideAll();
            pendingNextTarget = null;
            currentDialogueId = null;
            waitQuestionId = null;
            return;
        }

        if (targetId.StartsWith("B_")) { ShowDeduction(targetId); return; }

        if (targetId.StartsWith("Q_"))
        {
            GameAudio.Instance.EnterStoryNode(LevelId, targetId);
            ShowQuestion(targetId);
            return;
        }

        DialogueData dialogue = database.GetDialogue(targetId);
        if (dialogue == null)
        {
            Debug.LogWarning($"[StoryManager:{LevelId}] 找不到对话 {targetId}，剧情结束。");
            dialogueBox.Hide();
            opponents.HideAll();
            pendingNextTarget = null;
            currentDialogueId = null;
            return;
        }

        if (targetId == "D_ST07_INCOMPLETE_01")
        {
            ShowIncompleteSearchHint(dialogue);
            return;
        }

        Debug.Log($"[StoryManager:{LevelId}] 加载对话 {targetId}：speaker='{dialogue.Speaker}'，text='{dialogue.Text}'，next='{dialogue.NextTarget}'。", dialogue);

        currentDialogueId = targetId;
        GameAudio.Instance.EnterStoryNode(LevelId, targetId);

        if (!string.IsNullOrWhiteSpace(dialogue.ConditionGroup)
            && GetStatementChoice(dialogue.ConditionGroup, dialogue.ConditionStatement) == dialogue.ConditionValue)
        { Goto(dialogue.AlternateTarget, transitions); return; }
        if (!string.IsNullOrWhiteSpace(dialogue.MainCharacters))
            dialogueBox.SetMainCharacters(dialogue.MainCharacters.Split('|'));
        if (dialogue.RewardClue != null) inventory?.AddItem(dialogue.RewardClue);
        if (!string.IsNullOrWhiteSpace(dialogue.StageAction))
            StorySceneCast.Run(dialogue.StageAction, gameObject);

        if (!string.IsNullOrEmpty(dialogue.Background))
        {
            if (background != null && dialogue.Background != background.CurrentBackground)
                dialogueBox.ClearConversationPartner();
            background?.Show(dialogue.Background);
        }

        if (dialogue.SearchClueIds.Length > 0 || dialogue.StageAction == "Explore")
        {
            BeginSearchPhase(dialogue);
            return;
        }

        // 玩法/剧本切换标记不作为台词显示，也不额外等待一次点击。
        if (dialogue.IsTransition)
        {
            if (dialogue.HoldSeconds > 0)
            {
                dialogueBox.Hide();
                opponents.HideAll();
                ScheduleDelayedTransition(dialogue);
                return;
            }
            transitions ??= new HashSet<string>();
            if (!transitions.Add(targetId))
            {
                Debug.LogError($"[StoryManager:{LevelId}] 剧本切换节点形成循环：{targetId}");
                dialogueBox.Hide();
                pendingNextTarget = null;
                return;
            }
            Goto(dialogue.NextTarget, transitions);
            return;
        }

        ShowDialogueLine(dialogue);
        Debug.Log($"[StoryManager:{LevelId}] 已显示对话 {targetId}。", dialogueBox);
    }

    /// <summary>普通对话行：更新对手舞台和 Character，MainCharacter 保持当前主角。</summary>
    private void ShowDialogueLine(DialogueData dialogue)
    {
        dialogueHoldUntil = Time.unscaledTime + dialogue.HoldSeconds;
        waitQuestionId = null;
        bool narration = dialogue.Speaker == "系统" || dialogue.Speaker == "旁白";
        // 与说话人同名的路径沿用当前表情；差分路径才切换主角立绘。
        if (dialogue.Speaker == dialogueBox.CurrentMainCharacterId
            && !string.IsNullOrWhiteSpace(dialogue.PortraitPath)
            && dialogue.PortraitPath != dialogue.Speaker)
            dialogueBox.SetMainCharacterPortrait(dialogue.Speaker, PortraitLoader.Load(dialogue.PortraitPath));
        Sprite portrait = narration || dialogue.Speaker == dialogueBox.CurrentMainCharacterId ? null
            : PortraitLoader.Load(string.IsNullOrWhiteSpace(dialogue.PortraitPath)
                ? dialogue.Speaker : dialogue.PortraitPath);

        if (dialogue.HasOptions)
        {
            pendingNextTarget = null;

            // 只收集填了文案的选项，避免空选项渲染成空白按钮。
            var options = new List<(string text, System.Action onClick)>(2);
            if (!string.IsNullOrEmpty(dialogue.Option1Text))
            {
                string target = dialogue.Option1Target;
                options.Add((dialogue.Option1Text, () => HandleOption(target)));
            }
            if (!string.IsNullOrEmpty(dialogue.Option2Text))
            {
                string target = dialogue.Option2Target;
                options.Add((dialogue.Option2Text, () => HandleOption(target)));
            }

            dialogueBox.Show(dialogue.Speaker, dialogue.Text, portrait, options.ToArray());
        }
        else
        {
            pendingNextTarget = dialogue.NextTarget;
            dialogueBox.Show(narration ? string.Empty : dialogue.Speaker, dialogue.Text, portrait);
        }

        opponents.Show(dialogueBox.ConversationPartnerId);
    }

    /// <summary>
    /// 搜证阶段：隐藏对话内容和对手，留在场景里等玩家点 WorldItem；
    /// 所需线索可能已在包里（题目奖励、重复进入），立即完成。
    /// </summary>
    private void BeginSearchPhase(DialogueData dialogue)
    {
        inspectionSearchId = inspectionClueId = null;
        searchAutoCompletes = dialogue.StageAction != "Explore";
        if (!searchAutoCompletes)
        {
            // 完成搜证后的自由查看允许点出口、取消或直接进入下一张背景。
            dialogueBox.ClearConversationPartner();
        }
        currentSearchId = dialogue.DialogueId;
        conversationSearchId = currentSearchId;
        searchRequiredClues = new HashSet<string>(dialogue.SearchClueIds);
        searchAvailableClues = new HashSet<string>(dialogue.SearchClueIds);
        searchAvailableClues.UnionWith(dialogue.OptionalClueIds);
        searchCompleteTarget = dialogue.NextTarget;
        waitQuestionId = null;

        dialogueBox.Hide();
        opponents.HideAll();

        inSearchPhase = true;
        if (LevelId == "Level_03")
        {
            dialogueBox.SetMainCharacterInteractionEnabled(!string.IsNullOrEmpty(dialogue.AlternateTarget));

        }
        SetScenePanningEnabled(true);
        BeginNavigation(dialogue);
        searchStage.BeginSearch(dialogue, database, inventory);
        if (searchAutoCompletes && HasAllSearchClues()) CompleteSearch();
    }

    private void SetScenePanningEnabled(bool enabled)
    {
        foreach (ScenePanner panner in FindObjectsByType<ScenePanner>(FindObjectsInactive.Include))
        {
            if (panner.gameObject.scene == gameObject.scene)
                panner.NavigationMode = !enabled;
        }
    }

    private void ShowIncompleteSearchHint(DialogueData dialogue)
    {
        currentDialogueId = dialogue.DialogueId;
        GameAudio.Instance.EnterStoryNode(LevelId, dialogue.DialogueId);
        string[] required = { "ST02", "ST05", "ST04", "ST03" };
        string[] missing = required
            .Where(id => inventory == null || !inventory.Items.Any(item => item != null && item.ItemId == id))
            .Select(id => database.GetClue(id)?.ItemName ?? id)
            .ToArray();
        string text = dialogue.Text;
        if (missing.Length > 0)
            text += $"还缺少：{string.Join("、", missing)}。";

        pendingNextTarget = null;
        dialogueBox.SelectMainCharacter("小毛");
        Sprite portrait = PortraitLoader.Load(string.IsNullOrWhiteSpace(dialogue.PortraitPath)
            ? dialogue.Speaker : dialogue.PortraitPath);
        dialogueBox.Show(dialogue.Speaker, text, portrait,
            ("继续搜证", () => Goto("D_501")));
        opponents.Show(dialogueBox.ConversationPartnerId);
    }

    private bool HasAllSearchClues()
    {
        if (currentSearchId == "D_501")
        {
            if (inventory == null) return false;
            string[] confrontationClues = { "ST07", "ST02", "ST05", "ST04", "ST03" };
            return confrontationClues.All(requiredId =>
                inventory.Items.Any(item => item != null && item.ItemId == requiredId));
        }

        if (searchRequiredClues == null || searchRequiredClues.Count == 0)
        {
            return true;
        }

        if (inventory == null)
        {
            return false;
        }

        return searchRequiredClues.All(requiredId =>
            inventory.Items.Any(item => item != null && item.ItemId == requiredId));
    }

    private void OnInventoryChanged()
    {
        // FinishInspection owns the final clue transition. Don't complete the
        // search re-entrantly while it is adding that clue and showing its receipt.
        if (!searchAutoCompletes || !inSearchPhase || !string.IsNullOrEmpty(inspectionSearchId))
        {
            return;
        }

        if (HasAllSearchClues())
        {
            CompleteSearch();
        }
    }

    private void HandleOption(string target)
    {
        if (target == "RESUME_SEARCH" && !string.IsNullOrEmpty(conversationSearchId))
        {
            Goto(conversationSearchId);
            return;
        }
        if (target.StartsWith("INSPECT:"))
        {
            BeginConversationInspection(target);
            return;
        }
        if (target.StartsWith(PresentPrefix))
        {
            ShowPresentChoices(target[PresentPrefix.Length..]);
            return;
        }

        Goto(target);
    }

    /// <summary>对话框内的“出示证物”：把物品栏证物列成选项，加一个取消项回到原对话行。</summary>
    private void ShowPresentChoices(string characterId)
    {
        string who = string.IsNullOrEmpty(characterId) ? "对方" : characterId;
        string returnTarget = inConfrontationWait ? waitQuestionId : currentDialogueId;

        if (inventory == null || inventory.Items.Count == 0)
        {
            dialogueBox.Show(dialogueBox.CurrentMainCharacterId, "（还没有可以出示的证物。先去店里调查一下吧。）", null,
                ("返回", () => Goto(returnTarget)));
            return;
        }

        var options = new List<(string text, System.Action onClick)>(inventory.Items.Count + 1);
        foreach (ItemData item in inventory.Items)
        {
            if (item == null)
            {
                continue;
            }

            options.Add((item.ItemName, () => StartConfrontation(characterId, item)));
        }

        options.Add(("（先不出示）", () => Goto(returnTarget)));

        dialogueBox.Show(dialogueBox.CurrentMainCharacterId, $"要向{who}出示哪件证物？", null, options.ToArray());
    }

    private void ShowQuestion(string questionId)
    {
        QuestionData question = database.GetQuestion(questionId);
        if (question == null)
        {
            Debug.LogWarning($"[StoryManager:{LevelId}] 找不到题目 {questionId}，剧情结束。");
            dialogueBox.Hide();
            opponents.HideAll();
            return;
        }

        currentDialogueId = null;
        pendingNextTarget = null;

        if (question.IsInterrogation)
        {
            EnterConfrontationWait(questionId, question);
            return;
        }

        // 独立拼凑题不延续上一段交谈对象，选项使用场景中的 Options。
        waitQuestionId = null;
        opponents.HideAll();
        dialogueBox.ClearConversationPartner();

        var options = new List<(string text, System.Action onClick)>(question.Options.Count);
        for (int i = 0; i < question.Options.Count; i++)
        {
            int chosen = i;
            options.Add((question.Options[i], () => AnswerAssembly(question, chosen)));
        }

        dialogueBox.Show(dialogueBox.CurrentMainCharacterId, question.Statement.Replace("{0}", "______"), null, options.ToArray());
    }

    /// <summary>质询等待态：沿用对话 UI，对方沉默，等待笔记本里的证物。</summary>
    private void EnterConfrontationWait(string questionId, QuestionData question)
    {
        string who = database.GetInterrogationCharacterId(question);
        if (string.IsNullOrWhiteSpace(who))
        {
            Debug.LogError($"[StoryManager:{LevelId}] 质询题 {questionId} 缺少 CharacterID，请重新导入关卡 CSV。");
            dialogueBox.Hide();
            waitQuestionId = null;
            return;
        }

        opponents.HideAll();
        waitQuestionId = questionId;
        inConfrontationWait = true;
        string portraitPath = string.IsNullOrWhiteSpace(question.HesitantPortraitPath)
            ? who : question.HesitantPortraitPath;
        if (LevelId == "Level_03")
        {
            if (questionId.StartsWith("Q_Letter") && who == "守宫妹妹")
                dialogueBox.Show(who, question.Statement, PortraitLoader.Load(portraitPath),
                    ("出示证据", () => ShowPresentChoices(who)),
                    ("先休息", () => ShowLetterBreak(questionId)));
            else
                dialogueBox.Show(who, question.Statement, PortraitLoader.Load(portraitPath),
                    ("出示证据", () => ShowPresentChoices(who)));
            return;
        }
        dialogueBox.ShowConfrontation(who, PortraitLoader.Load(portraitPath), question.Statement,
            clue => StartConfrontation(who, clue));
    }

    private void ShowLetterBreak(string questionId, string activity = null)
    {
        // 题号由按钮闭包保留；休息不跳到下一题，也不重置证词和选词。
        GetComponent<ChapterThreeLetterPresentation>()?.CloseDisplay();
        inConfrontationWait = false;
        if (activity == null)
        {
            dialogueBox.Show("小毛", "先休息一会儿，不着急。", null,
                ("喝点水", () => ShowLetterBreak(questionId, "水在这里。等你愿意，我们再继续。")),
                ("和松鼠聊两句", () => ShowLetterBreak(questionId, "松鼠说这里可以慢慢坐，有需要就告诉她。")));
            return;
        }
        dialogueBox.Show("小毛", activity + "\n现在愿意接着聊吗？", null,
            ("妹妹点头，继续", () => Goto(questionId)),
            ("再休息一会儿", () => ShowLetterBreak(questionId)));
    }

    private void AnswerAssembly(QuestionData question, int chosenIndex)
    {
        if (chosenIndex == question.CorrectOption)
        {
            AwardQuestionClue(question);

            Goto(question.SuccessTarget);
        }
        else
        {
            Goto(question.WrongTarget);
        }
    }
}
