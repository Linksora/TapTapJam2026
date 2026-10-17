using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>挂在可点击的世界入口上，切换到指定背景，不跳转剧情或结束搜证。</summary>
[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public sealed class BackgroundSwitch : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("BackgroundController 中的背景 Id 或别名，例如：便利店、街道、学校侧院。")]
    [SerializeField] private string targetBackground;

    [Tooltip("可留空，自动查找本场景的 BackgroundController。")]
    [SerializeField] private BackgroundController controller;
    [Tooltip("剧情关卡中只在搜证时允许玩家切换。关闭后可用于其他自由探索场景。")]
    [SerializeField] private bool requireSearchPhase = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) SwitchBackground();
    }

    public void SwitchBackground()
    {
        if (requireSearchPhase)
        {
            foreach (StoryManager story in FindObjectsByType<StoryManager>(FindObjectsSortMode.None))
                if (story.gameObject.scene == gameObject.scene && story.enabled)
                {
                    if (!story.IsSearching || story.ShouldInspectBeforeTravel(gameObject)) return;
                    if (story.TryNavigateBackground(targetBackground)) return;
                }
        }
        if (string.IsNullOrWhiteSpace(targetBackground))
        {
            Debug.LogWarning("[BackgroundSwitch] 请填写 Target Background。", this);
            return;
        }

        if (controller == null)
        {
            foreach (BackgroundController candidate in FindObjectsByType<BackgroundController>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                controller = candidate;
                break;
            }
        }

        if (controller == null)
        {
            Debug.LogWarning("[BackgroundSwitch] 当前场景没有 BackgroundController。", this);
            return;
        }

        controller.Show(targetBackground.Trim());
    }
}
