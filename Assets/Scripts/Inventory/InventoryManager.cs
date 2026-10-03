using System.Collections.Generic;
using UnityEngine;

public sealed class InventoryManager : MonoBehaviour
{
    [SerializeField] private InventorySlot[] slots;
    private readonly List<ItemData> items = new();

    public IReadOnlyList<ItemData> Items => items;

    private void Awake()
    {
        RefreshUI();
    }

    public bool AddItem(ItemData item)
    {
        if (item == null || slots == null || items.Count >= slots.Length)
        {
            return false;
        }

        items.Add(item);
        RefreshUI();
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        if (item == null || !items.Remove(item))
        {
            return false;
        }

        RefreshUI();
        return true;
    }

    public void RefreshUI()
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i]?.SetItem(i < items.Count ? items[i] : null);
        }
    }
}
