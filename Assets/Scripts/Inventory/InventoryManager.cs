using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class InventoryManager : MonoBehaviour
{
    [SerializeField] private InventorySlot[] slots;
    private readonly List<ItemData> items = new();

    public IReadOnlyList<ItemData> Items => items;

    private void Awake()
    {
        ResolveSlots();
        RefreshUI();
    }

    /// <summary>场景按约定名字搭建时 slots 序列化数组可能为空，运行时从 InventoryPanel 下兜底收集。</summary>
    private void ResolveSlots()
    {
        if (slots != null && slots.Length > 0)
        {
            return;
        }

        Transform panel = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "InventoryPanel");
        if (panel != null)
        {
            slots = panel.GetComponentsInChildren<InventorySlot>(true)
                .OrderBy(slot => slot.name)
                .ToArray();
        }
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
