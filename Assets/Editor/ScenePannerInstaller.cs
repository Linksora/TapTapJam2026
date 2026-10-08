#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Celia_TestScene 运行时组件的自动装配：
/// 1) Main Camera 挂 ScenePanner（方向键滚动场景，边界取“背景”精灵）；
/// 2) “人物”“对话框”挂 CameraScreenAnchor（前景锁定屏幕，不随背景滚走）。
/// 打开场景时自动检查，缺了才补，可重复执行。
/// </summary>
public static class ScenePannerInstaller
{
    private const string ScenePath = "Assets/Scenes/Celia_TestScene.unity";
    private static readonly string[] ScreenLockedObjects = { "人物", "对话框","人物背景框" };

    /// <summary>
    /// Unity 启动/脚本重编译后：若 Celia_TestScene 已打开却没挂 ScenePanner，自动补挂并保存。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void AutoInstallOnLoad()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += InstallIfOpen;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath)
        {
            InstallInScene(scene);
        }
    }

    private static void InstallIfOpen()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
        {
            InstallInScene(scene);
        }
    }

    [MenuItem("TapJam/Install Scene Runtime (Celia_TestScene)")]
    public static void Install()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"找不到场景：{ScenePath}");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool closeAfterInstall = !scene.IsValid() || !scene.isLoaded;
        if (closeAfterInstall)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        InstallInScene(scene);
        if (closeAfterInstall) EditorSceneManager.CloseScene(scene, true);
    }

    private static void InstallInScene(Scene scene)
    {
        GameObject cameraObject = FindInScene(scene, "Main Camera");
        GameObject backgroundObject = FindInScene(scene, "背景");
        if (cameraObject == null || backgroundObject == null)
        {
            Debug.LogError("ScenePanner 自动安装失败：场景中缺少 Main Camera 或 背景 对象，未做任何修改。");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            return;
        }

        SpriteRenderer backgroundRenderer = backgroundObject.GetComponent<SpriteRenderer>();
        if (backgroundRenderer == null)
        {
            Debug.LogError("自动安装失败：背景 对象上没有 SpriteRenderer，未做任何修改。");
            return;
        }

        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        bool changed = false;

        // 1) 相机：ScenePanner + 背景引用
        ScenePanner panner = cameraObject.GetComponent<ScenePanner>();
        if (panner == null)
        {
            panner = cameraObject.AddComponent<ScenePanner>();
            changed = true;
        }

        SerializedObject pannerSerialized = new(panner);
        if (pannerSerialized.FindProperty("background").objectReferenceValue == null)
        {
            pannerSerialized.FindProperty("background").objectReferenceValue = backgroundRenderer;
            pannerSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panner);
            changed = true;
        }

        // 2) 前景：人物、对话框锁屏幕
        foreach (string targetName in ScreenLockedObjects)
        {
            GameObject target = FindInScene(scene, targetName);
            if (target == null)
            {
                Debug.LogError($"自动安装失败：场景中找不到 {targetName}，跳过该对象。");
                continue;
            }

            CameraScreenAnchor anchor = target.GetComponent<CameraScreenAnchor>();
            if (anchor == null)
            {
                anchor = target.AddComponent<CameraScreenAnchor>();
                changed = true;
            }

            SerializedObject anchorSerialized = new(anchor);
            if (anchorSerialized.FindProperty("targetCamera").objectReferenceValue == null)
            {
                anchorSerialized.FindProperty("targetCamera").objectReferenceValue = cameraComponent;
                anchorSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(anchor);
                changed = true;
            }
        }

        // 所有组件都已就绪：不标脏、不保存。
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Celia_TestScene 运行时组件已自动装配：ScenePanner（相机）+ CameraScreenAnchor（人物、对话框）。");
        }
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject found = FindRecursive(root.transform, name);
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
