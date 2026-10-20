using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>世界物品的双图状态：普通；鼠标悬停或左键按下时高亮。</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public sealed class WorldItemHighlight : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightedSprite;

    private bool hovered;
    private bool pressed;

    /// <summary>剧情改变物件状态后，更新普通图，避免鼠标移出时恢复旧图。</summary>
    public void SetNormalSprite(Sprite sprite)
    {
        if (sprite == null) return;
        ResolveRenderer();
        normalSprite = sprite;
        Refresh();
    }

    private void Awake()
    {
        ResolveRenderer();
        Refresh();
    }

    private void Reset()
    {
        ResolveRenderer();
        // 点击范围按普通图固定，不随高亮描边变大，避免边缘反复进入/退出。
        if (normalSprite == null) return;
        BoxCollider2D hitArea = GetComponent<BoxCollider2D>();
        hitArea.size = normalSprite.bounds.size;
        hitArea.offset = normalSprite.bounds.center;
    }

    private void ResolveRenderer()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (normalSprite == null && targetRenderer != null) normalSprite = targetRenderer.sprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pressed = true;
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pressed = false;
        Refresh();
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        Refresh();
    }

    private void Refresh()
    {
        if (targetRenderer == null) return;
        targetRenderer.sprite = (hovered || pressed) && highlightedSprite != null
            ? highlightedSprite : normalSprite;
    }
}
