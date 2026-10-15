#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SampleSceneDemoInstaller
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MarkerName = "SampleDemoSystems";

    [InitializeOnLoadMethod]
    private static void InstallOnceWhenScriptsAreReady()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(ScenePath) && !SceneIsInstalled())
            {
                Install();
            }
        };
    }

    [MenuItem("TapJam/Install or Repair SampleScene Demo")]
    public static void Install()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool closeAfterInstall = !scene.IsValid() || !scene.isLoaded;
        if (closeAfterInstall)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        GameObject player = FindInScene(scene, "人物");
        GameObject apple = FindInScene(scene, "可选物品1.Apple");
        GameObject yarn = FindInScene(scene, "可选物品2.yarn");
        GameObject cursor = FindInScene(scene, "光标");
        GameObject dialogueFrame = FindInScene(scene, "对话框");
        GameObject cameraObject = FindInScene(scene, "Main Camera");
        GameObject canvasObject = FindInScene(scene, "Canvas");

        if (player == null || apple == null || yarn == null || cursor == null || dialogueFrame == null ||
            cameraObject == null || canvasObject == null)
        {
            Debug.LogError("SampleScene 安装失败：缺少人物、物品、光标、对话框、相机或 Canvas。未移动任何对象。");
            if (closeAfterInstall) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        if (cameraObject.GetComponent<Physics2DRaycaster>() == null)
            cameraObject.AddComponent<Physics2DRaycaster>();

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        ConfigureCanvas(canvasObject);
        DialogueUI dialogueUI = ConfigureDialogue(canvas.transform, dialogueFrame);
        ClueDetailPanel clueDetailPanel = ConfigureClueDetailPanel(canvas.transform);

        ItemData appleData = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Generated/Demo01/AppleData.asset");
        ItemData yarnData = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Generated/Demo01/YarnData.asset");
        Sprite appleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/Demo01/Apple.png");
        Sprite yarnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/Demo01/Yarn.png");
        SetItemDescription(appleData, "一个普普通通的红苹果。看起来很新鲜，但小毛现在似乎没有胃口。");
        SetItemDescription(yarnData, "小毛最喜欢的毛线团。线头有被反复玩弄留下的轻微毛边。");

        GameObject systems = FindInScene(scene, MarkerName);
        if (systems == null) systems = new GameObject(MarkerName);
        if (systems.scene != scene) SceneManager.MoveGameObjectToScene(systems, scene);
        InventoryManager inventory = GetOrAdd<InventoryManager>(systems);

        PlayerReaction reaction = GetOrAdd<PlayerReaction>(player);
        SetReference(reaction, "dialogueUI", dialogueUI);
        ItemReceiver receiver = GetOrAdd<ItemReceiver>(player);
        if (receiver.OnItemReceived.GetPersistentEventCount() == 0)
        {
            UnityEventTools.AddPersistentListener(receiver.OnItemReceived, reaction.ReactToItem);
            EditorUtility.SetDirty(receiver);
        }
        GetOrAdd<BoxCollider2D>(player);

        RectTransform dragLayer = CreateOrGetDragLayer(canvas.transform);
        InventorySlot[] slots = CreateOrGetInventory(canvas, dragLayer, reaction, clueDetailPanel);
        SetReferenceArray(inventory, "slots", slots);

        ConfigureWorldItem(apple, appleData, appleSprite, inventory);
        ConfigureWorldItem(yarn, yarnData, yarnSprite, inventory);

        MouseCursorController cursorController = GetOrAdd<MouseCursorController>(cursor);
        SetReference(cursorController, "worldCamera", camera);
        SpriteRenderer cursorRenderer = cursor.GetComponent<SpriteRenderer>();
        if (cursorRenderer != null) cursorRenderer.sortingOrder = 100;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (closeAfterInstall) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("SampleScene Demo 已安装：保留原有物体位置，已补齐拾取、背包、拖拽、选择台词和跟随光标。");
    }

    private static void ConfigureCanvas(GameObject canvasObject)
    {
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        if (canvasObject.GetComponent<GraphicRaycaster>() == null) canvasObject.AddComponent<GraphicRaycaster>();

        GameObject oldText = FindChild(canvasObject.transform, "Text (TMP)");
        if (oldText != null) oldText.SetActive(false);
    }

    private static DialogueUI ConfigureDialogue(Transform canvas, GameObject dialogueFrame)
    {
        GameObject textObject = FindChild(canvas, "DialogueText_Runtime");
        if (textObject == null) textObject = CreateUIObject("DialogueText_Runtime", canvas);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(105f, 138f);
        rect.sizeDelta = new Vector2(800f, 84f);

        Text text = textObject.GetComponent<Text>();
        if (text == null) text = textObject.AddComponent<Text>();
        text.text = "点击货架上的物品进行拾取";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.98f, 0.94f, 0.82f);
        text.raycastTarget = false;

        DialogueUI dialogue = dialogueFrame.GetComponent<DialogueUI>();
        if (dialogue == null) dialogue = dialogueFrame.AddComponent<DialogueUI>();
        SetReference(dialogue, "dialoguePanel", dialogueFrame);
        SetReference(dialogue, "dialogueText", text);
        return dialogue;
    }

    private static RectTransform CreateOrGetDragLayer(Transform canvas)
    {
        GameObject gameObject = FindChild(canvas, "DragLayer");
        if (gameObject == null) gameObject = CreateUIObject("DragLayer", canvas);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        Stretch(rect);
        CanvasGroup group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        rect.SetAsLastSibling();
        return rect;
    }

    private static InventorySlot[] CreateOrGetInventory(Canvas canvas, RectTransform dragLayer,
        PlayerReaction reaction, ClueDetailPanel clueDetailPanel)
    {
        GameObject panel = FindChild(canvas.transform, "InventoryPanel");
        if (panel == null) panel = CreateUIObject("InventoryPanel", canvas.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(105f, 24f);
        panelRect.sizeDelta = new Vector2(620f, 102f);
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage == null) panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.11f, 0.08f, 0.92f);
        panelImage.raycastTarget = true;

        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(18, 18, 9, 9);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var slots = new InventorySlot[5];
        for (int i = 0; i < slots.Length; i++)
        {
            string slotName = $"Slot{i + 1:00}";
            GameObject slotObject = FindChild(panel.transform, slotName);
            if (slotObject == null) slotObject = CreateUIObject(slotName, panel.transform);
            slotObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 84f);
            Image background = slotObject.GetComponent<Image>();
            if (background == null) background = slotObject.AddComponent<Image>();
            background.color = new Color(0.23f, 0.31f, 0.25f, 1f);
            background.raycastTarget = true;

            GameObject iconObject = FindChild(slotObject.transform, "ItemIcon");
            if (iconObject == null) iconObject = CreateUIObject("ItemIcon", slotObject.transform);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            Stretch(iconRect, 8f);
            Image icon = iconObject.GetComponent<Image>();
            if (icon == null) icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = true;

            DraggableItem draggable = iconObject.GetComponent<DraggableItem>();
            if (draggable == null) draggable = iconObject.AddComponent<DraggableItem>();
            SetReference(draggable, "rootCanvas", canvas);
            SetReference(draggable, "dragLayer", dragLayer);
            SetReference(draggable, "selectionReaction", reaction);
            SetReference(draggable, "clueDetailPanel", clueDetailPanel);

            InventorySlot slot = slotObject.GetComponent<InventorySlot>();
            if (slot == null) slot = slotObject.AddComponent<InventorySlot>();
            SetReference(slot, "itemIcon", icon);
            SetReference(slot, "draggableItem", draggable);
            slots[i] = slot;
            icon.enabled = false;
        }

        dragLayer.SetAsLastSibling();
        return slots;
    }

    private static ClueDetailPanel ConfigureClueDetailPanel(Transform canvas)
    {
        GameObject panel = FindChild(canvas, "ClueDetailPanel");
        if (panel == null) panel = CreateUIObject("ClueDetailPanel", canvas);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(620f, 340f);
        Image background = panel.GetComponent<Image>();
        if (background == null) background = panel.AddComponent<Image>();
        background.color = new Color(0.035f, 0.11f, 0.08f, 0.98f);
        background.raycastTarget = true;

        GameObject iconObject = FindChild(panel.transform, "ClueIcon");
        if (iconObject == null) iconObject = CreateUIObject("ClueIcon", panel.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(34f, 0f);
        iconRect.sizeDelta = new Vector2(180f, 180f);
        Image icon = iconObject.GetComponent<Image>();
        if (icon == null) icon = iconObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        Text nameText = CreateOrGetText(panel.transform, "ClueName", new Vector2(245f, 230f),
            new Vector2(330f, 60f), 34, TextAnchor.MiddleLeft);
        Text descriptionText = CreateOrGetText(panel.transform, "ClueDescription", new Vector2(245f, 72f),
            new Vector2(330f, 190f), 25, TextAnchor.UpperLeft);
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject closeObject = FindChild(panel.transform, "CloseButton");
        if (closeObject == null) closeObject = CreateUIObject("CloseButton", panel.transform);
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-16f, -16f);
        closeRect.sizeDelta = new Vector2(56f, 48f);
        Image closeImage = closeObject.GetComponent<Image>();
        if (closeImage == null) closeImage = closeObject.AddComponent<Image>();
        closeImage.color = new Color(0.3f, 0.39f, 0.32f, 1f);
        Button closeButton = closeObject.GetComponent<Button>();
        if (closeButton == null) closeButton = closeObject.AddComponent<Button>();
        Text closeText = CreateOrGetText(closeObject.transform, "Label", Vector2.zero,
            new Vector2(56f, 48f), 28, TextAnchor.MiddleCenter);
        closeText.text = "×";

        ClueDetailPanel detail = panel.GetComponent<ClueDetailPanel>();
        if (detail == null) detail = panel.AddComponent<ClueDetailPanel>();
        SetReference(detail, "panelRoot", panel);
        SetReference(detail, "iconImage", icon);
        SetReference(detail, "itemNameText", nameText);
        SetReference(detail, "descriptionText", descriptionText);
        SetReference(detail, "closeButton", closeButton);
        panel.SetActive(false);
        return detail;
    }

    private static Text CreateOrGetText(Transform parent, string name, Vector2 anchoredPosition,
        Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject gameObject = FindChild(parent, name);
        if (gameObject == null) gameObject = CreateUIObject(name, parent);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Text text = gameObject.GetComponent<Text>();
        if (text == null) text = gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.98f, 0.94f, 0.82f);
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureWorldItem(GameObject gameObject, ItemData data, Sprite sprite, InventoryManager inventory)
    {
        if (gameObject == null || data == null) return;
        SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
        if (renderer != null && sprite != null)
        {
            renderer.sprite = sprite;
            gameObject.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
        }

        BoxCollider2D collider = GetOrAdd<BoxCollider2D>(gameObject);
        collider.isTrigger = false;
        WorldItem worldItem = GetOrAdd<WorldItem>(gameObject);
        SetReference(worldItem, "itemData", data);
        SetReference(worldItem, "inventoryManager", inventory);
    }

    private static bool SceneIsInstalled()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return false;
        return FindInScene(scene, MarkerName) != null &&
               FindInScene(scene, "InventoryPanel") != null &&
               FindInScene(scene, "ClueDetailPanel")?.GetComponent<ClueDetailPanel>() != null &&
               FindInScene(scene, "可选物品1.Apple")?.GetComponent<WorldItem>() != null;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject found = FindRecursive(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindChild(Transform parent, string name)
    {
        return FindRecursive(parent, name);
    }

    private static GameObject FindRecursive(Transform current, string name)
    {
        if (current.name == name) return current.gameObject;
        for (int i = 0; i < current.childCount; i++)
        {
            GameObject found = FindRecursive(current.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component == null ? gameObject.AddComponent<T>() : component;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReferenceArray(Object target, string propertyName, Object[] values)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetItemDescription(ItemData item, string description)
    {
        if (item == null) return;
        SerializedObject serialized = new(item);
        SerializedProperty property = serialized.FindProperty("description");
        if (property != null && string.IsNullOrWhiteSpace(property.stringValue))
        {
            property.stringValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }
    }
}
#endif
