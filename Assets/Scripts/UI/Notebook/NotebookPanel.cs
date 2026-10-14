using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 笔记本面板草稿：平时整本收到屏幕右侧外，只露边缘拉片（OpenNotebookButton）；
/// 点拉片后笔记本向左平移展开，书脊处出现收起拉片（CloseNotebookButton），再点收回。
/// 场景内对象全部按约定名字查找，不做 Inspector 拖线，方便复制到其他场景。
/// 约定物体名：NotebookPanel（滑动的根 RectTransform）、OpenNotebookButton、CloseNotebookButton。
/// </summary>
public sealed class NotebookPanel : MonoBehaviour
{
    [Tooltip("关闭时整体右移距离（参考分辨率 1280x720 下的像素数，草稿值可自行调整）")]
    [SerializeField] private float slideDistance = 465f;

    [Tooltip("平移动画时长（秒）")]
    [SerializeField] private float slideDuration = 0.28f;

    private RectTransform slidingRoot;
    private Button openButton;
    private Button closeButton;

    private Coroutine slideRoutine;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        slidingRoot = FindRecursive(transform, "NotebookPanel") as RectTransform;
        openButton = FindButton("OpenNotebookButton");
        closeButton = FindButton("CloseNotebookButton");

        if (openButton != null)
        {
            openButton.onClick.AddListener(Open);
        }
        else
        {
            Debug.LogWarning("NotebookPanel：找不到 OpenNotebookButton");
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
        else
        {
            Debug.LogWarning("NotebookPanel：找不到 CloseNotebookButton");
        }

        SetClosedInstant();
    }

    public void Open()
    {
        if (isOpen || slidingRoot == null)
        {
            return;
        }

        isOpen = true;

        if (openButton != null)
        {
            openButton.gameObject.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
        }

        StartSlide(0f);
    }

    public void Close()
    {
        if (!isOpen || slidingRoot == null)
        {
            return;
        }

        isOpen = false;

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(false);
        }

        StartSlide(slideDistance);
    }

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void SetClosedInstant()
    {
        isOpen = false;
        if (slidingRoot != null)
        {
            Vector2 position = slidingRoot.anchoredPosition;
            position.x = slideDistance;
            slidingRoot.anchoredPosition = position;
        }

        if (openButton != null)
        {
            openButton.gameObject.SetActive(true);
        }

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(false);
        }
    }

    private void StartSlide(float targetX)
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
        }

        slideRoutine = StartCoroutine(SlideRoutine(targetX));
    }

    private IEnumerator SlideRoutine(float targetX)
    {
        float startX = slidingRoot.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = 1f - (1f - t) * (1f - t); // ease-out
            Vector2 position = slidingRoot.anchoredPosition;
            position.x = Mathf.Lerp(startX, targetX, t);
            slidingRoot.anchoredPosition = position;
            yield return null;
        }

        Vector2 finalPosition = slidingRoot.anchoredPosition;
        finalPosition.x = targetX;
        slidingRoot.anchoredPosition = finalPosition;
        slideRoutine = null;

        // 收起完成后才显示边缘拉开片，避免它提前出现在屏幕里。
        if (!isOpen && openButton != null)
        {
            openButton.gameObject.SetActive(true);
        }
    }

    private Button FindButton(string name)
    {
        return FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == name);
    }

    private static Transform FindRecursive(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindRecursive(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
