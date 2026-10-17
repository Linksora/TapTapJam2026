using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡剧情总控（阶段状态机）：
/// 1) 对话阶段：谁说话，world 里同名角色立绘就出现在对话框对面；主角内心独白时立绘隐藏；
/// 2) 搜证阶段（对话行配了 SearchClueIDs）：关闭对话框、隐藏角色，玩家自由点场景取证，
///    物品栏集齐所需线索后自动跳到 NextTarget 推进剧情；
/// 3) 质询阶段（Q_ 题且 Options 为空）：目标角色沉默在场、不弹对话框，
///    玩家把背包证物拖到角色身上——拖错播对方台词后回到沉默等待，拖对进入质询结果；
/// 4) 有 Options 的 Q_ 题仍是拼凑选择题：选项判定、发线索、错误回流；
/// 5) 对话选项里的 "PRESENT:角色ID" 仍可作为点选式出示的备用入口。
/// </summary>
public sealed class StoryManager : MonoBehaviour
{
    private const string DefaultLevelId = "Level_01";
    private const string PresentPrefix = "PRESENT:";

    [SerializeField] private string overrideLevelId;
    [SerializeField] private bool playOnStart = true;

    private LevelDatabase database;
    private DialogueBoxView dialogueBox;
    private InventoryManager inventory;
    private BackgroundController background;
    private OpponentStage opponents;

    private string pendingNextTarget;
    private string currentDialogueId;
    private string waitQuestionId;

    // 搜证阶段状态
    private bool inSearchPhase;
    private HashSet<string> searchRequiredClues;
    private string searchCompleteTarget;

    public string LevelId { get; private set; }

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

        opponents = OpponentStage.Ensure();

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
    /// 命中质询题（角色+TargetClue）→ 发放 SuccessClue 并进成功对话；
    /// 角色有质询题但证物不对 → 进错误回流对话；角色当前没有可面质内容 → 内心一句后回到等待态。
    /// </summary>
    public void StartConfrontation(string characterId, ItemData clue)
    {
        if (database == null || dialogueBox == null || clue == null)
        {
            return;
        }

        QuestionData matched = database.FindInterrogation(characterId, clue);
        if (matched != null)
        {
            if (matched.SuccessClue != null && inventory != null)
            {
                inventory.AddItem(matched.SuccessClue);
            }

            waitQuestionId = null;
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
        if (!dialogueBox.IsShowing || dialogueBox.IsShowingOptions)
        {
            return;
        }

        Goto(pendingNextTarget);
    }

    private void Goto(string targetId)
    {
        // 任何跳转都先退出搜证等待态；进入搜证节点时会重新置位。
        inSearchPhase = false;
        searchRequiredClues = null;

        if (string.IsNullOrEmpty(targetId) || targetId == "-1")
        {
            dialogueBox.Hide();
            opponents.HideAll();
            pendingNextTarget = null;
            currentDialogueId = null;
            waitQuestionId = null;
            return;
        }

        if (targetId.StartsWith("Q_"))
        {
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

        currentDialogueId = targetId;

        if (!string.IsNullOrEmpty(dialogue.Background))
        {
            background?.Show(dialogue.Background);
        }

        if (dialogue.SearchClueIds.Length > 0)
        {
            BeginSearchPhase(dialogue);
            return;
        }

        ShowDialogueLine(dialogue);
    }

    /// <summary>普通对话行：更新对手舞台和 Character，MainCharacter 保持当前主角。</summary>
    private void ShowDialogueLine(DialogueData dialogue)
    {
        waitQuestionId = null;
        opponents.Show(dialogue.Speaker);
        Sprite portrait = dialogue.Speaker == dialogueBox.CurrentMainCharacterId ? null
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
            dialogueBox.Show(dialogue.Speaker, dialogue.Text, portrait);
        }
    }

    /// <summary>
    /// 搜证阶段：关对话框、隐藏角色，留在场景里等玩家点 WorldItem；
    /// 所需线索可能已在包里（题目奖励、重复进入），立即完成。
    /// </summary>
    private void BeginSearchPhase(DialogueData dialogue)
    {
        searchRequiredClues = new HashSet<string>(dialogue.SearchClueIds);
        searchCompleteTarget = dialogue.NextTarget;
        waitQuestionId = null;

        dialogueBox.Hide();
        opponents.HideAll();

        if (HasAllSearchClues())
        {
            Goto(searchCompleteTarget);
            return;
        }

        inSearchPhase = true;
    }

    private bool HasAllSearchClues()
    {
        if (searchRequiredClues == null || searchRequiredClues.Count == 0)
        {
            return true;
        }

        if (inventory == null)
        {
            return false;
        }

        foreach (ItemData item in inventory.Items)
        {
            if (item != null && searchRequiredClues.Contains(item.ItemId))
            {
                searchRequiredClues.Remove(item.ItemId);
            }
        }

        return searchRequiredClues.Count == 0;
    }

    private void OnInventoryChanged()
    {
        if (!inSearchPhase)
        {
            return;
        }

        if (HasAllSearchClues())
        {
            string target = searchCompleteTarget;
            inSearchPhase = false;
            searchRequiredClues = null;
            Goto(target);
        }
    }

    private void HandleOption(string target)
    {
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

        if (inventory == null || inventory.Items.Count == 0)
        {
            dialogueBox.Show(dialogueBox.CurrentMainCharacterId, "（还没有可以出示的证物。先去店里调查一下吧。）", null,
                ("返回", () => Goto(currentDialogueId)));
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

        options.Add(("（先不出示）", () => Goto(currentDialogueId)));

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

        // 拼凑选填题：题干里的 {0} 用空位代替，选项纵向列出。
        waitQuestionId = null;
        opponents.HideAll();

        var options = new List<(string text, System.Action onClick)>(question.Options.Count);
        for (int i = 0; i < question.Options.Count; i++)
        {
            int chosen = i;
            options.Add((question.Options[i], () => AnswerAssembly(question, chosen)));
        }

        dialogueBox.Show(dialogueBox.CurrentMainCharacterId, question.Statement.Replace("{0}", "______"), null, options.ToArray());
    }

    /// <summary>质询等待态：对方立绘出现、沉默、对话框不弹，等玩家把证物拖到她身上。</summary>
    private void EnterConfrontationWait(string questionId, QuestionData question)
    {
        string who = string.IsNullOrEmpty(question.CharacterId) ? string.Empty : question.CharacterId;

        bool shown = opponents.Show(who);
        if (!shown)
        {
            Debug.LogWarning($"[StoryManager:{LevelId}] 质询题 {questionId} 的角色“{who}”" +
                             "在 world 下找不到同名立绘物体，拖拽面质无法进行。");
        }

        dialogueBox.Hide();
        waitQuestionId = questionId;
        opponents.SetDropArmed(true, clue => StartConfrontation(who, clue));
    }

    private void AnswerAssembly(QuestionData question, int chosenIndex)
    {
        if (chosenIndex == question.CorrectOption)
        {
            if (question.SuccessClue != null && inventory != null)
            {
                inventory.AddItem(question.SuccessClue);
            }

            Goto(question.SuccessTarget);
        }
        else
        {
            Goto(question.WrongTarget);
        }
    }
}
