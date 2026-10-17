#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/TitleScene.unity";
    private const string GameplayScenePath = "Assets/Scenes/Celia_TestScene.unity";

    [MenuItem("TapJam/Build Title Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "TitleScene";

        GameObject cameraObject = CreateObject("Main Camera", scene);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.075f, 0.06f);

        GameObject canvasObject = CreateObject("Canvas", scene);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image backdrop = CreateImage("Backdrop", canvas.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.035f, 0.11f, 0.08f, 1f));
        backdrop.raycastTarget = false;

        Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/Demo01/Player.png");
        GameObject portraitObject = CreateUIObject("DetectivePortrait", canvas.transform);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0f);
        portraitRect.anchorMax = new Vector2(0.52f, 1f);
        portraitRect.offsetMin = new Vector2(50f, 25f);
        portraitRect.offsetMax = new Vector2(-20f, -25f);
        Image portrait = portraitObject.AddComponent<Image>();
        portrait.sprite = playerSprite;
        portrait.preserveAspect = true;
        portrait.color = new Color(1f, 1f, 1f, 0.92f);
        portrait.raycastTarget = false;

        Text title = CreateText("Title", canvas.transform, "小毛侦探事务所", 56, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.72f, 0.78f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(520f, 90f);
        title.color = new Color(1f, 0.86f, 0.55f);

        Text subtitle = CreateText("Subtitle", canvas.transform, "点击式解谜 Demo", 25, TextAnchor.MiddleCenter);
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = subtitleRect.anchorMax = new Vector2(0.72f, 0.68f);
        subtitleRect.anchoredPosition = Vector2.zero;
        subtitleRect.sizeDelta = new Vector2(400f, 50f);
        subtitle.color = new Color(0.78f, 0.84f, 0.76f);

        Button start = CreateButton(canvas.transform, "StartButton", "开始游戏", new Vector2(0.72f, 0.50f));
        Button continueButton = CreateButton(canvas.transform, "ContinueButton", "继续游戏", new Vector2(0.72f, 0.36f));
        Button quit = CreateButton(canvas.transform, "QuitButton", "退出游戏", new Vector2(0.72f, 0.22f));

        GameObject systems = CreateObject("TitleSystems", scene);
        TitleMenu menu = systems.AddComponent<TitleMenu>();
        SerializedObject serialized = new(menu);
        serialized.FindProperty("firstGameplayScene").stringValue = "Celia_TestScene";
        serialized.FindProperty("startButton").objectReferenceValue = start;
        serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
        serialized.FindProperty("quitButton").objectReferenceValue = quit;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject eventSystem = CreateObject("EventSystem", scene);
        eventSystem.AddComponent<EventSystem>();
        InputSystemUIInputModule module = eventSystem.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();

        EditorSceneManager.SaveScene(scene, ScenePath);
        ConfigureBuildSettings();
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        Debug.Log("TitleScene 已生成，并已加入 Build Settings 首位。");
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(360f, 72f);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.20f, 0.31f, 0.25f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.31f, 0.46f, 0.36f, 1f);
        colors.pressedColor = new Color(0.13f, 0.22f, 0.17f, 1f);
        button.colors = colors;

        Text text = CreateText("Label", buttonObject.transform, label, 30, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.raycastTarget = false;
        return button;
    }

    private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        Text text = gameObject.AddComponent<Text>();
        text.text = value;
        text.font = GameFont.Primary;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.98f, 0.94f, 0.82f);
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject CreateObject(string name, Scene scene)
    {
        GameObject gameObject = new(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        return gameObject;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void ConfigureBuildSettings()
    {
        var paths = new List<string> { ScenePath, GameplayScenePath };
        foreach (EditorBuildSettingsScene oldScene in EditorBuildSettings.scenes)
        {
            if (!paths.Contains(oldScene.path)) paths.Add(oldScene.path);
        }

        var scenes = new EditorBuildSettingsScene[paths.Count];
        for (int i = 0; i < paths.Count; i++)
            scenes[i] = new EditorBuildSettingsScene(paths[i], true);
        EditorBuildSettings.scenes = scenes;
    }
}
#endif
