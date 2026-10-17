using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class StoryManager
{
    private readonly Dictionary<string, string[]> deductionChoices = new();
    private readonly Dictionary<string, bool[]> deductionErrors = new();
    private DeductionData activeDeduction;
    private int activeDeductionRow;

    private string GetStatementChoice(string groupId, string statementId)
    {
        DeductionData group = database.GetDeduction(groupId);
        if (group == null || !deductionChoices.TryGetValue(groupId, out var choices)) return null;
        int index = Array.FindIndex(group.statements, statement => statement.id == statementId);
        return index >= 0 ? choices[index] : null;
    }

    private void ShowDeduction(string id)
    {
        activeDeduction = database.GetDeduction(id);
        if (activeDeduction == null) { Debug.LogError($"找不到整理题目 {id}"); return; }
        foreach (string clue in activeDeduction.requiredClueIds)
            if (!inventory.Items.Any(item => item.ItemId == clue))
            { Debug.LogWarning($"{id} 需要补充证词 {clue}，尚不能确认真相。"); return; }
        if (!deductionChoices.ContainsKey(id))
        {
            var selected = new string[activeDeduction.statements.Length];
            if (!string.IsNullOrEmpty(activeDeduction.initialFromGroup)
                && deductionChoices.TryGetValue(activeDeduction.initialFromGroup, out var previous))
                Array.Copy(previous, selected, Mathf.Min(previous.Length, selected.Length));
            deductionChoices[id] = selected;
            deductionErrors[id] = new bool[selected.Length];
        }
        activeDeductionRow = 0;
        RenderDeduction();
    }

    private void RenderDeduction()
    {
        opponents.HideAll();
        RenderSequentialDeduction();
    }

    private void RenderSequentialDeduction()
    {
        string[] selected = deductionChoices[activeDeduction.groupId];
        if (activeDeductionRow >= activeDeduction.statements.Length)
        {
            string next = activeDeduction.nextTarget;
            string completionLine = activeDeduction.mode == "Hypothesis"
                ? "四句话已经填好。"
                : activeDeduction.mode == "Truth" ? "四个关键词都已核实。" : "问题都整理好了。";
            dialogueBox.Show(dialogueBox.CurrentMainCharacterId, completionLine,
                null, (activeDeduction.confirmText, () =>
                {
                    activeDeduction = null;
                    searchStage?.HideFeedback();
                    Goto(next);
                }));
            return;
        }

        DeductionData.Statement statement = activeDeduction.statements[activeDeductionRow];
        string answer = string.IsNullOrEmpty(selected[activeDeductionRow]) ? "请选择" : selected[activeDeductionRow];
        string title = activeDeductionRow == 0 ? activeDeduction.title + "\n" : string.Empty;
        string prompt = $"{title}（{activeDeductionRow + 1}/{activeDeduction.statements.Length}）\n"
            + statement.sentence.Replace("{0}", $"【{answer}】");
        if (deductionErrors[activeDeduction.groupId][activeDeductionRow])
        {
            int wrongOption = Array.IndexOf(statement.options, selected[activeDeductionRow]);
            string feedback = wrongOption >= 0 && wrongOption < statement.wrongFeedback.Length
                ? statement.wrongFeedback[wrongOption] : statement.hint;
            prompt += "（尚有矛盾）";
            if (!string.IsNullOrWhiteSpace(feedback)) prompt += "\n" + feedback;
        }

        var choices = statement.options.Select((word, index) =>
        {
            int optionIndex = index;
            return (word, (Action)(() => ChooseSequentialAnswer(activeDeductionRow, optionIndex)));
        }).ToArray();
        dialogueBox.Show(dialogueBox.CurrentMainCharacterId, prompt, null, choices);
    }

    private void ChooseSequentialAnswer(int rowIndex, int optionIndex)
    {
        if (activeDeduction == null || rowIndex != activeDeductionRow) return;
        DeductionData.Statement statement = activeDeduction.statements[rowIndex];
        string[] selected = deductionChoices[activeDeduction.groupId];
        bool[] errors = deductionErrors[activeDeduction.groupId];
        if (optionIndex < 0 || optionIndex >= statement.options.Length) return;

        selected[rowIndex] = statement.options[optionIndex];
        bool mustBeCorrect = activeDeduction.mode != "Hypothesis";
        errors[rowIndex] = mustBeCorrect
            && (statement.correctOption < 0 || statement.correctOption >= statement.options.Length
                || optionIndex != statement.correctOption);
        if (errors[rowIndex])
        {
            RenderSequentialDeduction();
            return;
        }

        activeDeductionRow++;
        RenderSequentialDeduction();
    }

    private void ChooseStatement(int row)
    {
        DeductionData group = activeDeduction;
        var statement = group.statements[row];
        var choices = statement.options.Select(word => (word, (Action)(() =>
        {
            deductionChoices[group.groupId][row] = word;
            deductionErrors[group.groupId][row] = false;
            activeDeduction = group;
            RenderDeduction();
        }))).ToArray();
        dialogueBox.Show(dialogueBox.CurrentMainCharacterId, statement.sentence.Replace("{0}", "______"), null, choices);
    }

    public void ConfirmDeduction()
    {
        if (activeDeduction == null) return;
        string[] selected = deductionChoices[activeDeduction.groupId];
        if (selected.Any(string.IsNullOrEmpty)) return;
        if (activeDeduction.mode != "Hypothesis")
        {
            var errors = deductionErrors[activeDeduction.groupId];
            for (int i = 0; i < selected.Length; i++)
            {
                var row = activeDeduction.statements[i];
                errors[i] = row.correctOption < 0 || row.correctOption >= row.options.Length || selected[i] != row.options[row.correctOption];
            }
            if (errors.Any(error => error))
            {
                RenderDeduction();
                int wrong = Array.FindIndex(errors, error => error);
                var row = activeDeduction.statements[wrong];
                int option = Array.IndexOf(row.options, selected[wrong]);
                string feedback = option >= 0 && option < row.wrongFeedback.Length ? row.wrongFeedback[option] : row.hint;
                // 留在原题，逐行标记错误并保留全部已选内容。
                searchStage?.ShowFeedback(string.IsNullOrEmpty(feedback) ? row.hint : feedback);
                return;
            }
        }
        string next = activeDeduction.nextTarget;
        activeDeduction = null;
        searchStage?.HideFeedback();
        Goto(next);
    }
}
