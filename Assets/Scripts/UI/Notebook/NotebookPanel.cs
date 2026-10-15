using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 笔记本面板：平时整本收到屏幕右侧外，只露边缘拉片（OpenNotebookButton）；
/// 点拉片后笔记本向左平移展开，书脊处出现收起拉片（CloseNotebookButton），再点收回。
/// 展开后上半页是线索描述文本框（ClueDescriptionText），点下半页物品栏里的物品显示对应描述。
/// 场景内对象全部按约定名字查找（InventoryPanel/DragLayer/ClueDescriptionText），不做 Inspector 拖线。
/// InventoryPanel、DragLayer 在场景里可以先挂在任意 Canvas 下，Awake 会原样挪进滑动层，随笔记本一起收展。
/// 约定物体名：NotebookPanel（滑动的根 RectTransform）、OpenNotebookButton、CloseNotebookButton。
/// </summary>
public sealed class NotebookPanel : MonoBehaviour
{
    private const string DescriptionPlaceholder = "点击物品查看描述。";

    [Tooltip("关闭时整体右移距离（参考分辨率 1280x720 下的像素数，草稿值可自行调整）")]
    [SerializeField] private float slideDistance = 465f;

    [Tooltip("平移动画时长（秒）")]
    [SerializeField] private float slideDuration = 0.28f;

    private RectTransform slidingRoot;
    private Button openButton;
    private Button closeButton;
    private Text descriptionText;
    private CanvasGroup canvasGroup;

    private Coroutine slideRoutine;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        slidingRoot = FindRecursive(transform, "NotebookPanel") as RectTransform;
        openButton = FindButton("OpenNotebookButton");
        closeButton = FindButton("CloseNotebookButton");

        // 滑动层收起后仍盖住大半个屏幕，必须靠 CanvasGroup 整块放行射线，
        // 否则关闭状态下笔记本的全屏底图会吞掉 world 搜证和对话框的点击。
        canvasGroup = slidingRoot != null ? slidingRoot.GetComponent<CanvasGroup>() : null;
        if (canvasGroup == null && slidingRoot != null)
        {
            canvasGroup = slidingRoot.gameObject.AddComponent<CanvasGroup>();
        }

        AdoptScenePiece("InventoryPanel");
        AdoptScenePiece("DragLayer");
        MakeBackgroundClickThrough();
        ResolveDescription();
        BindInventoryClicks();

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

    /// <summary>把场景里散放在其他 Canvas 下的物品栏/拖拽层原样收进滑动层（保持屏幕位置不变）。</summary>
    private void AdoptScenePiece(string objectName)
    {
        if (slidingRoot == null)
        {
            return;
        }

        Transform found = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == objectName && !t.IsChildOf(slidingRoot) && t != slidingRoot);
        if (found == null)
        {
            return;
        }

        found.SetParent(slidingRoot, true);

        // 渲染/点击顺序：底图在最下，物品栏压底图，收起拉片压物品栏，拖拽图标永远在最上。
        found.SetAsLastSibling();
        if (closeButton != null)
        {
            closeButton.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 底图是铺满全屏的纯美术 Image，UGUI 默认透明像素也吃射线，
    /// 必须关掉 raycastTarget，否则笔记本一展开就全屏挡住对话框和 world 搜证。
    /// </summary>
    private void MakeBackgroundClickThrough()
    {
        if (slidingRoot == null)
        {
            return;
        }

        Image background = FindRecursive(slidingRoot, "NotebookImage")?.GetComponent<Image>();
        if (background != null)
        {
            background.raycastTarget = false;
        }
    }

    private void ResolveDescription()
    {
        Transform textTransform = slidingRoot != null
            ? FindRecursive(slidingRoot, "ClueDescriptionText")
            : FindRecursive(transform, "ClueDescriptionText");
        descriptionText = textTransform != null ? textTransform.GetComponent<Text>() : null;
        if (descriptionText == null)
        {
            Debug.LogWarning("NotebookPanel：找不到 ClueDescriptionText（执行 TapJam/Build Notebook Panel）。");
            return;
        }

        Font chineseFont = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial" }, descriptionText.fontSize);
        if (chineseFont != null)
        {
            descriptionText.font = chineseFont;
        }

        descriptionText.text = DescriptionPlaceholder;
    }

    private void BindInventoryClicks()
    {
        foreach (DraggableItem draggable in FindObjectsByType<DraggableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            draggable.Clicked += ShowDescription;
        }
    }

    /// <summary>点击物品栏物品：在上半页文本框显示物品名与描述。</summary>
    public void ShowDescription(ItemData item)
    {
        if (item == null || descriptionText == null)
        {
            return;
        }

        descriptionText.text = $"{item.ItemName}\n{item.Description}";
    }

    public void Open()
    {
        if (isOpen || slidingRoot == null)
        {
            return;
        }

        isOpen = true;
        SetRaycastBlocking(true);

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
        SetRaycastBlocking(false);

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
        SetRaycastBlocking(false);
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

    /// <summary>展开时整块拦截点击（模态）；收起时整块穿透，露出下面的 world 搜证点和对话框。</summary>
    private void SetRaycastBlocking(bool blocking)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = blocking;
            canvasGroup.interactable = blocking;
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
