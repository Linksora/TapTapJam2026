#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 Celia_TestScene 的 Canvas 下创建统一对话 UI（DialogueBox）：
/// 立绘 + 说话人 + 台词 + 两个选项按钮，组件引用自动连好。
/// 打开场景时自动检查，已存在则跳过；验收后可自行拖成 prefab。
/// </summary>
public static class DialogueBoxBuilder
{
    private const string ScenePath = "Assets/Scenes/Celia_TestScene.unity";

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

    [MenuItem("TapJam/Build Dialogue Box (Celia_TestScene)")]
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

        if (FindInScene(scene, "DialogueBox") != null)
        {
            return;
        }

        GameObject canvasObject = FindInScene(scene, "Canvas");
        if (canvasObject == null)
        {
            Debug.LogError("DialogueBox 创建失败：场景中没有 Canvas。");
            return;
        }

        Transform canvas = canvasObject.transform;

        // 根面板：贴底横条，左右各留 80，底部高过背包栏。
        GameObject root = CreateUIObject("DialogueBox", canvas);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(-160f, 230f);
        Image panel = root.AddComponent<Image>();
        panel.color = new Color(0.035f, 0.11f, 0.08f, 0.96f);
        panel.raycastTarget = true;

        // 立绘位
        GameObject portraitObject = CreateUIObject("Portrait", root.transform);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0f);
        portraitRect.anchorMax = new Vector2(0f, 0f);
        portraitRect.pivot = new Vector2(0f, 0f);
        portraitRect.anchoredPosition = new Vector2(16f, 14f);
        portraitRect.sizeDelta = new Vector2(200f, 212f);
        Image portrait = portraitObject.AddComponent<Image>();
        portrait.color = new Color(0.15f, 0.2f, 0.18f, 1f);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        // 说话人
        GameObject speakerObject = CreateUIObject("SpeakerText", root.transform);
        RectTransform speakerRect = speakerObject.GetComponent<RectTransform>();
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = new Vector2(116f, -14f);
        speakerRect.sizeDelta = new Vector2(-248f, 40f);
        Text speakerText = speakerObject.AddComponent<Text>();
        speakerText.text = string.Empty;
        speakerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        speakerText.fontSize = 32;
        speakerText.alignment = TextAnchor.MiddleLeft;
        speakerText.color = new Color(1f, 0.86f, 0.55f);
        speakerText.raycastTarget = false;

        // 台词
        GameObject contentObject = CreateUIObject("ContentText", root.transform);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        Stretch(contentRect, new Vector2(232f, 84f), new Vector2(-20f, -66f));
        Text contentText = contentObject.AddComponent<Text>();
        contentText.text = string.Empty;
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 28;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.color = new Color(0.98f, 0.94f, 0.82f);
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.raycastTarget = false;

        // 选项区：底部右侧横排两个按钮
        GameObject optionsObject = CreateUIObject("Options", root.transform);
        RectTransform optionsRect = optionsObject.GetComponent<RectTransform>();
        optionsRect.anchorMin = new Vector2(0f, 0f);
        optionsRect.anchorMax = new Vector2(1f, 0f);
        optionsRect.pivot = new Vector2(0.5f, 0f);
        optionsRect.anchoredPosition = Vector2.zero;
        optionsRect.sizeDelta = new Vector2(0f, 60f);
        HorizontalLayoutGroup layout = optionsObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.padding = new RectOffset(0, 24, 0, 0);
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var buttons = new Button[2];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i] = CreateOptionButton(optionsObject.transform, i + 1);
        }

        DialogueBoxView view = root.AddComponent<DialogueBoxView>();
        SerializedObject serialized = new(view);
        serialized.FindProperty("portraitImage").objectReferenceValue = portrait;
        serialized.FindProperty("speakerText").objectReferenceValue = speakerText;
        serialized.FindProperty("contentText").objectReferenceValue = contentText;
        SerializedProperty array = serialized.FindProperty("optionButtons");
        array.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);

        root.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("DialogueBox 已创建在 Celia_TestScene 的 Canvas 下（默认隐藏，运行时由 DialogueBoxView.Show 显示）。");
    }

    private static Button CreateOptionButton(Transform parent, int index)
    {
        GameObject buttonObject = CreateUIObject($"OptionButton{index}", parent);
        buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(380f, 56f);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.22f, 0.31f, 0.26f, 1f);
        image.raycastTarget = true;
        Button button = buttonObject.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.32f, 0.43f, 0.36f, 1f);
        colors.pressedColor = new Color(0.16f, 0.23f, 0.19f, 1f);
        button.colors = colors;

        GameObject labelObject = CreateUIObject("Label", buttonObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect, new Vector2(10f, 6f), new Vector2(-10f, -6f));
        Text label = labelObject.AddComponent<Text>();
        label.text = $"选项{index}";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.98f, 0.94f, 0.82f);
        label.raycastTarget = false;

        buttonObject.SetActive(false);
        return button;
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
