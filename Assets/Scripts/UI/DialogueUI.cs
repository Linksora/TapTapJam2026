using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Text dialogueText;
    [SerializeField, Min(0f)] private float autoHideDelay;
    private Coroutine hideRoutine;

    private void Awake()
    {
        // 使用系统动态字体，保证 Windows 编辑器中中文可以正常显示。
        if (dialogueText != null)
        {
            Font chineseFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" }, 32);
            if (chineseFont != null)
            {
                dialogueText.font = chineseFont;
            }
        }
    }

    public void ShowDialogue(string text)
    {
        if (dialoguePanel == null || dialogueText == null)
        {
            return;
        }

        dialogueText.text = text ?? string.Empty;
        dialoguePanel.SetActive(true);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        if (autoHideDelay > 0f)
        {
            hideRoutine = StartCoroutine(HideAfterDelay());
        }
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(autoHideDelay);
        hideRoutine = null;
        HideDialogue();
    }
}
