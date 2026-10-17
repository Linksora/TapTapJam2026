using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 笔记本面板：平时整本收到屏幕右侧外，只露边缘拉片（OpenNotebookButton）；
/// 点拉片后笔记本向左平移展开，书脊处出现收起拉片（CloseNotebookButton），再点收回。
/// 展开后上半页是线索描述文本框（ClueDescriptionText），点下半页物品栏里的物品显示对应描述。
/// 场景内对象全部按约定名字查找（InventoryPanel/DragLayer/ClueDescriptionText），不做 Inspector 拖线。
/// InventoryPanel 随书页滑动；DragLayer 保留在 Canvas 顶层，拖拽图标不受书页收展影响。
/// 约定物体名：NotebookPanel（滑动的根 RectTransform）、OpenNotebookButton、CloseNotebookButton。
/// </summary>
public sealed class NotebookPanel : MonoBehaviour
{
    private const string DescriptionPlaceholder = "点击物品查看描述。";

    [Tooltip("关闭时整体右移距离（参考分辨率 1280x720 下的像素数，草稿值可自行调整）")]
    [SerializeField] private float slideDistance = 465f;

    [Tooltip("平移动画时长（秒）")]
    [SerializeField] private float slideDuration = 0.28f;

    [SerializeField] private RectTransform slidingRoot;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text descriptionText;
    [SerializeField] private RectTransform dragLayer;
    private CanvasGroup canvasGroup;
    private Vector2 openPosition;
    private readonly List<DraggableItem> boundItems = new();

    private Coroutine slideRoutine;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (slidingRoot == null) slidingRoot = FindRecursive(transform, "NotebookPanel") as RectTransform;
        if (openButton == null) openButton = FindButton("OpenNotebookButton");
        if (closeButton == null) closeButton = FindButton("CloseNotebookButton");
        if (slidingRoot == null)
        {
            Debug.LogError("NotebookPanel：找不到笔记本滑动层。", this);
            enabled = false;
            return;
        }
        // 编辑态摆放的位置就是展开位置，不能再写死 x = 0。
        openPosition = slidingRoot.anchoredPosition;

        // 滑动层收起后仍盖住大半个屏幕，必须靠 CanvasGroup 整块放行射线，
        // 否则关闭状态下笔记本的全屏底图会吞掉 world 搜证和对话框的点击。
        canvasGroup = slidingRoot != null ? slidingRoot.GetComponent<CanvasGroup>() : null;
        if (canvasGroup == null && slidingRoot != null)
        {
            canvasGroup = slidingRoot.gameObject.AddComponent<CanvasGroup>();
        }

        AdoptScenePiece("InventoryPanel");
        ResolveDragLayer();
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

        // 已经在书页下就保留用户布局，避免再收进其他同名面板。
        if (FindRecursive(slidingRoot, objectName) != null) return;
        Transform found = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.gameObject.scene == gameObject.scene && t.name == objectName);
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

    private void ResolveDragLayer()
    {
        if (dragLayer == null)
            dragLayer = FindRecursive(transform, "DragLayer") as RectTransform;
        if (dragLayer == null)
            dragLayer = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.gameObject.scene == gameObject.scene && t.name == "DragLayer");
        if (dragLayer == null)
            dragLayer = new GameObject("DragLayer", typeof(RectTransform)).GetComponent<RectTransform>();

        dragLayer.SetParent(transform, false);
        dragLayer.anchorMin = Vector2.zero;
        dragLayer.anchorMax = Vector2.one;
        dragLayer.offsetMin = dragLayer.offsetMax = Vector2.zero;
        dragLayer.SetAsLastSibling();
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
        if (descriptionText == null)
            descriptionText = textTransform != null ? textTransform.GetComponent<Text>() : null;
        if (descriptionText == null)
        {
            Debug.LogWarning("NotebookPanel：找不到 ClueDescriptionText（执行 TapJam/Build Notebook Panel）。");
            return;
        }

        Font chineseFont = GameFont.Primary;
        if (chineseFont != null)
        {
            descriptionText.font = chineseFont;
        }

        descriptionText.text = DescriptionPlaceholder;
        descriptionText.raycastTarget = false;
    }

    private void BindInventoryClicks()
    {
        foreach (DraggableItem draggable in slidingRoot.GetComponentsInChildren<DraggableItem>(true))
        {
            BindInventoryItem(draggable);
        }
    }

    public void BindInventoryItem(DraggableItem draggable)
    {
        if (draggable == null || boundItems.Contains(draggable)) return;
        draggable.ConfigureNotebook(GetComponentInParent<Canvas>(), dragLayer);
        draggable.Clicked += ShowDescription;
        boundItems.Add(draggable);
    }

    private void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        foreach (DraggableItem draggable in boundItems)
            if (draggable != null) draggable.Clicked -= ShowDescription;
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

        StartSlide(openPosition.x);
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

        StartSlide(openPosition.x + slideDistance);
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
            position.x = openPosition.x + slideDistance;
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

    /// <summary>展开时启用书页内交互；收起时穿透，场景和对话框保持可点击。</summary>
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
        return FindRecursive(transform, name)?.GetComponent<Button>();
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
