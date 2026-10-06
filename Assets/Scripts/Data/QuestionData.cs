using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AssemblyTable 的一行。Options 非空为拼凑选填题；为空为质询题，靠拖拽线索比对 TargetClue。
/// </summary>
public sealed class QuestionData : ScriptableObject
{
    [SerializeField] private string questionId;
    [SerializeField, TextArea] private string statement;
    [SerializeField] private List<string> options = new();
    [SerializeField] private int correctOption = -1;
    [SerializeField] private string successTarget;
    [SerializeField] private ItemData successClue;
    [SerializeField] private string wrongTarget;
    [SerializeField] private Sprite wrongPortrait;
    [SerializeField] private Sprite hesitantPortrait;
    [SerializeField] private ItemData targetClue;

    public string QuestionId => questionId;
    public string Statement => statement;
    public IReadOnlyList<string> Options => options;
    public int CorrectOption => correctOption;
    public string SuccessTarget => successTarget;
    public ItemData SuccessClue => successClue;
    public string WrongTarget => wrongTarget;
    public Sprite WrongPortrait => wrongPortrait;
    public Sprite HesitantPortrait => hesitantPortrait;
    public ItemData TargetClue => targetClue;

    public bool IsInterrogation => options == null || options.Count == 0;
}
