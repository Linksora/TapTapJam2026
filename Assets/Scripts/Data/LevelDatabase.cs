using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个关卡的全部导表产物。由编辑器从 Data/{level} 下的剧情、证据、质询、拼凑、物件交互和背景表生成。
/// </summary>
public sealed class LevelDatabase : ScriptableObject
{
    [SerializeField] private string startDialogueId;
    [SerializeField] private List<ItemData> clues = new();
    [SerializeField] private List<DialogueData> dialogues = new();
    [SerializeField] private List<QuestionData> questions = new();
    [SerializeField] private List<DeductionData> deductions = new();
    [SerializeField] private List<WorldInteractionData> worldInteractions = new();
    [SerializeField] private List<ExplorationZoneData> zones = new();
    public IReadOnlyList<ExplorationZoneData> Zones => zones;

    public IReadOnlyList<WorldInteractionData> WorldInteractions => worldInteractions;
    public DeductionData GetDeduction(string id) => deductions?.Find(group => group != null && group.groupId == id);

    // 非序列化字段：ScriptableObject 反序列化时为 null，且不能用 readonly（要在 BuildIndex 里兜底创建）。
    private Dictionary<string, ItemData> clueMap = new();
    private Dictionary<string, DialogueData> dialogueMap = new();
    private Dictionary<string, QuestionData> questionMap = new();

    public string StartDialogueId => startDialogueId;

    public IEnumerable<string> CharacterIds
    {
        get
        {
            foreach (DialogueData dialogue in dialogues)
                if (dialogue != null && !string.IsNullOrWhiteSpace(dialogue.Speaker))
                    yield return dialogue.Speaker;
            foreach (QuestionData question in questions)
                if (question != null && !string.IsNullOrWhiteSpace(question.CharacterId))
                    yield return question.CharacterId;
        }
    }

    private void OnEnable()
    {
        BuildIndex();
    }

    public void BuildIndex()
    {
        // ScriptableObject 反序列化时不跑字段初始化器，这里必须按需 new，不能直接 Clear。
        clueMap ??= new Dictionary<string, ItemData>();
        dialogueMap ??= new Dictionary<string, DialogueData>();
        questionMap ??= new Dictionary<string, QuestionData>();
        clueMap.Clear();
        dialogueMap.Clear();
        questionMap.Clear();

        if (clues != null)
        {
            foreach (ItemData clue in clues)
            {
                if (clue != null && !string.IsNullOrEmpty(clue.ItemId))
                {
                    clueMap[clue.ItemId] = clue;
                }
            }
        }

        if (dialogues != null)
        {
            foreach (DialogueData dialogue in dialogues)
            {
                if (dialogue != null && !string.IsNullOrEmpty(dialogue.DialogueId))
                {
                    dialogueMap[dialogue.DialogueId] = dialogue;
                }
            }
        }

        if (questions != null)
        {
            foreach (QuestionData question in questions)
            {
                if (question != null && !string.IsNullOrEmpty(question.QuestionId))
                {
                    questionMap[question.QuestionId] = question;
                }
            }
        }
    }

    public ItemData GetClue(string id)
    {
        return string.IsNullOrEmpty(id) ? null : clueMap.GetValueOrDefault(id);
    }

    public DialogueData GetDialogue(string id)
    {
        return string.IsNullOrEmpty(id) ? null : dialogueMap.GetValueOrDefault(id);
    }

    public QuestionData GetQuestion(string id)
    {
        return string.IsNullOrEmpty(id) ? null : questionMap.GetValueOrDefault(id);
    }

    /// <summary>
    /// 质询命中：把某条证物拖到某角色身上时，找“该角色 + 目标证物完全匹配”的质询题。
    /// 题目的 CharacterID 为空表示不限定角色。
    /// </summary>
    public QuestionData FindInterrogation(string characterId, ItemData clue)
    {
        if (clue == null)
        {
            return null;
        }

        foreach (QuestionData question in questions)
        {
            if (question == null || !question.IsInterrogation || question.TargetClue != clue)
            {
                continue;
            }

            string who = GetInterrogationCharacterId(question);
            if (string.IsNullOrEmpty(who) || who == characterId)
            {
                return question;
            }
        }

        return null;
    }

    /// <summary>该角色身上是否挂着任何质询题（拖错证物时给出该题的错误回流对话）。</summary>
    public QuestionData FindInterrogationForCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return null;
        }

        foreach (QuestionData question in questions)
        {
            if (question != null && question.IsInterrogation && GetInterrogationCharacterId(question) == characterId)
            {
                return question;
            }
        }

        return null;
    }

    /// <summary>兼容旧生成数据：CharacterID 缺失时，从已知角色立绘名或错误回应的说话人恢复。</summary>
    public string GetInterrogationCharacterId(QuestionData question)
    {
        if (question == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(question.CharacterId)) return question.CharacterId.Trim();
        foreach (DialogueData dialogue in dialogues)
            if (dialogue != null && !string.IsNullOrWhiteSpace(question.HesitantPortraitPath)
                && dialogue.Speaker == question.HesitantPortraitPath)
                return dialogue.Speaker;
        DialogueData response = GetDialogue(question.WrongTarget);
        return response != null ? response.Speaker : string.Empty;
    }

    public DialogueData FindEntryDialogue(ItemData clue)
    {
        if (clue == null)
        {
            return null;
        }

        foreach (DialogueData dialogue in dialogues)
        {
            if (dialogue != null && dialogue.EntryClue == clue)
            {
                return dialogue;
            }
        }

        return null;
    }
}
