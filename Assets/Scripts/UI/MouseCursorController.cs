using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Image))]
public sealed class MouseCursorController : MonoBehaviour
{
    private const int CursorCanvasOrder = 32767;

    [SerializeField] private Camera worldCamera;

    private RectTransform cursorRect;
    private Image cursorImage;
    private Canvas cursorCanvas;
    private DialogueBoxView dialogueBox;
    private ScenePanner scenePanner;
    private StoryManager storyManager;
    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> raycastResults = new();
    private string currentCharacter = "小毛";
    private CursorState currentState;
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> CursorSprites = new();

    private enum CursorState
    {
        Normal,
        Move,
        Investigate
    }

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        cursorRect = GetComponent<RectTransform>();
        cursorImage = GetComponent<Image>();
        cursorCanvas = GetComponentInParent<Canvas>();
        if (worldCamera != null) scenePanner = worldCamera.GetComponent<ScenePanner>();

        if (cursorCanvas == null || cursorCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogError("[MouseCursorController] 光标 Image 必须放在 Screen Space - Overlay Canvas 下。", this);
            enabled = false;
            return;
        }

        cursorCanvas.overrideSorting = true;
        cursorCanvas.sortingOrder = CursorCanvasOrder;
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = false;
        SetCursorSprite(GetAnimalName(currentCharacter), currentState);
    }

    private void OnEnable()
    {
        Cursor.visible = false;
        BindDialogueBox();
    }

    private void LateUpdate()
    {
        if (cursorRect == null || Mouse.current == null) return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        CursorState nextState = GetCursorState(screenPosition);
        if (nextState != currentState)
        {
            currentState = nextState;
            SetCursorSprite(GetAnimalName(currentCharacter), currentState);
        }

        RectTransform canvasRect = cursorCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, null, out Vector2 localPosition))
        {
            // pivot 就是热点（指尖），直接把 pivot 点放到鼠标位置，无需任何偏移补偿。
            cursorRect.anchoredPosition = localPosition;
        }
    }

    private CursorState GetCursorState(Vector2 screenPosition)
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (scenePanner == null && worldCamera != null) scenePanner = worldCamera.GetComponent<ScenePanner>();
        if (storyManager == null) storyManager = FindFirstObjectByType<StoryManager>();
        // Movement takes priority whether it came from the keyboard or a held on-screen arrow button.
        if (scenePanner != null && scenePanner.IsMoving) return CursorState.Move;
        if (storyManager != null && storyManager.enabled && !storyManager.CanShowInvestigationCursor)
            return CursorState.Normal;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            pointerEventData ??= new PointerEventData(eventSystem);
            pointerEventData.position = screenPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            if (raycastResults.Count == 0) return CursorState.Normal;

            // RaycastAll is sorted by the UI/EventSystem, so a visible UI element keeps priority
            // over a world object behind it while world-item hover still works via Physics2DRaycaster.
            GameObject hitObject = raycastResults[0].gameObject;
            if (hitObject.GetComponentInParent<WorldItem>() != null ||
                hitObject.GetComponentInParent<ItemReceiver>() != null)
                return CursorState.Investigate;
            return CursorState.Normal;
        }

        if (worldCamera == null) return CursorState.Normal;

        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(
            screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
        Collider2D hit = Physics2D.OverlapPoint(worldPoint);
        if (hit == null) return CursorState.Normal;
        if (hit.GetComponentInParent<WorldItem>() != null ||
            hit.GetComponentInParent<ItemReceiver>() != null)
            return CursorState.Investigate;
        return CursorState.Normal;
    }

    private static string GetAnimalName(string characterId)
    {
        if (!string.IsNullOrEmpty(characterId) && characterId.Contains("猫头鹰")) return "猫头鹰";
        if (!string.IsNullOrEmpty(characterId) && characterId.Contains("狗")) return "狗";
        return "猫";
    }

    private void SetCursorSprite(string animal, CursorState state)
    {
        if (cursorImage == null) return;
        string stateName = state switch
        {
            CursorState.Move => "移动",
            CursorState.Investigate => "调查",
            _ => "正常"
        };
        Sprite sprite = LoadCompositeSprite(animal, stateName);
        if (sprite == null)
        {
            Debug.LogWarning($"[MouseCursorController] 找不到鼠标图片：Art/鼠标/鼠标-{animal}/{stateName}-{animal}", this);
            return;
        }

        cursorImage.sprite = sprite;

        // Sprite 的导入 Pivot 由美术设置；RectTransform 使用同一像素位置作为定位点。
        Rect spriteRect = sprite.rect;
        cursorRect.pivot = new Vector2(
            sprite.pivot.x / spriteRect.width,
            sprite.pivot.y / spriteRect.height);

        // 不改 RectTransform 尺寸或缩放，图案按 Inspector 配置的矩形显示。
    }

    private static Sprite LoadCompositeSprite(string animal, string stateName)
    {
        string key = $"{animal}/{stateName}";
        if (CursorSprites.TryGetValue(key, out Sprite cached)) return cached;

        Sprite[] parts = Resources.LoadAll<Sprite>($"Art/鼠标/鼠标-{animal}/{stateName}-{animal}");
        if (parts == null || parts.Length == 0) return null;
        if (parts.Length == 1)
        {
            CursorSprites[key] = parts[0];
            return parts[0];
        }

        Rect bounds = parts[0].rect;
        for (int i = 1; i < parts.Length; i++)
        {
            bounds = Rect.MinMaxRect(
                Mathf.Min(bounds.xMin, parts[i].rect.xMin), Mathf.Min(bounds.yMin, parts[i].rect.yMin),
                Mathf.Max(bounds.xMax, parts[i].rect.xMax), Mathf.Max(bounds.yMax, parts[i].rect.yMax));
        }

        // Treat the first imported slice as the hotspot authoring source for this composite.
        Vector2 sourcePivot = parts[0].rect.min + parts[0].pivot;
        Vector2 compositePivot = new(
            (sourcePivot.x - bounds.xMin) / bounds.width,
            (sourcePivot.y - bounds.yMin) / bounds.height);
        Sprite composite = Sprite.Create(parts[0].texture, bounds, compositePivot, 100f);
        composite.name = $"{stateName}-{animal}-Cursor";
        CursorSprites[key] = composite;
        return composite;
    }

    private void BindDialogueBox()
    {
        if (dialogueBox == null)
            dialogueBox = FindFirstObjectByType<DialogueBoxView>(FindObjectsInactive.Include);
        if (dialogueBox == null) return;

        dialogueBox.MainCharacterChanged -= OnMainCharacterChanged;
        dialogueBox.MainCharacterChanged += OnMainCharacterChanged;
        OnMainCharacterChanged(dialogueBox.CurrentMainCharacterId);
    }

    private void OnMainCharacterChanged(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return;
        currentCharacter = characterId;
        SetCursorSprite(GetAnimalName(currentCharacter), currentState);
    }

    private void OnDisable()
    {
        if (dialogueBox != null) dialogueBox.MainCharacterChanged -= OnMainCharacterChanged;
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        Cursor.visible = !hasFocus;
    }
}
