#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>第三关素材占位场景。只创建新场景，不修改前两关或共享 prefab。</summary>
public static class LevelThreeSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Level3.unity";
    private const string SpritePath = "Assets/Generated/Level_03/Placeholder.png";
    private static readonly string[] Areas = {
        "守宫家玄关", "守宫家书桌区", "楼下街口", "窄巷长凳", "松鼠家前厅", "松鼠家里间", "松鼠家院子", "事务所"
    };
    private static readonly string[][] Props = {
        new[] { "TR01_门锁与钥匙挂钩", "TR04_门垫推演" },
        new[] { "TR02_手机与插座", "TR03_花纹纸角" },
        new[] { "TR05_花车", "TR06_路口录像" },
        new[] { "TR07_长凳与格栅纸片" },
        new[] { "LT01_请假确认", "LT02_包装袋与外披", "LT03_铅笔与橡皮", "LT07_来访便条" },
        new[] { "LT04_纪念日信", "LT05_学校联系便条", "结尾_信的最后一问" },
        new[] { "LT06_日程纸", "收尾_试穿外披", "收尾_空纸", "可选_小狗消息" },
        new[] { "E03_次日纸条" }
    };

    [MenuItem("TapJam/Create Level 3 Placeholder Scene")]
    public static void Create()
    {
        if (File.Exists(ScenePath))
        {
            Debug.Log("Level3 已存在，保留现有场景，不覆盖。路径：" + ScenePath);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
        var texture = new Texture2D(16, 16);
        texture.SetPixels(Enumerable.Repeat(Color.white, 256).ToArray());
        texture.Apply();
        File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);

        var previous = SceneManager.GetActiveScene();
        if (!AssetDatabase.CopyAsset("Assets/Scenes/Level1.unity", ScenePath))
            throw new InvalidOperationException("无法复制第一关作为第三关基础场景。");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var world = scene.GetRootGameObjects().Single(root => root.name == "world");
            foreach (Transform child in world.transform.Cast<Transform>().ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            world.transform.position = Vector3.zero;
            world.transform.localScale = Vector3.one;
            var systems = scene.GetRootGameObjects().Single(root => root.name == "LevelSystems");
            var story = systems.GetComponent<StoryManager>();
            var storyData = new SerializedObject(story);
            storyData.FindProperty("overrideLevelId").stringValue = "Level_03";
            storyData.FindProperty("playOnStart").boolValue = false;
            storyData.ApplyModifiedPropertiesWithoutUndo();
            // 素材布局阶段没有剧情数据库，禁用剧情组件，避免误播前两关或缺库报错。
            story.enabled = false;
            var background = systems.GetComponent<BackgroundController>();
            var data = new SerializedObject(background);
            var entries = data.FindProperty("backgrounds");
            entries.arraySize = Areas.Length;
            SpriteRenderer first = null;
            for (int i = 0; i < Areas.Length; i++)
            {
                var group = new GameObject(Areas[i]);
                group.transform.SetParent(world.transform, false);
                var backdrop = MakeSprite("背景", group.transform, sprite, new Vector3(0, 0, 5),
                    new Vector2(30, 12), Color.HSVToRGB(i / 10f, .15f, .82f), -100);
                if (i == 0) first = backdrop;
                Label(Areas[i] + " · 素材待替换", group.transform, new Vector3(0, 3.4f, 0), .12f);
                for (int p = 0; p < Props[i].Length; p++)
                {
                    float x = (p - (Props[i].Length - 1) * .5f) * 4;
                    var prop = MakeSprite(Props[i][p], group.transform, sprite, new Vector3(x, 0, 0),
                        new Vector2(2.5f, 1.5f), new Color(.7f, .55f, .35f), 1);
                    prop.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(2.5f, 1.5f);
                    prop.gameObject.AddComponent<WorldItem>();
                    var item = new SerializedObject(prop.GetComponent<WorldItem>());
                    item.FindProperty("hideOnCollect").boolValue = false;
                    item.ApplyModifiedPropertiesWithoutUndo();
                    Label(Props[i][p], group.transform, new Vector3(x, 1.2f, -1), .065f);
                }
                // 布局预览阶段各区串联，正式剧情接入后按 Trace / Letter 导航表限制。
                if (i > 0) Exit(group.transform, "预览上一处", Areas[i - 1], -11, sprite);
                if (i + 1 < Areas.Length) Exit(group.transform, "预览下一处", Areas[i + 1], 11, sprite);
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = Areas[i];
                entry.FindPropertyRelative("aliases").arraySize = 0;
                entry.FindPropertyRelative("root").objectReferenceValue = group;
                entry.FindPropertyRelative("boundsRenderer").objectReferenceValue = backdrop;
                entry.FindPropertyRelative("audioBackground").stringValue = string.Empty;
                group.SetActive(i == 0);
            }
            data.FindProperty("initialBackground").stringValue = Areas[0];
            data.ApplyModifiedPropertiesWithoutUndo();
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographicSize = 6;
            var panner = new SerializedObject(camera.GetComponent<ScenePanner>());
            panner.FindProperty("background").objectReferenceValue = first;
            panner.FindProperty("moveSpeed").floatValue = 12;
            panner.ApplyModifiedPropertiesWithoutUndo();
            foreach (var box in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DialogueBoxView>(true)))
                box.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene);
            if (!EditorBuildSettings.scenes.Any(entry => entry.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("LEVEL3_PLACEHOLDER_CREATED: 8个背景分组、14个证据位置及收尾位置，已复用第二关基础 UI。剧情尚未接入。");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("TapJam/Configure Level 3 From Level 1 Prefabs")]
    public static void ConfigureFromLevelOne()
    {
        // 不在已经打开的场景上替换，避免丢弃用户尚未保存的修改。
        if (Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Any(open => open.path == ScenePath))
            throw new InvalidOperationException("请先保存并关闭 Level3 场景，再执行配置菜单。");
        if (!File.Exists(ScenePath)) Create();
        const string template = "Assets/Generated/Level_03/Level1Template.unity";
        if (File.Exists(template)) throw new InvalidOperationException("临时模板已存在，请先检查：" + template);
        if (!AssetDatabase.CopyAsset("Assets/Scenes/Level1.unity", template))
            throw new InvalidOperationException("无法复制 Level1 场景。");
        var previous = SceneManager.GetActiveScene();
        Scene old = default;
        Scene scene = default;
        try
        {
            old = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            scene = EditorSceneManager.OpenScene(template, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var oldWorld = old.GetRootGameObjects().Single(root => root.name == "world");
            var world = scene.GetRootGameObjects().Single(root => root.name == "world");
            foreach (Transform child in world.transform.Cast<Transform>().ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            world.transform.position = oldWorld.transform.position;
            world.transform.rotation = oldWorld.transform.rotation;
            world.transform.localScale = oldWorld.transform.localScale;
            foreach (Transform child in oldWorld.transform.Cast<Transform>().ToArray())
            {
                child.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(child.gameObject, scene);
                child.SetParent(world.transform, true);
            }
            var backgroundSprite = SlotSprite("BackgroundSlot", 300, 120, new Color(.84f, .85f, .82f));
            var propSprite = SlotSprite("PropSlot", 25, 15, new Color(.7f, .55f, .35f));
            var exitSprite = SlotSprite("ExitSlot", 15, 20, new Color(.35f, .55f, .55f));
            foreach (var renderer in world.GetComponentsInChildren<SpriteRenderer>(true))
            {
                // 只转换我们生成的占位图片；用户已经替换的正式图保持原样。
                if (AssetDatabase.GetAssetPath(renderer.sprite) != SpritePath) continue;
                renderer.sprite = renderer.name == "背景" ? backgroundSprite
                    : renderer.GetComponent<BackgroundSwitch>() != null ? exitSprite : propSprite;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.color = Color.white;
            }
            foreach (Transform group in world.transform)
            {
                var labels = group.Find("编辑辅助标签")?.gameObject;
                if (labels == null)
                {
                    labels = new GameObject("编辑辅助标签");
                    labels.transform.SetParent(group, false);
                }
                foreach (Transform child in group.Cast<Transform>().Where(child => child.name.StartsWith("标签_", StringComparison.Ordinal)).ToArray())
                    child.SetParent(labels.transform, true);
            }
            var systems = scene.GetRootGameObjects().Single(root => root.name == "LevelSystems");
            var story = systems.GetComponent<StoryManager>();
            var storyData = new SerializedObject(story);
            storyData.FindProperty("overrideLevelId").stringValue = "Level_03";
            storyData.FindProperty("playOnStart").boolValue = false;
            storyData.ApplyModifiedPropertiesWithoutUndo();
            story.enabled = false;
            var controller = systems.GetComponent<BackgroundController>();
            var config = new SerializedObject(controller);
            config.FindProperty("backgrounds").arraySize = 0;
            config.FindProperty("initialBackground").stringValue = Areas[0];
            config.ApplyModifiedPropertiesWithoutUndo();
            controller.CollectBackgrounds();
            PrefabUtility.RecordPrefabInstancePropertyModifications(story);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographicSize = 6;
            var panner = new SerializedObject(camera.GetComponent<ScenePanner>());
            panner.FindProperty("background").objectReferenceValue = world.transform.Find(Areas[0] + "/背景").GetComponent<SpriteRenderer>();
            panner.FindProperty("moveSpeed").floatValue = 12;
            panner.ApplyModifiedPropertiesWithoutUndo();
            foreach (var box in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DialogueBoxView>(true)))
            {
                box.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(box.gameObject);
            }
            if (!EditorSceneManager.SaveScene(scene, ScenePath, true))
                throw new InvalidOperationException("第三关保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("LEVEL3_LEVEL1_PREFABS_CONFIGURED: 已继承 Level1 场景预制件，保留第三关 world，图片槽采用 Simple 和白色，剧情暂未启用。");
        }
        finally
        {
            if (old.IsValid() && old.isLoaded) EditorSceneManager.CloseScene(old, true);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            AssetDatabase.DeleteAsset(template);
        }
    }

    private static Sprite SlotSprite(string name, int width, int height, Color color)
    {
        string path = "Assets/Generated/Level_03/" + name + ".png";
        if (!File.Exists(path))
        {
            var image = new Texture2D(width, height);
            image.SetPixels(Enumerable.Repeat(color, width * height).ToArray());
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 10;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Vector3 position, Vector2 size, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static void Label(string text, Transform parent, Vector3 position, float size)
    {
        var obj = new GameObject("标签_" + text);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        var label = obj.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.fontSize = 64;
        label.characterSize = size;
        label.color = new Color(.15f, .13f, .12f);
        label.font = GameFont.Primary;
        if (label.font != null) obj.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        obj.GetComponent<MeshRenderer>().sortingOrder = 5;
    }

    private static void Exit(Transform parent, string name, string target, float x, Sprite sprite)
    {
        var renderer = MakeSprite(name, parent, sprite, new Vector3(x, 0, 0), new Vector2(1.5f, 2), new Color(.35f, .55f, .55f), 2);
        var travel = renderer.gameObject.AddComponent<BackgroundSwitch>();
        renderer.GetComponent<BoxCollider2D>().size = new Vector2(1.5f, 2);
        var data = new SerializedObject(travel);
        data.FindProperty("targetBackground").stringValue = target;
        data.FindProperty("requireSearchPhase").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        Label(name, parent, new Vector3(x, 1.5f, -1), .07f);
    }
}
#endif
