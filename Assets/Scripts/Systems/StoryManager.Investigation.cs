using System.Linq;
using UnityEngine;

public sealed partial class StoryManager
{
    private string currentSearchId;
    private string inspectionSearchId;
    private string inspectionClueId;
    private ExplorationNavigator navigator;
    private string conversationSearchId;

    private void OnExplorationCharacterClicked()
    {
        if (!inSearchPhase || dialogueBox.CurrentMainCharacterId != "小毛") return;
        // 自由查看节点可配置再次询问的目标；对话期间及其他阶段点击不会跳过剧情。
        DialogueData exploration = database.GetDialogue(currentSearchId);
        if (!string.IsNullOrEmpty(exploration?.AlternateTarget))
        {
            conversationSearchId = currentSearchId;
            Goto(exploration.AlternateTarget);
        }
    }

    private void BeginConversationInspection(string target)
    {
        string[] parts = target.Split(new[] { ':' }, 3);
        DialogueData phase = database.GetDialogue(conversationSearchId);
        if (parts.Length != 3 || phase == null || database.GetDialogue(parts[2]) == null
            || !phase.SearchClueIds.Concat(phase.OptionalClueIds).Contains(parts[1]))
        {
            Debug.LogError($"对话调查入口无效或不属于当前阶段：{target}", this);
            return;
        }
        inspectionSearchId = conversationSearchId;
        inspectionClueId = parts[1];
        if (inventory.Items.Any(item => item != null && item.ItemId == parts[1]))
        {
            inspectionClueId = null;
            pendingNextTarget = "RETURN_SEARCH";
            dialogueBox.Show("小毛", "这部分已经核对过了。");
            return;
        }
        Goto(parts[2]);
    }

    private void OnDestroy()
    {
        if (dialogueBox != null) dialogueBox.MainCharacterClicked -= OnExplorationCharacterClicked;
        if (inventory != null) inventory.ItemsChanged -= OnInventoryChanged;
    }

    public bool TryNavigateBackground(string destination)
    {
        if (!inSearchPhase) return true;
        navigator?.TryGo(destination);
        return true;
    }

    private WorldInteractionData FindInteraction(WorldItem item)
    {
        if (database == null || string.IsNullOrEmpty(currentSearchId)) return null;
        var matches = database.WorldInteractions
            .Where(rule => rule.worldPath == item.WorldPath && rule.searchNodeId == currentSearchId)
            .ToArray();

        // One world object can represent more than one clue. Prefer the next
        // uncollected interaction, then keep a collected match so TryInspect
        // can suppress the click instead of falling back to generic pickup.
        return matches.FirstOrDefault(rule =>
                   inventory == null || !inventory.Items.Any(clue => clue != null && clue.ItemId == rule.clueId))
               ?? matches.FirstOrDefault();
    }

    public bool ShouldInspectBeforeTravel(GameObject target)
    {
        var item = target.GetComponent<WorldItem>();
        var rule = item != null ? FindInteraction(item) : null;
        return rule != null && !inventory.Items.Any(clue => clue.ItemId == rule.clueId);
    }

    public bool TryInspect(WorldItem item)
    {
        var rule = FindInteraction(item);
        if (rule == null) return false;
        if (!inSearchPhase)
        {
            Debug.LogWarning($"[StoryManager:{LevelId}] 调查物 {item.WorldPath} 匹配到 {rule.dialogueId}，但当前不在搜证阶段（当前节点：{currentSearchId ?? "无"}）。", item);
            return true;
        }
        bool collected = inventory.Items.Any(clue => clue.ItemId == rule.clueId);
        // The same clue may be exposed by multiple world objects or search phases.
        // Once it is in the notebook, don't replay its inspection dialogue.
        if (collected) return true;
        if (!string.IsNullOrEmpty(rule.requiredCharacter) && dialogueBox.CurrentMainCharacterId != rule.requiredCharacter)
        {
            ShowSearchNarration($"这个调查点需要{rule.requiredCharacter}。点击主角头像旁的箭头切换角色。");
            return true;
        }
        inspectionSearchId = currentSearchId;
        inspectionClueId = rule.clueId;
        Debug.Log($"[StoryManager:{LevelId}] 点击调查物 {item.WorldPath}，进入对话 {rule.dialogueId} 并记录证据 {rule.clueId}。", item);
        Goto(rule.dialogueId);
        return true;
    }

    private void FinishInspection()
    {
        GetComponent<ChapterThreeLetterPresentation>()?.CloseDisplay();
        if (LevelId == "Level_03") dialogueBox.ClearConversationPartner();
        var homePresentation = GetComponent<ChapterThreeHomePresentation>();
        if (homePresentation != null)
        {
            homePresentation.EndInspection();
            dialogueBox.ClearConversationPartner();
        }
        string phase = inspectionSearchId;
        string clue = inspectionClueId;
        if (string.IsNullOrEmpty(phase))
        {
            inspectionSearchId = inspectionClueId = null;
            return;
        }

        ItemData recorded = string.IsNullOrEmpty(clue) ? null : database.GetClue(clue);
        if (recorded != null) inventory.AddItem(recorded);
        inspectionSearchId = inspectionClueId = null;
        if (recorded != null)
        {
            DialogueData searchDialogue = database.GetDialogue(phase);
            string[] required = searchDialogue != null ? searchDialogue.SearchClueIds : System.Array.Empty<string>();
            bool allCollected = phase == currentSearchId
                ? HasAllSearchClues()
                : required.Length > 0 && required.All(id =>
                    inventory.Items.Any(item => item != null && item.ItemId == id));
            // Pause world controls while the evidence receipt and any follow-up
            // line are on screen. The next search node re-enables navigation.
            inSearchPhase = false;
            SetScenePanningEnabled(false);
            navigator?.End();
            if (allCollected)
            {
                dialogueBox.SelectMainCharacter("小毛");
                if (phase == "D_501") background?.Show("学校侧院");
            }
            pendingNextTarget = allCollected && !string.IsNullOrEmpty(searchCompleteTarget)
                ? searchCompleteTarget
                : phase == "D_501" && clue == "ST07"
                    ? "D_ST07_INCOMPLETE_01"
                    : phase;
            int collectedCount = required.Count(id =>
                inventory.Items.Any(item => item != null && item.ItemId == id));
            Debug.Log($"[StoryManager:{LevelId}] 证据 {clue} 已入册；搜证 {phase} 必需线索 {collectedCount}/{required.Length}，继续节点 {pendingNextTarget}。", this);
            dialogueBox.Show(dialogueBox.CurrentMainCharacterId, $"【已获得证据：{recorded.ItemName}】");
            // 最后一张调查证据入册后，计时由系统启动，不需要再点一次提示。
            DialogueData followUp = allCollected ? database.GetDialogue(searchCompleteTarget) : null;
            if (followUp != null && followUp.IsTransition && followUp.HoldSeconds > 0)
                ScheduleDelayedTransition(followUp);
            return;
        }
        Goto(phase);
    }

    private void ShowSearchNarration(string text)
    {
        string resumeSearch = currentSearchId;
        inSearchPhase = false;
        SetScenePanningEnabled(false);
        pendingNextTarget = resumeSearch;
        dialogueBox.Show(string.Empty, text);
    }

    public void CompleteSearch()
    {
        if (!searchAutoCompletes || !inSearchPhase || !HasAllSearchClues()) return;
        Debug.Log($"[StoryManager:{LevelId}] 必需证据已收齐，搜证节点 {currentSearchId} 推进到 {searchCompleteTarget}。", this);
        dialogueBox.SelectMainCharacter("小毛");
        if (currentSearchId == "D_501") background.Show("学校侧院");
        Goto(searchCompleteTarget);
    }

    private void BeginNavigation(DialogueData dialogue)
    {
        navigator = GetComponent<ExplorationNavigator>();
        if (navigator == null) navigator = gameObject.AddComponent<ExplorationNavigator>();
        navigator.Begin(database, this, background, dialogue.DialogueId, dialogue.SearchStartZone);
    }
}
