using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 线索详情视图：只负责展示 ItemData，不持有或修改背包数据。
/// </summary>
public sealed class ClueDetailPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text itemNameText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Button closeButton;

    public ItemData CurrentItem { get; private set; }

    private void Awake()
    {
        Font chineseFont = GameFont.Primary;
        if (chineseFont != null)
        {
            if (itemNameText != null) itemNameText.font = chineseFont;
            if (descriptionText != null) descriptionText.font = chineseFont;
            Text closeLabel = closeButton != null ? closeButton.GetComponentInChildren<Text>() : null;
            if (closeLabel != null) closeLabel.font = chineseFont;
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }

        // 初始显隐由场景/Prefab 决定。不要在 Awake 再次 Hide：
        // inactive 面板首次由 Show 激活时才会执行 Awake，否则会吞掉第一次点击。
    }

    public void Show(ItemData item)
    {
        if (item == null || panelRoot == null)
        {
            return;
        }

        CurrentItem = item;
        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = item.Icon != null;
        }

        if (itemNameText != null)
        {
            itemNameText.text = item.ItemName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrWhiteSpace(item.Description)
                ? "暂时没有更多调查记录。"
                : item.Description;
        }

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        CurrentItem = null;
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
}
