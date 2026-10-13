using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡剧情总控：
/// 1) 按场景名自动定位关卡（场景名含 Level_01 → 读 LevelData/Level_01；Celia_TestScene 等测试场景默认 Level_01）；
/// 2) 从 Resources 加载该关卡的 LevelDatabase，自动查找场景里的 DialogueBoxView；
/// 3) 无选项台词点面板继续，有选项点按钮分支，Q_ 目标暂停等待题目系统接入。
/// </summary>
public sealed class StoryManager : MonoBehaviour
{
    private const string DefaultLevelId = "Level_01";

    [SerializeField] private string overrideLevelId;
    [SerializeField] private bool playOnStart = true;

    private LevelDatabase database;
    private DialogueBoxView dialogueBox;
    private string pendingNextTarget;

    public string LevelId { get; private set; }

    private void Start()
    {
        LevelId = ResolveLevelId();
        database = Resources.Load<LevelDatabase>($"LevelData/{LevelId}/LevelDatabase");
        if (database == null)
        {
            Debug.LogError($"[StoryManager] 找不到关卡数据 Resources/LevelData/{LevelId}/LevelDatabase，" +
                           "请先执行 TapJam/Import All Level CSV。");
            enabled = false;
            return;
        }

        // DialogueBox 默认隐藏（Builder 里 SetActive(false)），必须包含 inactive 对象才能找到。
        dialogueBox = FindFirstObjectByType<DialogueBoxView>(FindObjectsInactive.Include);
        if (dialogueBox == null)
        {
            Debug.LogError("[StoryManager] 场景中没有 DialogueBoxView。");
            enabled = false;
            return;
        }

        // 点击对话框本体继续（运行时挂 Button，不改动 UI 结构）。
        Button clickCatcher = dialogueBox.GetComponent<Button>();
        if (clickCatcher == null)
        {
            clickCatcher = dialogueBox.gameObject.AddComponent<Button>();
            clickCatcher.transition = Selectable.Transition.None;
        }

        clickCatcher.onClick.AddListener(OnBoxClicked);

        if (playOnStart)
        {
            Play(database.StartDialogueId);
        }
    }

    public void Play(string dialogueId)
    {
        Goto(dialogueId);
    }

    private string ResolveLevelId()
    {
        if (!string.IsNullOrWhiteSpace(overrideLevelId))
        {
            return overrideLevelId.Trim();
        }

        Match match = Regex.Match(gameObject.scene.name, @"Level_\d+");
        return match.Success ? match.Value : DefaultLevelId;
    }

    private void OnBoxClicked()
    {
        if (dialogueBox.IsShowingOptions)
        {
            return;
        }

        Goto(pendingNextTarget);
    }

    private void Goto(string targetId)
    {
        if (string.IsNullOrEmpty(targetId) || targetId == "-1")
        {
            dialogueBox.Hide();
            pendingNextTarget = null;
            return;
        }

        if (targetId.StartsWith("Q_"))
        {
            QuestionData question = database.GetQuestion(targetId);
            dialogueBox.Show("（拼凑/质询）",
                question != null ? question.Statement : $"题目 {targetId} 的 UI 尚未接入。",
                question != null ? PortraitLoader.Load(question.HesitantPortraitPath) : null);
            pendingNextTarget = null;
            Debug.Log($"[StoryManager:{LevelId}] 到达题目 {targetId}，题目交互尚未接入，剧情在此暂停。");
            return;
        }

        DialogueData dialogue = database.GetDialogue(targetId);
        if (dialogue == null)
        {
            Debug.LogWarning($"[StoryManager:{LevelId}] 找不到对话 {targetId}，剧情结束。");
            dialogueBox.Hide();
            pendingNextTarget = null;
            return;
        }

        Sprite portrait = PortraitLoader.Load(dialogue.PortraitPath);
        if (dialogue.HasOptions)
        {
            pendingNextTarget = null;
            dialogueBox.Show(dialogue.Speaker, dialogue.Text, portrait,
                (dialogue.Option1Text, () => Goto(dialogue.Option1Target)),
                (dialogue.Option2Text, () => Goto(dialogue.Option2Target)));
        }
        else
        {
            pendingNextTarget = dialogue.NextTarget;
            dialogueBox.Show(dialogue.Speaker, dialogue.Text, portrait);
        }
    }
}
