using System.Linq;
using UnityEngine;

public sealed partial class StoryManager
{
    private string currentSearchId;
    private string inspectionSearchId;
    private string inspectionClueId;
    private ExplorationNavigator navigator;

    public bool TryNavigateBackground(string destination)
    {
        if (!inSearchPhase) return true;
        navigator?.TryGo(destination);
        return true;
    }

    private WorldInteractionData FindInteraction(WorldItem item)
    {
        if (database == null || string.IsNullOrEmpty(currentSearchId)) return null;
        return database.WorldInteractions.FirstOrDefault(rule => rule.worldPath == item.WorldPath && rule.searchNodeId == currentSearchId);
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
        if (collected && item.GetComponent<BackgroundSwitch>() != null) return true;
        if (!string.IsNullOrEmpty(rule.requiredCharacter) && dialogueBox.CurrentMainCharacterId != rule.requiredCharacter)
        {
            searchStage.ShowFeedback($"这个调查点需要{rule.requiredCharacter}。点击主角头像旁的箭头切换角色。");
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
        string phase = inspectionSearchId;
        string clue = inspectionClueId;
        inspectionSearchId = inspectionClueId = null;
        if (string.IsNullOrEmpty(phase)) return;
        ItemData recorded = string.IsNullOrEmpty(clue) ? null : database.GetClue(clue);
        if (recorded != null) inventory.AddItem(recorded);
        if (recorded != null)
        {
            DialogueData searchDialogue = database.GetDialogue(phase);
            string[] required = searchDialogue != null ? searchDialogue.SearchClueIds : System.Array.Empty<string>();
            bool allCollected = required.Length > 0 && required.All(id =>
                inventory.Items.Any(item => item != null && item.ItemId == id));
            pendingNextTarget = allCollected && !string.IsNullOrEmpty(searchCompleteTarget)
                ? searchCompleteTarget : phase;
            int collectedCount = required.Count(id =>
                inventory.Items.Any(item => item != null && item.ItemId == id));
            Debug.Log($"[StoryManager:{LevelId}] 证据 {clue} 已入册；搜证 {phase} 必需线索 {collectedCount}/{required.Length}，继续节点 {pendingNextTarget}。", this);
            dialogueBox.Show(dialogueBox.CurrentMainCharacterId, $"【已获得证据：{recorded.ItemName}】");
            return;
        }
        Goto(phase);
    }

    public void CompleteSearch()
    {
        if (!inSearchPhase || !HasAllSearchClues()) return;
        Debug.Log($"[StoryManager:{LevelId}] 必需证据已收齐，搜证节点 {currentSearchId} 推进到 {searchCompleteTarget}。", this);
        dialogueBox.SelectMainCharacter("小毛");
        if (currentSearchId == "D_501")
        {
            background.Show("学校侧院");
            StorySceneCast.ShowYard(background.CurrentRoot, true);
        }
        Goto(searchCompleteTarget);
    }

    private void BeginNavigation(DialogueData dialogue)
    {
        navigator = GetComponent<ExplorationNavigator>();
        if (navigator == null) navigator = gameObject.AddComponent<ExplorationNavigator>();
        navigator.Begin(database, this, background, dialogue.DialogueId, dialogue.SearchStartZone);
    }
}
