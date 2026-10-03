using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private PlayerReaction selectionReaction;
    private ItemData item;
    private GameObject dragIconObject;
    private RectTransform dragIconRect;

    public void SetItem(ItemData value)
    {
        item = value;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 完成拖拽后 EventSystem 不会触发点击，因此这里仅处理真正的单击选择。
        if (item != null && eventData.button == PointerEventData.InputButton.Left)
        {
            selectionReaction?.ReactToSelectedItem(item);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (item == null || item.Icon == null || rootCanvas == null || dragLayer == null)
        {
            return;
        }

        dragIconObject = new GameObject("DragIcon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragIconObject.transform.SetParent(dragLayer, false);
        dragIconObject.transform.SetAsLastSibling();

        dragIconRect = dragIconObject.GetComponent<RectTransform>();
        dragIconRect.sizeDelta = new Vector2(96f, 96f);

        Image image = dragIconObject.GetComponent<Image>();
        image.sprite = item.Icon;
        image.preserveAspect = true;
        image.raycastTarget = false;

        CanvasGroup canvasGroup = dragIconObject.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0.9f;

        UpdateDragPosition(eventData);
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
        }
    }
}
