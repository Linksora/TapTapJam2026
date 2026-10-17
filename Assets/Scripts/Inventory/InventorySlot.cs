using UnityEngine;
using UnityEngine.UI;

public sealed class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private DraggableItem draggableItem;
    private ItemData currentItem;
    private Text fallbackLabel;
    private Color iconColor;
    private bool capturedIconColor;
    private static Font fallbackFont;

    public ItemData CurrentItem => currentItem;

    public void SetItem(ItemData item)
    {
        currentItem = item;

        if (itemIcon != null)
        {
            if (!capturedIconColor)
            {
                iconColor = itemIcon.color.a > 0f ? itemIcon.color : Color.white;
                capturedIconColor = true;
            }
            itemIcon.sprite = item != null ? item.Icon : null;
            // 未配图标的线索仍需要可点击、可拖拽的 Graphic。
            itemIcon.enabled = item != null;
            itemIcon.raycastTarget = true;
            itemIcon.color = item != null && item.Icon != null ? iconColor : Color.clear;
            if (item != null && item.Icon == null) EnsureFallbackLabel();
            if (fallbackLabel != null)
            {
                fallbackLabel.text = item != null ? item.ItemName : string.Empty;
                fallbackLabel.gameObject.SetActive(item != null && item.Icon == null);
            }
        }

        if (draggableItem != null)
        {
            draggableItem.SetItem(item);
        }
    }

    private void EnsureFallbackLabel()
    {
        if (fallbackLabel != null) return;
        // 克隆格子时复用已有占位文字，避免叠加。
        fallbackLabel = itemIcon.transform.Find("ItemName")?.GetComponent<Text>();
        if (fallbackLabel != null) return;
        var label = new GameObject("ItemName", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(itemIcon.transform, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fallbackLabel = label.GetComponent<Text>();
        if (fallbackFont == null)
            fallbackFont = Font.CreateDynamicFontFromOSFont(
                new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial" }, 20);
        fallbackLabel.font = fallbackFont;
        fallbackLabel.fontSize = 20;
        fallbackLabel.alignment = TextAnchor.MiddleCenter;
        fallbackLabel.color = new Color(0.98f, 0.94f, 0.82f);
        fallbackLabel.raycastTarget = false;
    }
}
