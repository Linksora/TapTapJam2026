using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryManager : MonoBehaviour
{
    [SerializeField] private InventorySlot[] slots;
    private readonly List<ItemData> items = new();
    private RectTransform inventoryPanel;
    private RectTransform scrollContent;
    private ScrollRect scroll;

    public IReadOnlyList<ItemData> Items => items;

    /// <summary>成功入包/移出后触发，供剧情系统做“证物集齐”判定。</summary>
    public event Action ItemsChanged;

    private void Awake()
    {
        ResolveSlots();
        RefreshUI();
    }

    /// <summary>场景按约定名字搭建时 slots 序列化数组可能为空，运行时从 InventoryPanel 下兜底收集。</summary>
    private void ResolveSlots()
    {
        Transform panel = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.gameObject.scene == gameObject.scene && t.name == "InventoryPanel");
        if (panel != null)
        {
            inventoryPanel = panel as RectTransform;
            // 以当前布局为准，包含用户新增的格子，并按从上到下、从左到右填充。
            slots = panel.GetComponentsInChildren<InventorySlot>(true)
                .OrderByDescending(slot => panel.InverseTransformPoint(slot.transform.position).y)
                .ThenBy(slot => panel.InverseTransformPoint(slot.transform.position).x)
                .ToArray();
        }
        else if (slots != null)
            slots = slots.Where(slot => slot != null).ToArray();
    }

    public bool AddItem(ItemData item)
    {
        if (item == null || slots == null)
        {
            return false;
        }

        // 同一条证物只能入包一次：拼凑答对/面质可能被重复触发。
        if (items.Any(existing => existing != null && existing.ItemId == item.ItemId))
        {
            return false;
        }

        if (items.Count >= slots.Length && !AddOverflowSlot()) return false;

        items.Add(item);
        RefreshUI();
        ItemsChanged?.Invoke();
        return true;
    }

    // 沿用现有格子的尺寸和间距。线索超过手工摆放的容量时，增加下一格并允许滚动。
    private bool AddOverflowSlot()
    {
        if (inventoryPanel == null || slots.Length == 0) return false;
        RectTransform first = (RectTransform)slots[0].transform;
        var firstRow = slots.Where(slot => Mathf.Abs(((RectTransform)slot.transform).anchoredPosition.y
            - first.anchoredPosition.y) < 1f).ToArray();
        int columns = firstRow.Length;
        float rowStep = slots.Length > columns
            ? Mathf.Abs(first.anchoredPosition.y - ((RectTransform)slots[columns].transform).anchoredPosition.y)
            : first.rect.height + 6f;

        if (scrollContent == null)
        {
            scrollContent = new GameObject("InventoryContent", typeof(RectTransform)).GetComponent<RectTransform>();
            scrollContent.SetParent(inventoryPanel, false);
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = Vector2.one;
            scrollContent.pivot = new Vector2(0.5f, 1f);
            scrollContent.sizeDelta = new Vector2(0f, inventoryPanel.rect.height);
            foreach (InventorySlot slot in slots)
            {
                Vector2 position = ((RectTransform)slot.transform).anchoredPosition;
                slot.transform.SetParent(scrollContent, false);
                ((RectTransform)slot.transform).anchoredPosition = position;
            }
            if (inventoryPanel.GetComponent<RectMask2D>() == null)
                inventoryPanel.gameObject.AddComponent<RectMask2D>();
            scroll = inventoryPanel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = inventoryPanel;
            scroll.content = scrollContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        int index = slots.Length;
        InventorySlot added = Instantiate(slots[index % columns], scrollContent);
        added.name = $"Slot{index + 1:00}";
        RectTransform rect = (RectTransform)added.transform;
        rect.anchoredPosition = new Vector2(((RectTransform)firstRow[index % columns].transform).anchoredPosition.x,
            first.anchoredPosition.y - index / columns * rowStep);
        added.SetItem(null);
        slots = slots.Concat(new[] { added }).ToArray();
        float height = Mathf.Max(inventoryPanel.rect.height, -rect.anchoredPosition.y + rect.rect.height * rect.pivot.y);
        scrollContent.sizeDelta = new Vector2(0f, height);
        NotebookPanel notebook = inventoryPanel.GetComponentInParent<NotebookPanel>();
        if (notebook != null) notebook.BindInventoryItem(added.GetComponentInChildren<DraggableItem>(true));
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0f;
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        if (item == null || !items.Remove(item))
        {
            return false;
        }

        RefreshUI();
        ItemsChanged?.Invoke();
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
