using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private PlayerReaction selectionReaction;
    [SerializeField] private ClueDetailPanel clueDetailPanel;
    private ItemData item;
    private GameObject dragIconObject;
    private RectTransform dragIconRect;

    /// <summary>单击物品图标时触发（笔记本描述框等订阅），拖拽不会触发。</summary>
    public event Action<ItemData> Clicked;

    public void ConfigureNotebook(Canvas canvas, RectTransform layer)
    {
        rootCanvas = canvas;
        dragLayer = layer;
        // 笔记本负责线索详情，避免同时弹出旧版物品反应/详情面板。
        selectionReaction = null;
        clueDetailPanel = null;
    }

    public void SetItem(ItemData value)
    {
        item = value;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 完成拖拽后 EventSystem 不会触发点击，因此这里仅处理真正的单击查看描述。
        if (item != null && eventData.button == PointerEventData.InputButton.Left)
        {
            Clicked?.Invoke(item);
            selectionReaction?.ReactToSelectedItem(item);
            clueDetailPanel?.Show(item);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || item == null || rootCanvas == null || dragLayer == null)
        {
            return;
        }

        dragIconObject = item.Icon != null ? CreateIconChip() : CreateTextChip(item.ItemName);
        dragIconObject.transform.SetParent(dragLayer, false);
        dragIconObject.transform.SetAsLastSibling();

        dragIconRect = dragIconObject.GetComponent<RectTransform>();

        UpdateDragPosition(eventData);
    }

    private GameObject CreateIconChip()
    {
        var chip = new GameObject("DragIcon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        chip.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 96f);

        Image image = chip.GetComponent<Image>();
        image.sprite = item.Icon;
        image.preserveAspect = true;
        image.raycastTarget = false;

        ConfigureChipCanvasGroup(chip);
        return chip;
    }

    /// <summary>没有图标资源的证物也必须能拖去面质：用带物品名的深色小芯片占位。</summary>
    private GameObject CreateTextChip(string label)
    {
        var chip = new GameObject("DragIcon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        chip.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 56f);

        Image background = chip.GetComponent<Image>();
        background.color = new Color(0.07f, 0.09f, 0.12f, 0.85f);
        background.raycastTarget = false;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(chip.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        Text text = labelObject.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = Color.white;
        text.fontSize = 22;
        text.raycastTarget = false;
        text.font = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial" }, text.fontSize);

        ConfigureChipCanvasGroup(chip);
        return chip;
    }

    private static void ConfigureChipCanvasGroup(GameObject chip)
    {
        CanvasGroup canvasGroup = chip.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0.9f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateDragPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIconObject == null)
        {
            return;
        }

        // 先移除临时图标，再重新射线检测，确保它不会挡住目标。
        Destroy(dragIconObject);
        dragIconObject = null;
        dragIconRect = null;

        if (item == null || EventSystem.current == null)
        {
            return;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            ItemReceiver receiver = result.gameObject.GetComponentInParent<ItemReceiver>();
            if (receiver != null)
            {
                receiver.ReceiveItem(item);
                return;
            }
        }

        // 没有有效目标时不做数据变更，物品自然留在原 Slot。
    }

    private void UpdateDragPosition(PointerEventData eventData)
    {
        if (dragIconRect == null || rootCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventData.pressEventCamera;

        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position, eventCamera, out Vector2 localPoint))
        {
            dragIconRect.position = rootCanvas.transform.TransformPoint(localPoint);
        }
    }

    private void OnDisable()
    {
        if (dragIconObject != null)
        {
            Destroy(dragIconObject);
            dragIconObject = null;
            dragIconRect = null;
        }
    }
}
