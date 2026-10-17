using UnityEngine;

/// <summary>
/// ScriptTable 的一行：一句对话。Target 协议：D_xxx 跳转对话，Q_xxx 进入题目，-1/空 关闭面板。
/// PortraitPath 是 Resources/Portraits 下的美术文件名（不带扩展名），运行时按名字加载。
/// </summary>
public sealed class DialogueData : ScriptableObject
{
    [SerializeField] private string dialogueId;
    [SerializeField] private ItemData entryClue;
    [SerializeField] private string speaker;
    [SerializeField, TextArea] private string text;
    [SerializeField] private string portraitPath;
    [SerializeField] private string option1Text;
    [SerializeField] private string option1Target;
    [SerializeField] private string option2Text;
    [SerializeField] private string option2Target;
    [SerializeField] private string nextTarget;
    [SerializeField] private string background;
    [SerializeField] private string[] searchClueIds;

    public string DialogueId => dialogueId;
    public ItemData EntryClue => entryClue;
    public string Speaker => speaker;
    public string Text => text;
    public string PortraitPath => portraitPath;
    public string Option1Text => option1Text;
    public string Option1Target => option1Target;
    public string Option2Text => option2Text;
    public string Option2Target => option2Target;
    public string NextTarget => nextTarget;

    /// <summary>非空时切换场景背景，值为 Resources/Art/场景 下的贴图名（不带扩展名）。</summary>
    public string Background => background;

    /// <summary>
    /// 非空时本行是搜证节点：关闭对话框与对手立绘，玩家集齐这些线索（竖线分隔的 ClueID）后，
    /// 自动跳转到 NextTarget 继续剧情。
    /// </summary>
    public string[] SearchClueIds => searchClueIds ?? System.Array.Empty<string>();

    public bool HasOptions => !string.IsNullOrEmpty(option1Text) || !string.IsNullOrEmpty(option2Text);
}
