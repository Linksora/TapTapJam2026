using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public sealed class WorldItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private InventoryManager inventoryManager;
    private bool pickedUp;

    private void Awake()
    {
        inventoryManager ??= FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (pickedUp || itemData == null || inventoryManager == null)
        {
            return;
        }

        // 只有成功放入背包后才隐藏，避免背包已满时丢失物品。
        if (inventoryManager.AddItem(itemData))
        {
            pickedUp = true;
            gameObject.SetActive(false);
        }
    }
}
