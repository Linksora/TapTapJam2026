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
    [SerializeField] private string[] optionalClueIds;
    [SerializeField] private bool isTransition;
    [SerializeField] private ItemData rewardClue;
    [SerializeField] private string mainCharacters;
    [SerializeField] private string conditionGroup;
    [SerializeField] private string conditionStatement;
    [SerializeField] private string conditionValue;
    [SerializeField] private string alternateTarget;
    [SerializeField] private string searchStartZone;
    [SerializeField] private string stageAction;
    public string SearchStartZone => searchStartZone;
    public string StageAction => stageAction;
    [SerializeField] private float holdSeconds;
    public float HoldSeconds => holdSeconds;
    public ItemData RewardClue => rewardClue;
    public string MainCharacters => mainCharacters;
    public string ConditionGroup => conditionGroup;
    public string ConditionStatement => conditionStatement;
    public string ConditionValue => conditionValue;
    public string AlternateTarget => alternateTarget;

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
    // 兼容尚未重新导入的旧剧本标记。
    public bool IsTransition => isTransition || speaker?.Trim() == "【玩法】";

    /// <summary>非空时切换背景分组，值为 BackgroundController 中的 Id 或别名；旧场景仍可使用贴图名。</summary>
    public string Background => background;

    /// <summary>
    /// 非空时本行是搜证节点：隐藏对话框，允许收集必需及可选线索；集齐必需线索后自动跳转。
    /// </summary>
    public string[] SearchClueIds => searchClueIds ?? System.Array.Empty<string>();
    public string[] OptionalClueIds => optionalClueIds ?? System.Array.Empty<string>();

    public bool HasOptions => !string.IsNullOrEmpty(option1Text) || !string.IsNullOrEmpty(option2Text);
}
