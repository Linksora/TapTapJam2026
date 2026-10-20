using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryManager : MonoBehaviour
{
    [SerializeField] private InventorySlot[] slots;
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private Text pageLabel;
    private readonly List<ItemData> items = new();
    private RectTransform inventoryPanel;
    private int currentPage;

    public IReadOnlyList<ItemData> Items => items;
    public int CurrentPage => currentPage;
    public int ItemsPerPage => slots?.Length ?? 0;
    public int PageCount => Mathf.Max(1, ItemsPerPage > 0 ? Mathf.CeilToInt(items.Count / (float)ItemsPerPage) : 1);

    /// <summary>成功入包/移出后触发，供剧情系统做“证物集齐”判定。</summary>
    public event Action ItemsChanged;

    private void Awake()
    {
        ResolveSlots();
        EnsurePageControls();
        if (previousPageButton != null) previousPageButton.onClick.AddListener(PreviousPage);
        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);
        RefreshUI();
    }

    private void ResolveSlots()
    {
        Transform panel = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.gameObject.scene == gameObject.scene && t.name == "InventoryPanel");
        if (panel != null)
        {
            inventoryPanel = panel as RectTransform;
            slots = panel.GetComponentsInChildren<InventorySlot>(true)
                .OrderByDescending(slot => panel.InverseTransformPoint(slot.transform.position).y)
                .ThenBy(slot => panel.InverseTransformPoint(slot.transform.position).x)
                .ToArray();
        }
        else if (slots != null)
            slots = slots.Where(slot => slot != null).ToArray();
    }

    /// <summary>沿用现有六格布局，在下方补上翻页按钮；已有按钮保留位置和美术。</summary>
    public bool EnsurePageControls()
    {
        if (inventoryPanel == null) ResolveSlots();
        if (inventoryPanel == null || ItemsPerPage == 0) return false;
        bool changed = false;
        if (previousPageButton == null)
        {
            previousPageButton = inventoryPanel.Find("InventoryPreviousPageButton")?.GetComponent<Button>();
            if (previousPageButton == null)
            {
                previousPageButton = CreatePageButton("InventoryPreviousPageButton", "左箭头", -48f);
                changed = true;
            }
        }
        if (nextPageButton == null)
        {
            nextPageButton = inventoryPanel.Find("InventoryNextPageButton")?.GetComponent<Button>();
            if (nextPageButton == null)
            {
                nextPageButton = CreatePageButton("InventoryNextPageButton", "右箭头", 48f);
                changed = true;
            }
        }
        if (pageLabel == null)
        {
            pageLabel = inventoryPanel.Find("InventoryPageLabel")?.GetComponent<Text>();
            if (pageLabel == null)
            {
                var label = new GameObject("InventoryPageLabel", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(inventoryPanel, false);
                LayoutPageControl(label.GetComponent<RectTransform>(), 0f, new Vector2(64f, 32f));
                pageLabel = label.GetComponent<Text>();
                pageLabel.font = GameFont.Primary;
                pageLabel.fontSize = 18;
                pageLabel.color = new Color(0.04f, 0.32f, 0.2f);
                pageLabel.alignment = TextAnchor.MiddleCenter;
                pageLabel.raycastTarget = false;
                pageLabel.text = "1/1";
                changed = true;
            }
        }
        return changed;
    }

    private Button CreatePageButton(string objectName, string artName, float x)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(inventoryPanel, false);
        LayoutPageControl(go.GetComponent<RectTransform>(), x, new Vector2(28f, 38f));
        Image image = go.GetComponent<Image>();
        image.sprite = Resources.LoadAll<Sprite>($"Art/左右箭头/{artName}").FirstOrDefault();
        image.preserveAspect = true;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        Sprite bright = Resources.LoadAll<Sprite>($"Art/左右箭头/{artName}_选中").FirstOrDefault();
        button.spriteState = new SpriteState { highlightedSprite = bright, pressedSprite = bright, selectedSprite = bright };
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        return button;
    }

    private static void LayoutPageControl(RectTransform rect, float x, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 35f);
        rect.sizeDelta = size;
    }

    public bool AddItem(ItemData item)
    {
        if (item == null || ItemsPerPage == 0
            || items.Any(existing => existing != null && existing.ItemId == item.ItemId)) return false;
        items.Add(item);
        // 新证据所在页直接显示，打开笔记本时即可看到刚找到的证据。
        currentPage = (items.Count - 1) / ItemsPerPage;
        RefreshUI();
        ItemsChanged?.Invoke();
        return true;
    }

    public bool ReplaceItem(ItemData current, ItemData replacement)
    {
        if (current == null || replacement == null) return false;
        int index = items.FindIndex(item => item != null && item.ItemId == current.ItemId);
        if (index < 0 || items.Any(item => item != null && item.ItemId == replacement.ItemId)) return false;
        items[index] = replacement;
        currentPage = ItemsPerPage > 0 ? index / ItemsPerPage : 0;
        RefreshUI();
        ItemsChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        if (item == null || !items.Remove(item)) return false;
        RefreshUI();
        ItemsChanged?.Invoke();
        return true;
    }

    public void PreviousPage() => GoToPage(currentPage - 1);
    public void NextPage() => GoToPage(currentPage + 1);

    public void GoToPage(int page)
    {
        currentPage = Mathf.Clamp(page, 0, PageCount - 1);
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (slots == null) return;
        currentPage = Mathf.Clamp(currentPage, 0, PageCount - 1);
        for (int i = 0; i < slots.Length; i++)
        {
            int index = currentPage * slots.Length + i;
            slots[i]?.SetItem(index < items.Count ? items[index] : null);
        }
        UpdatePageButton(previousPageButton, currentPage > 0);
        UpdatePageButton(nextPageButton, currentPage + 1 < PageCount);
        if (pageLabel != null) pageLabel.text = $"{currentPage + 1}/{PageCount}";
    }

    private static void UpdatePageButton(Button button, bool available)
    {
        if (button == null) return;
        button.interactable = available;
        if (button.targetGraphic != null) button.targetGraphic.color = new Color(1f, 1f, 1f, available ? 1f : 0.3f);
    }

    private void OnDestroy()
    {
        if (previousPageButton != null) previousPageButton.onClick.RemoveListener(PreviousPage);
        if (nextPageButton != null) nextPageButton.onClick.RemoveListener(NextPage);
    }
}
