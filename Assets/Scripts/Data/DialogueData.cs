using UnityEngine;

/// <summary>
/// ScriptTable 的一行：一句对话。Target 协议：D_xxx 跳转对话，Q_xxx 进入题目，-1/空 关闭面板。
/// </summary>
public sealed class DialogueData : ScriptableObject
{
    [SerializeField] private string dialogueId;
    [SerializeField] private ItemData entryClue;
    [SerializeField] private string speaker;
    [SerializeField, TextArea] private string text;
    [SerializeField] private Sprite portrait;
    [SerializeField] private string option1Text;
    [SerializeField] private string option1Target;
    [SerializeField] private string option2Text;
    [SerializeField] private string option2Target;
    [SerializeField] private string nextTarget;

    public string DialogueId => dialogueId;
    public ItemData EntryClue => entryClue;
    public string Speaker => speaker;
    public string Text => text;
    public Sprite Portrait => portrait;
    public string Option1Text => option1Text;
    public string Option1Target => option1Target;
    public string Option2Text => option2Text;
    public string Option2Target => option2Target;
    public string NextTarget => nextTarget;

    public bool HasOptions => !string.IsNullOrEmpty(option1Text) || !string.IsNullOrEmpty(option2Text);
}
