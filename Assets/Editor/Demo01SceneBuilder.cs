#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Demo01SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Demo01.unity";
    private const string GeneratedPath = "Assets/Generated/Demo01";

    [InitializeOnLoadMethod]
    private static void BuildOnceWhenScriptsAreReady()
    {
        if (!File.Exists(ScenePath))
        {
            EditorApplication.delayCall += BuildDemoScene;
        }
    }

    [MenuItem("TapJam/Rebuild Demo01 Scene")]
    public static void BuildDemoScene()
    {
        Directory.CreateDirectory(GeneratedPath);
        CreateDemoSprites();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedPath}/Player.png");
        Sprite yarnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedPath}/Yarn.png");
        Sprite appleSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedPath}/Apple.png");

        ItemData yarnData = CreateOrUpdateItem("YarnData", "yarn", "毛线团", yarnSprite);
        ItemData appleData = CreateOrUpdateItem("AppleData", "apple", "苹果", appleSprite);

        // 使用临时 Additive 场景，避免丢弃用户当前打开场景的未保存修改。
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "Demo01";

        Camera camera = CreateCamera(scene);
        GameObject world = CreateObject("World", scene);

        Canvas canvas = CreateCanvas(scene);
        DialogueUI dialogueUI = CreateDialogueUI(canvas.transform);
        RectTransform dragLayer = CreateDragLayer(canvas.transform);
        InventorySlot[] slots = CreateInventoryUI(canvas, dragLayer);

        GameObject systems = CreateObject("Systems", scene);
        GameObject inventoryObject = CreateObject("InventoryManager", scene, systems.transform);
        InventoryManager inventoryManager = inventoryObject.AddComponent<InventoryManager>();
        SetObjectReferenceArray(inventoryManager, "slots", slots);

        CreatePlayer(world.transform, dialogueUI, playerSprite);
        CreateWorldItem(world.transform, "Yarn", new Vector3(-3.7f, -1.4f, 0f), yarnSprite, yarnData, inventoryManager);
        CreateWorldItem(world.transform, "Apple", new Vector3(3.7f, -1.4f, 0f), appleSprite, appleData, inventoryManager);
        CreateEventSystem(scene);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Demo01 已生成：打开 Assets/Scenes/Demo01.unity 并进入 Play Mode 即可验收。");
    }

    private static Camera CreateCamera(Scene scene)
    {
        GameObject gameObject = CreateObject("Main Camera", scene);
        gameObject.tag = "MainCamera";
        gameObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = gameObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.17f, 0.16f);
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        gameObject.AddComponent<Physics2DRaycaster>();
        return camera;
    }

    private static Canvas CreateCanvas(Scene scene)
    {
        GameObject gameObject = CreateObject("Canvas", scene);
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static DialogueUI CreateDialogueUI(Transform canvas)
    {
        GameObject panel = CreateUIObject("DialoguePanel", canvas);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetRect(panelRect, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.36f), Vector2.zero, Vector2.zero);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.12f, 0.09f, 0.96f);
        panelImage.raycastTarget = false;

        GameObject textObject = CreateUIObject("DialogueText", panel.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        SetRect(textRect, Vector2.zero, Vector2.one, new Vector2(36f, 18f), new Vector2(-36f, -18f));
        Text text = textObject.AddComponent<Text>();
        text.text = "把背包物品拖到小毛身上试试";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 34;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.96f, 0.92f, 0.78f);
        text.raycastTarget = false;

        DialogueUI dialogueUI = panel.AddComponent<DialogueUI>();
        SetObjectReference(dialogueUI, "dialoguePanel", panel);
        SetObjectReference(dialogueUI, "dialogueText", text);
        return dialogueUI;
    }

    private static RectTransform CreateDragLayer(Transform canvas)
    {
        GameObject gameObject = CreateUIObject("DragLayer", canvas);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        return rect;
    }

    private static InventorySlot[] CreateInventoryUI(Canvas canvas, RectTransform dragLayer)
    {
        GameObject panel = CreateUIObject("InventoryPanel", canvas.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(0f, 170f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.09f, 0.08f, 0.98f);
        panelImage.raycastTarget = true;

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(530, 530, 20, 20);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var slots = new InventorySlot[5];
        for (int i = 0; i < slots.Length; i++)
        {
            GameObject slotObject = CreateUIObject($"Slot{i + 1:00}", panel.transform);
            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(130f, 130f);
            Image background = slotObject.AddComponent<Image>();
            background.color = new Color(0.25f, 0.33f, 0.28f, 1f);
            background.raycastTarget = true;

            GameObject iconObject = CreateUIObject("ItemIcon", slotObject.transform);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            SetRect(iconRect, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));
            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = true;

            DraggableItem draggable = iconObject.AddComponent<DraggableItem>();
            SetObjectReference(draggable, "rootCanvas", canvas);
            SetObjectReference(draggable, "dragLayer", dragLayer);

            InventorySlot slot = slotObject.AddComponent<InventorySlot>();
            SetObjectReference(slot, "itemIcon", icon);
            SetObjectReference(slot, "draggableItem", draggable);
            slots[i] = slot;
            icon.enabled = false;
        }

        // DragLayer 必须在背包之后绘制，临时图标才会始终位于最上方。
        dragLayer.SetAsLastSibling();
        return slots;
    }

    private static void CreatePlayer(Transform world, DialogueUI dialogueUI, Sprite sprite)
    {
        GameObject player = new("Player");
        player.transform.SetParent(world, false);
        player.transform.position = new Vector3(0f, 0.6f, 0f);
        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 2;
        player.AddComponent<BoxCollider2D>().size = new Vector2(3.3f, 5.1f);

        PlayerReaction reaction = player.AddComponent<PlayerReaction>();
        SetObjectReference(reaction, "dialogueUI", dialogueUI);
        ItemReceiver receiver = player.AddComponent<ItemReceiver>();
        UnityEventTools.AddPersistentListener(receiver.OnItemReceived, reaction.ReactToItem);
        EditorUtility.SetDirty(receiver);
    }

    private static void CreateWorldItem(Transform world, string name, Vector3 position, Sprite sprite,
        ItemData itemData, InventoryManager inventoryManager)
    {
        GameObject gameObject = new(name);
        gameObject.transform.SetParent(world, false);
        gameObject.transform.position = position;
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 3;
        gameObject.AddComponent<CircleCollider2D>();
        WorldItem worldItem = gameObject.AddComponent<WorldItem>();
        SetObjectReference(worldItem, "itemData", itemData);
        SetObjectReference(worldItem, "inventoryManager", inventoryManager);
    }

    private static void CreateEventSystem(Scene scene)
    {
        GameObject gameObject = CreateObject("EventSystem", scene);
        gameObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = gameObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    private static ItemData CreateOrUpdateItem(string assetName, string id, string displayName, Sprite icon)
    {
        string path = $"{GeneratedPath}/{assetName}.asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        SerializedObject serialized = new(item);
        serialized.FindProperty("itemId").stringValue = id;
        serialized.FindProperty("itemName").stringValue = displayName;
        serialized.FindProperty("icon").objectReferenceValue = icon;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void CreateDemoSprites()
    {
        WriteSprite("Yarn.png", 128, (x, y, size) =>
        {
            float dx = x - size * 0.5f;
            float dy = y - size * 0.5f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance > size * 0.43f) return Color.clear;
            Color baseColor = new(0.55f, 0.34f, 0.72f, 1f);
            if (Mathf.Abs(Mathf.Sin((dx + dy) * 0.16f)) < 0.18f || Mathf.Abs(Mathf.Sin((dx - dy) * 0.14f)) < 0.15f)
                return new Color(0.82f, 0.67f, 0.91f, 1f);
            return baseColor;
        });

        WriteSprite("Apple.png", 128, (x, y, size) =>
        {
            float cx = size * 0.5f;
            float cy = size * 0.44f;
            float nx = (x - cx) / (size * 0.39f);
            float ny = (y - cy) / (size * 0.38f);
            if (nx * nx + ny * ny <= 1f) return new Color(0.84f, 0.19f, 0.16f, 1f);
            if (x > size * 0.51f && x < size * 0.58f && y > size * 0.78f && y < size * 0.98f)
                return new Color(0.27f, 0.15f, 0.06f, 1f);
            if (x > size * 0.56f && x < size * 0.83f && y > size * 0.75f && y < size * 0.9f)
                return new Color(0.24f, 0.58f, 0.22f, 1f);
            return Color.clear;
        });

        WriteSprite("Player.png", 256, (x, y, size) =>
        {
            float nx = (x - size * 0.5f) / size;
            float ny = y / (float)size;
            bool body = (nx * nx) / 0.075f + ((ny - 0.37f) * (ny - 0.37f)) / 0.13f < 1f;
            bool head = (nx * nx) / 0.085f + ((ny - 0.72f) * (ny - 0.72f)) / 0.055f < 1f;
            bool leftEar = ny > 0.82f && ny < 0.99f && nx > -0.31f && nx < -0.04f && ny > 1.23f + nx * 1.35f;
            bool rightEar = ny > 0.82f && ny < 0.99f && nx < 0.31f && nx > 0.04f && ny > 1.23f - nx * 1.35f;
            if (!(body || head || leftEar || rightEar)) return Color.clear;
            Color fur = new(0.88f, 0.82f, 0.68f, 1f);
            bool eye = ny > 0.71f && ny < 0.76f && (Mathf.Abs(nx - 0.12f) < 0.025f || Mathf.Abs(nx + 0.12f) < 0.025f);
            return eye ? new Color(0.08f, 0.1f, 0.09f, 1f) : fur;
        });
    }

    private delegate Color PixelDrawer(int x, int y, int size);

    private static void WriteSprite(string fileName, int size, PixelDrawer drawer)
    {
        string assetPath = $"{GeneratedPath}/{fileName}";
        if (!File.Exists(assetPath))
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, drawer(x, y, size));
            texture.Apply();
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = fileName == "Player.png" ? 48f : 64f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static GameObject CreateObject(string name, Scene scene, Transform parent = null)
    {
        GameObject gameObject = new(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        if (parent != null) gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReferenceArray(Object target, string propertyName, Object[] values)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] oldScenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene oldScene in oldScenes)
        {
            if (oldScene.path == ScenePath) return;
        }

        var newScenes = new EditorBuildSettingsScene[oldScenes.Length + 1];
        newScenes[0] = new EditorBuildSettingsScene(ScenePath, true);
        System.Array.Copy(oldScenes, 0, newScenes, 1, oldScenes.Length);
        EditorBuildSettings.scenes = newScenes;
    }
}
#endif
