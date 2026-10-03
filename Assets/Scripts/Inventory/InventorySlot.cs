using UnityEngine;
using UnityEngine.UI;

public sealed class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private DraggableItem draggableItem;
    private ItemData currentItem;

    public ItemData CurrentItem => currentItem;

    public void SetItem(ItemData item)
    {
        currentItem = item;

        if (itemIcon != null)
        {
            itemIcon.sprite = item != null ? item.Icon : null;
            itemIcon.enabled = item != null && item.Icon != null;
        }

        if (draggableItem != null)
        {
            draggableItem.SetItem(item);
        }
    }
}
