using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public sealed class WorldItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ItemData itemData;
    [Tooltip("使用当前章节的证据 ID，例如 SP01；填写后优先使用章节数据，无需拖生成的资源。")]
    [SerializeField] private string clueId;
    [SerializeField] private InventoryManager inventoryManager;
    [Tooltip("实体可拾取物品可勾选；货架、座位等调查物品取消勾选，收集的是记录。")]
    [SerializeField] private bool hideOnCollect = true;
    private bool pickedUp;
    private StoryManager story;
    public string WorldPath
    {
        get
        {
            string path = name;
            Transform parent = transform.parent;
            while (parent != null && parent.name != "world") { path = parent.name + "/" + path; parent = parent.parent; }
            return path;
        }
    }

    public ItemData Item
    {
        get
        {
            ResolveReferences();
            return itemData;
        }
    }

    public void Configure(ItemData value, InventoryManager inventory)
    {
        itemData = value;
        clueId = string.Empty;
        inventoryManager = inventory;
        pickedUp = false;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (inventoryManager == null)
            foreach (InventoryManager candidate in FindObjectsByType<InventoryManager>(FindObjectsInactive.Include))
                if (candidate.gameObject.scene == gameObject.scene) { inventoryManager = candidate; break; }
        if (story == null)
            foreach (StoryManager candidate in FindObjectsByType<StoryManager>(FindObjectsInactive.Include))
                if (candidate.gameObject.scene == gameObject.scene) { story = candidate; break; }
        if (!string.IsNullOrWhiteSpace(clueId) && story != null) itemData = story.GetClue(clueId.Trim());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ResolveReferences();
        if (eventData.button == PointerEventData.InputButton.Left && story != null && story.TryInspect(this)) return;
        if (eventData.button != PointerEventData.InputButton.Left || pickedUp || itemData == null || inventoryManager == null)
        {
            return;
        }

        // 剧情场景只允许收集当前搜证阶段的线索，旧 Demo 场景仍可直接拾取。
        if (story != null && story.enabled && !story.CanCollectClue(itemData.ItemId)) return;

        // 只有成功放入背包后才隐藏，避免背包已满时丢失物品。
        if (inventoryManager.AddItem(itemData))
        {
            pickedUp = true;
            GameAudio.Instance.Play(GameSound.Click);
            if (hideOnCollect) gameObject.SetActive(false);
        }
    }
}
