#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡场景自动挂载 StoryManager：
/// 场景名匹配 Level_数字（如 Level_01）或 Celia_TestScene（测试用，默认 Level_01）时，
/// 打开场景自动检查，缺了就在 LevelSystems 上补挂，不需要手动拖任何引用。
/// </summary>
public static class StoryManagerInstaller
{
    private const string SystemsName = "LevelSystems";
    private static readonly Regex LevelScenePattern = new(@"Level_\d+", RegexOptions.Compiled);

    [InitializeOnLoadMethod]
    private static void AutoInstallOnLoad()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += InstallInOpenScenes;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (IsStoryScene(scene))
        {
            InstallInScene(scene);
        }
    }

    private static void InstallInOpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (IsStoryScene(scene))
            {
                InstallInScene(scene);
            }
        }
    }

    [MenuItem("TapJam/Install Story Manager (Current Level Scene)")]
    public static void InstallMenu()
    {
        Scene active = SceneManager.GetActiveScene();
        if (IsStoryScene(active))
        {
            InstallInScene(active);
        }
        else
        {
            Debug.LogWarning($"当前场景 {active.name} 不是关卡场景（命名需含 Level_数字，或使用 Celia_TestScene）。");
        }
    }

    private static bool IsStoryScene(Scene scene)
    {
        return scene.IsValid()
               && (LevelScenePattern.IsMatch(scene.name) || scene.name == "Celia_TestScene");
    }

    private static void InstallInScene(Scene scene)
    {
        if (EditorApplication.isPlaying || !scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject systems = FindInScene(scene, SystemsName);
        if (systems == null)
        {
            systems = new GameObject(SystemsName);
            SceneManager.MoveGameObjectToScene(systems, scene);
        }

        bool changed = false;

        // 清理旧版本删掉脚本后残留的 Missing Script。
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(systems);
        changed |= removed > 0;

        if (systems.GetComponent<StoryManager>() == null)
        {
            systems.AddComponent<StoryManager>();
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"StoryManager 已自动挂到 {scene.name} 的 {SystemsName}（关卡 ID 运行时按场景名解析）。");
        }
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
