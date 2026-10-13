using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个关卡的全部导表产物。由编辑器从 Data/{level} 下的三张 CSV 生成。
/// </summary>
public sealed class LevelDatabase : ScriptableObject
{
    [SerializeField] private string startDialogueId;
    [SerializeField] private List<ItemData> clues = new();
    [SerializeField] private List<DialogueData> dialogues = new();
    [SerializeField] private List<QuestionData> questions = new();

    // 非序列化字段：ScriptableObject 反序列化时为 null，且不能用 readonly（要在 BuildIndex 里兜底创建）。
    private Dictionary<string, ItemData> clueMap = new();
    private Dictionary<string, DialogueData> dialogueMap = new();
    private Dictionary<string, QuestionData> questionMap = new();

    public string StartDialogueId => startDialogueId;

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
