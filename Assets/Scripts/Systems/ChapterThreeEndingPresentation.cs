using UnityEngine;
using UnityEngine.UI;

/// <summary>结局 CG 底图，位于对话框和立绘下方，直接在 Image 中替换精灵。</summary>
public sealed class ChapterThreeEndingPresentation : MonoBehaviour
{
    [SerializeField] private Image backgroundCG;

    public void SetVisible(bool visible)
    {
        if (backgroundCG == null) backgroundCG = GetComponent<Image>();
        // 未提供 CG 时保留当前现场，不显示空 Image 的白色矩形。
        if (backgroundCG != null)
        {
            backgroundCG.raycastTarget = false;
            backgroundCG.enabled = backgroundCG.sprite != null;
        }
        gameObject.SetActive(visible);
    }
}
