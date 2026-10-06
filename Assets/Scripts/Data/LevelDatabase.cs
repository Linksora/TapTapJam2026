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

    private readonly Dictionary<string, ItemData> clueMap = new();
    private readonly Dictionary<string, DialogueData> dialogueMap = new();
    private readonly Dictionary<string, QuestionData> questionMap = new();

    public string StartDialogueId => startDialogueId;

    private void OnEnable()
    {
        BuildIndex();
    }

    public void BuildIndex()
    {
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
