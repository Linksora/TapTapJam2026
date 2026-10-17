#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TapJam.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>给已制作的第一章场景补齐证据绑定，保留已有美术、碰撞体和自定义绑定。</summary>
public static class LevelOneEvidenceInstaller
{
    private const string ScenePath = "Assets/Scenes/Celia_TestScene.unity";
    private static readonly (string path, string clue)[] Bindings =
    {
        ("便利店/值班表", "SP01"),
        ("便利店/控制终端", "SP02"),
        ("便利店/热饮柜", "SP03"),
        ("便利店/冰柜侧座位", "SP04"),
        ("便利店/促销牌", "OP_Promotion"),
        ("便利店/文具货架", "OP_Pencil"),
        ("街道/教室布置图", "ST04"),
        ("街道/体育课占位单", "ST05")
    };

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorSceneManager.sceneOpened += (scene, _) => Configure(scene);
        EditorApplication.delayCall += ConfigureOpenScene;
    }

    [MenuItem("TapJam/Configure Level 1 Evidence")]
    public static void ConfigureOpenScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded) Configure(scene);
    }

    public static void Configure(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded || scene.path != ScenePath) return;
        Transform world = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "world") { world = root.transform; break; }
        if (world == null) return;

        bool changed = EnsureNoticeBoard(world);
        var bindings = new List<(string path, string clue)>();
        string tablePath = "Assets/Data/Level_01/WorldInteractionTable.csv";
        if (File.Exists(tablePath))
        {
            var table = CsvParser.Parse(File.ReadAllText(tablePath));
            var seen = new HashSet<string>();
            for (int row = 0; row < table.RowCount; row++)
                if (seen.Add(table.Get(row, "WorldPath"))) bindings.Add((table.Get(row, "WorldPath"), table.Get(row, "ClueID")));
        }
        else bindings.AddRange(Bindings);
        foreach (var binding in bindings)
        {
            Transform target = world.Find(binding.path);
            if (target == null) continue;
            WorldItem item = target.GetComponent<WorldItem>();
            if (item == null) { item = target.gameObject.AddComponent<WorldItem>(); changed = true; }
            var serialized = new SerializedObject(item);
            if (!string.IsNullOrWhiteSpace(serialized.FindProperty("clueId").stringValue)
                || serialized.FindProperty("itemData").objectReferenceValue != null) continue;
            serialized.FindProperty("clueId").stringValue = binding.clue;
            serialized.FindProperty("hideOnCollect").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            changed = true;
        }
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (InventoryManager inventory in root.GetComponentsInChildren<InventoryManager>(true))
                if (inventory.EnsurePageControls()) { EditorUtility.SetDirty(inventory); changed = true; }
        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool EnsureNoticeBoard(Transform world)
    {
        Transform street = world.Find("街道");
        if (street == null || street.Find("班级通知板") != null) return false;
        Transform template = street.Find("教室布置图");
        if (template == null) return false;
        GameObject notice = Object.Instantiate(template.gameObject, street);
        notice.name = "班级通知板";
        notice.transform.localPosition = template.localPosition + new Vector3(3f, 0, 0);
        if (notice.TryGetComponent<WorldItem>(out var item)) Object.DestroyImmediate(item);
        var labelObject = new GameObject("NoticeTitle", typeof(TextMesh)); labelObject.transform.SetParent(notice.transform, false);
        labelObject.transform.localPosition = new Vector3(0, -1.4f, -.1f);
        var label = labelObject.GetComponent<TextMesh>(); label.text = "班级通知"; label.font = GameFont.Primary;
        label.fontSize = 48; label.characterSize = .08f; label.anchor = TextAnchor.MiddleCenter;
        label.color = new Color(.06f,.3f,.18f);
        if (label.font != null) labelObject.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        labelObject.GetComponent<MeshRenderer>().sortingOrder = 8;
        return true;
    }
}
#endif
