using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 按住型按钮：按住期间 IsPressed 为 true，松开或手指滑出按钮区域时复位。
/// 场景平移等"按住即持续输入"的控制使用；普通点击仍可由 Button.onClick 处理。
/// </summary>
public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Button cachedButton;

    public Button Button => cachedButton != null ? cachedButton : (cachedButton = GetComponent<Button>());

    public bool IsPressed { get; private set; }

    private void Awake()
    {
        cachedButton = GetComponent<Button>();

        // 与对话 UI 一致：换用系统中文字体，保证箭头/特殊符号有字形。
        Text label = GetComponentInChildren<Text>();
        if (label != null)
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial" }, 32);
            if (font != null)
            {
                label.font = font;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Button != null && !Button.IsInteractable())
        {
            return;
        }

        IsPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 按住时手指/鼠标滑出按钮区域即停止输入。
        IsPressed = false;
    }

    private void OnDisable()
    {
        IsPressed = false;
    }
}
