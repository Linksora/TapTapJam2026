#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 Celia_TestScene 构建设置面板草稿：
/// - 游戏主 Canvas 右上角齿轮 SettingsButton
/// - 独立全屏 SettingsCanvas（默认隐藏）：半透明黑遮罩 + 居中设置界面背景
/// - 继续游戏 / 退出游戏 / 返回主界面 三个菜单按钮（美术整图层 + 透明热区，悬停切选中图）
/// - 喇叭音量条 + 5 个透明档位按钮（VolumeButton1~5）
/// 位置仅为粗略草稿，所有 RectTransform 可在场景里自行调整；已存在 SettingsCanvas 则跳过。
/// </summary>
public static class SettingsPanelBuilder
{
    private const string ScenePath = "Assets/Scenes/Celia_TestScene.unity";

    private const string ArtDir = "Assets/Resources/Art/设置界面/";
    private const string GearPath = "Assets/Resources/Art/对话UI/设置图标.png";

    [InitializeOnLoadMethod]
    private static void AutoBuildOnLoad()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += BuildIfOpen;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath)
        {
            BuildInScene(scene);
        }
    }

    private static void BuildIfOpen()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
        {
            BuildInScene(scene);
        }
    }

    [MenuItem("TapJam/Build Settings Panel (Celia_TestScene)")]
    public static void Build()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"找不到场景：{ScenePath}");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool closeAfter = !scene.IsValid() || !scene.isLoaded;
        if (closeAfter)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        BuildInScene(scene);
        if (closeAfter) EditorSceneManager.CloseScene(scene, true);
    }

    private static void BuildInScene(Scene scene)
    {
        if (EditorApplication.isPlaying || !scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (FindInScene(scene, "SettingsCanvas") != null)
        {
            return;
        }

        // 1) 主 Canvas 上的齿轮入口
        GameObject mainCanvasObject = FindInScene(scene, "Canvas");
        if (mainCanvasObject != null && FindInScene(scene, "SettingsButton") == null)
        {
            CreateGearButton(mainCanvasObject.transform);
        }

        // 2) 独立全屏 Canvas
        GameObject canvasObject = new("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        Stretch(canvasObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        // Panel：整个面板层（含遮罩），运行时整体 SetActive
        GameObject panel = CreateUIObject("Panel", canvasObject.transform);
        Stretch(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        // 半透明黑色全屏遮罩（同时挡住游戏点击）
        GameObject dim = CreateUIObject("Dim", panel.transform);
        Stretch(dim.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.6f);

        // 居中面板背景
        GameObject panelImageObject = CreateUIObject("PanelImage", panel.transform);
        RectTransform panelImageRect = panelImageObject.GetComponent<RectTransform>();
        panelImageRect.sizeDelta = new Vector2(1000f, 562f);
        Image panelImage = panelImageObject.AddComponent<Image>();
        panelImage.sprite = LoadSingleSprite(ArtDir + "设置界面背景.png");
        panelImage.raycastTarget = false;

        // 3) 三行菜单：美术图是 1280x720 整图层，按钮图铺满 PanelImage，
        //    自身不接收射线，另加透明 HitArea 子物体做热区。
        CreateMenuRow(panelImageObject.transform, "ContinueButton", "继续游戏",
            new Vector2(-23f, 150f), new Vector2(336f, 90f));
        CreateMenuRow(panelImageObject.transform, "QuitButton", "退出游戏",
            new Vector2(-23f, 47f), new Vector2(328f, 86f));
        CreateMenuRow(panelImageObject.transform, "TitleButton", "返回主界面",
            new Vector2(8f, -51f), new Vector2(375f, 78f));

        // 4) 音量条 + 5 个档位热区（坐标相对于 515x185 的音量条图，整体放大 1.25 倍）
        GameObject volumeBar = CreateUIObject("VolumeBarImage", panelImageObject.transform);
        RectTransform volumeRect = volumeBar.GetComponent<RectTransform>();
        volumeRect.anchoredPosition = new Vector2(0f, -185f);
        volumeRect.sizeDelta = new Vector2(644f, 231f);
        Image volumeImage = volumeBar.AddComponent<Image>();
        volumeImage.sprite = LoadFirstSprite(ArtDir + "音量条6.png");
        volumeImage.raycastTarget = false;
        volumeImage.preserveAspect = true;

        Vector2[] slotPositions =
        {
            new(-16f, 21f), new(40f, 21f), new(96f, 21f), new(153f, 21f), new(209f, 21f)
        };
        for (int i = 0; i < 5; i++)
        {
            GameObject slot = CreateUIObject($"VolumeButton{i + 1}", volumeBar.transform);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchoredPosition = slotPositions[i];
            slotRect.sizeDelta = new Vector2(50f, 111f);
            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(1f, 1f, 1f, 0f);
            Button slotButton = slot.AddComponent<Button>();
            slotButton.targetGraphic = slotImage;
            DisableNavigation(slotButton);
        }

        // 5) 挂运行时脚本，灌入 6 张音量状态图（资源引用，场景复制不会丢）
        SettingsPanel settingsPanel = canvasObject.AddComponent<SettingsPanel>();
        SerializedObject serialized = new(settingsPanel);
        SerializedProperty spritesProperty = serialized.FindProperty("volumeSprites");
        spritesProperty.arraySize = 6;
        for (int i = 1; i <= 6; i++)
        {
            spritesProperty.GetArrayElementAtIndex(i - 1).objectReferenceValue =
                LoadFirstSprite($"{ArtDir}音量条{i}.png");
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settingsPanel);

        panel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("设置面板草稿已创建：SettingsCanvas（默认隐藏）+ 主 Canvas 齿轮按钮。");
    }

    private static void CreateGearButton(Transform parent)
    {
        GameObject gear = CreateUIObject("SettingsButton", parent);
        RectTransform rect = gear.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-48f, -48f);
        rect.sizeDelta = new Vector2(64f, 64f);

        Image image = gear.AddComponent<Image>();
        image.sprite = LoadSingleSprite(GearPath);
        image.preserveAspect = true;

        Button button = gear.AddComponent<Button>();
        button.targetGraphic = image;
        DisableNavigation(button);
    }

    private static void CreateMenuRow(Transform parent, string buttonName, string artName,
        Vector2 hitPosition, Vector2 hitSize)
    {
        GameObject row = CreateUIObject(buttonName, parent);
        Stretch(row.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        Image image = row.AddComponent<Image>();
        image.sprite = LoadSingleSprite($"{ArtDir}{artName}.png");
        image.raycastTarget = false;
        image.preserveAspect = false;

        Button button = row.AddComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        button.targetGraphic = image;
        SpriteState spriteState = button.spriteState;
        spriteState.highlightedSprite = LoadSingleSprite($"{ArtDir}{artName}_选中.png");
        spriteState.pressedSprite = spriteState.highlightedSprite;
        button.spriteState = spriteState;
        DisableNavigation(button);

        // 透明热区：点击事件会冒泡到父级 Button，触发整图切换为选中态。
        GameObject hitArea = CreateUIObject("HitArea", row.transform);
        RectTransform hitRect = hitArea.GetComponent<RectTransform>();
        hitRect.anchoredPosition = hitPosition;
        hitRect.sizeDelta = hitSize;
        Image hitImage = hitArea.AddComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private static Sprite LoadSingleSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            Debug.LogError($"设置面板构建失败，找不到 Sprite：{assetPath}");
        }

        return sprite;
    }

    // Multiple 模式的贴图（音量条）用 LoadAllAssetsAtPath 取第一个子精灵。
    private static Sprite LoadFirstSprite(string assetPath)
    {
        Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (single != null)
        {
            return single;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                return sprite;
            }
        }

        Debug.LogError($"设置面板构建失败，找不到 Sprite：{assetPath}");
        return null;
    }

    private static void DisableNavigation(Button button)
    {
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            GameObject found = FindRecursive(rootObject.transform, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindRecursive(Transform current, string name)
    {
        if (current.name == name)
        {
            return current.gameObject;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            GameObject found = FindRecursive(current.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
