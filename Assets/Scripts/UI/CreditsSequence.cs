using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>四张制作人员精灵按顺序渐变播放，结束后返回开始页面。</summary>
public sealed class CreditsSequence : MonoBehaviour
{
    [Tooltip("按播放顺序连接四个 Image；精灵直接拖到各 Image 的 Source Image。")]
    [SerializeField] private Image[] slides = new Image[4];
    [SerializeField, Min(0f)] private float holdSeconds = 4f;
    [SerializeField, Min(0f)] private float fadeSeconds = 1f;
    [SerializeField] private string startScene = "TitleScene";

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        foreach (Image slide in slides)
        {
            if (slide == null) continue;
            slide.gameObject.SetActive(true);
            slide.raycastTarget = false;
            SetAlpha(slide, 0);
        }
        Image previous = null;
        foreach (Image slide in slides)
        {
            if (slide == null) continue;
            slide.transform.SetAsLastSibling();
            yield return Fade(previous, slide);
            previous = slide;
            float elapsed = 0;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        yield return Fade(previous, null);
        if (Application.CanStreamedLevelBeLoaded(startScene)) SceneManager.LoadScene(startScene);
        else Debug.LogError($"制作人员名单无法返回：{startScene} 未加入 Build Settings。", this);
    }

    private IEnumerator Fade(Image from, Image to)
    {
        float elapsed = 0;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSeconds);
            // 新图覆盖在旧图上渐显；最后一张再渐隐到黑，避免交叉渐变中途变暗。
            if (to != null) SetAlpha(to, t);
            else if (from != null) SetAlpha(from, 1f - t);
            yield return null;
        }
        if (from != null) SetAlpha(from, 0);
        if (to != null) SetAlpha(to, 1);
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
