#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 Celia_TestScene 构建笔记本面板草稿：
/// - 独立全屏 NotebookCanvas（sortingOrder 40）
/// - NotebookPanel 滑动层：笔记本整图 + 书脊处的收起拉片 CloseNotebookButton
/// - 固定在屏幕右缘的拉开片 OpenNotebookButton
/// 点击拉片时整个滑动层左右平移（运行时 NotebookPanel 协程控制），不是 SetActive 弹出。
/// 菜单图为 1280x720 整图层（自身不接收射线），透明 HitArea 子物体做热区。
/// 位置仅为粗略草稿，可自行调整；已存在 NotebookCanvas 则跳过。
/// </summary>
public static class NotebookPanelBuilder
{
    private const string ScenePath = "Assets/Scenes/Celia_TestScene.unity";
    private const string ArtDir = "Assets/Resources/Art/笔记本界面/";

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

    [MenuItem("TapJam/Build Notebook Panel (Celia_TestScene)")]
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

        GameObject canvasObject = FindInScene(scene, "NotebookCanvas");
        if (canvasObject != null)
        {
            // 已存在时不重建，只补齐新增部件（描述文本框、射线穿透用 CanvasGroup），保留用户手动调整过的布局。
            GameObject existingPanel = FindRecursive(canvasObject.transform, "NotebookPanel");
            if (existingPanel != null)
            {
                EnsureDescriptionText(existingPanel.transform, scene);
                EnsureCanvasGroup(existingPanel, scene);
            }

            return;
        }

        canvasObject = new GameObject("NotebookCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        Stretch(canvasObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        // 1) 滑动层（运行时整体右移收起）
        GameObject panel = CreateUIObject("NotebookPanel", canvasObject.transform);
        Stretch(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        // CanvasGroup：收起时整块放行射线，避免全屏底图挡住 world 搜证与对话框点击。
        panel.AddComponent<CanvasGroup>();

        // 笔记本整图（256x144 美术，铺满 1280x720 即放大 5 倍，比例一致）
        GameObject notebookImageObject = CreateUIObject("NotebookImage", panel.transform);
        Stretch(notebookImageObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        Image notebookImage = notebookImageObject.AddComponent<Image>();
        notebookImage.sprite = LoadSingleSprite(ArtDir + "笔记本.png");
        // 底图铺满全屏但只是美术：不参与射线，否则透明区域会整块挡住下面的对话框和搜证点击；
        // 需要点击的地方由各自的 HitArea / 物品栏负责。
        notebookImage.raycastTarget = false;

        // 2) 收起拉片：随滑动层移动，热区在书脊处（美术 bbox 765,320 ~ 920,415）
        CreateTabButton(panel.transform, "CloseNotebookButton",
            "笔记本收起按钮", "笔记本收起按钮_选中",
            new Vector2(202.5f, -7.5f), new Vector2(155f, 95f));

        // 3) 拉开拉片：固定在屏幕右缘不随滑动移动（美术 bbox 1230,325 ~ 1280,420）
        CreateTabButton(canvasObject.transform, "OpenNotebookButton",
            "笔记本打开按钮", "笔记本打开按钮_选中",
            new Vector2(615f, -12.5f), new Vector2(50f, 95f));

        // 4) 上半页线索描述文本框（位置只按纸页区域粗略摆放，可自行调整）
        EnsureDescriptionText(panel.transform, scene);

        // 5) 运行时控制脚本
        canvasObject.AddComponent<NotebookPanel>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("笔记本面板草稿已创建：NotebookCanvas（编辑态展开，运行时默认收起到屏幕右侧）。");
    }

    /// <summary>滑动层上必须有 CanvasGroup，运行时 NotebookPanel 用它切换整块的射线拦截/穿透。</summary>
    private static void EnsureCanvasGroup(GameObject panel, Scene scene)
    {
        if (panel.GetComponent<CanvasGroup>() != null)
        {
            return;
        }

        panel.AddComponent<CanvasGroup>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("已在 NotebookPanel 上补建 CanvasGroup。");
    }

    /// <summary>
    /// 上半页描述文本框：点物品栏物品时由 NotebookPanel 写入“物品名+描述”。
    /// 纸页米色区域约在屏幕 x 950~1230（256x144 底图放大 5 倍），底部让开物品栏。
    /// </summary>
    private static void EnsureDescriptionText(Transform panel, Scene scene)
    {
        if (FindRecursive(panel, "ClueDescriptionText") != null)
        {
            return;
        }

        GameObject textObject = CreateUIObject("ClueDescriptionText", panel);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(450f, 50f);
        rect.sizeDelta = new Vector2(260f, 500f);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.lineSpacing = 1.1f;
        text.color = new Color(0.18f, 0.13f, 0.07f);
        text.raycastTarget = false; // 纯展示，不挡场景和物品栏点击
        text.text = "点击物品查看描述。";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("已在笔记本上半页补建 ClueDescriptionText。");
    }

    private static void CreateTabButton(Transform parent, string buttonName, string normalArt, string selectedArt,
        Vector2 hitPosition, Vector2 hitSize)
    {
        GameObject buttonObject = CreateUIObject(buttonName, parent);
        Stretch(buttonObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = LoadSingleSprite($"{ArtDir}{normalArt}.png");
        image.raycastTarget = false;

        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        button.targetGraphic = image;
        SpriteState spriteState = button.spriteState;
        spriteState.highlightedSprite = LoadSingleSprite($"{ArtDir}{selectedArt}.png");
        spriteState.pressedSprite = spriteState.highlightedSprite;
        button.spriteState = spriteState;
        DisableNavigation(button);

        GameObject hitArea = CreateUIObject("HitArea", buttonObject.transform);
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
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object asset in assets)
            {
                if (asset is Sprite)
                {
                    return asset as Sprite;
                }
            }

            Debug.LogError($"笔记本面板构建失败，找不到 Sprite：{assetPath}");
        }

        return sprite;
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
