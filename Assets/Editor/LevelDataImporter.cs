#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TapJam.EditorTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 Assets/Data/{level}/ 下的关卡 CSV 导入为 Assets/Resources/LevelData/{level}/ 下的 ScriptableObject，
/// 供运行时 Resources.Load("LevelData/{level}/LevelDatabase") 读取。
/// 物品图标优先按 IconPath 从 Resources 加载正式证据 Sprite，兼容旧版 Generated 图标；
/// 立绘只存文件名，运行时从 Resources/Portraits 按名字加载。
/// </summary>
public static class LevelDataImporter
{
    private const string DataRoot = "Assets/Data";
    private const string ResourceRoot = "Assets/Resources/LevelData";
    private const string GeneratedRoot = "Assets/Generated";

    [InitializeOnLoadMethod]
    private static void ImportBeforePlay()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) ImportAll();
        };
    }

    [MenuItem("TapJam/Import All Level CSV")]
    public static void ImportAll()
    {
        if (!Directory.Exists(DataRoot))
        {
            Debug.LogWarning($"找不到 CSV 目录：{DataRoot}");
            return;
        }

        foreach (string directory in Directory.GetDirectories(DataRoot))
        {
            if (File.Exists($"{directory}/ClueTable.csv"))
            {
                string level = Path.GetFileName(directory);
                ImportLevel(level);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("全部关卡 CSV 导入完成。");
    }

    public static LevelDatabase ImportLevel(string level)
    {
        string inputDir = $"{DataRoot}/{level}";
        string outputRoot = $"{ResourceRoot}/{level}";

        // 增量更新：目录/asset 已存在就复用，不删除重建——GUID 保持稳定，外部引用不断。
        // 新建分支的写入可靠性由 CreateOrUpdate 内部保证；CSV 删掉的行由末尾 CleanupOrphans 清理。
        Directory.CreateDirectory($"{outputRoot}/Clues");
        Directory.CreateDirectory($"{outputRoot}/Dialogues");
        Directory.CreateDirectory($"{outputRoot}/Questions");

        CsvTable clueTable = CsvParser.Parse(File.ReadAllText($"{inputDir}/ClueTable.csv"));
        CsvTable scriptTable = CsvParser.Parse(File.ReadAllText($"{inputDir}/ScriptTable.csv"));
        CsvTable assemblyTable = CsvParser.Parse(File.ReadAllText($"{inputDir}/AssemblyTable.csv"));

        ValidateRowWidth(level, "ClueTable", clueTable);
        ValidateRowWidth(level, "ScriptTable", scriptTable);
        ValidateRowWidth(level, "AssemblyTable", assemblyTable);

        var clueOrder = new List<ItemData>();
        var clueMap = new Dictionary<string, ItemData>();
        var nextClueIds = new Dictionary<string, string>();

        for (int i = 0; i < clueTable.RowCount; i++)
        {
            string id = clueTable.Get(i, "ClueID").Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            ItemData clue = CreateOrUpdate<ItemData>($"{outputRoot}/Clues/{id}.asset");
            var serialized = new SerializedObject(clue);
            SetString(serialized, "itemId", id);
            SetString(serialized, "itemName", clueTable.Get(i, "Name"));
            SetObject(serialized, "icon", LoadSprite(level, clueTable.Get(i, "IconPath")));
            SetString(serialized, "description", clueTable.Get(i, "Description"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clue);

            clueOrder.Add(clue);
            clueMap[id] = clue;
            nextClueIds[id] = clueTable.Get(i, "NextClueID").Trim();
        }

        // 第二遍连 NextClue 引用，允许正向/反向任意顺序。
        foreach (var pair in clueMap)
        {
            string nextId = nextClueIds[pair.Key];
            ItemData next = string.IsNullOrEmpty(nextId) ? null : clueMap.GetValueOrDefault(nextId);
            var serialized = new SerializedObject(pair.Value);
            SetObject(serialized, "nextClue", next);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pair.Value);
        }

        var dialogueOrder = new List<DialogueData>();
        var dialogueIds = new HashSet<string>();
        for (int i = 0; i < scriptTable.RowCount; i++)
        {
            string id = scriptTable.Get(i, "DialogueID").Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            dialogueIds.Add(id);
            DialogueData dialogue = CreateOrUpdate<DialogueData>($"{outputRoot}/Dialogues/{id}.asset");
            var serialized = new SerializedObject(dialogue);
            SetString(serialized, "dialogueId", id);
            SetObject(serialized, "entryClue", clueMap.GetValueOrDefault(scriptTable.Get(i, "EntryClueID").Trim()));
            SetString(serialized, "speaker", scriptTable.Get(i, "Speaker"));
            SetString(serialized, "text", scriptTable.Get(i, "TextContent"));
            SetString(serialized, "portraitPath", scriptTable.Get(i, "PortraitPath").Trim());
            SetString(serialized, "option1Text", scriptTable.Get(i, "Option1_Text"));
            SetString(serialized, "option1Target", scriptTable.Get(i, "Option1_Target").Trim());
            SetString(serialized, "option2Text", scriptTable.Get(i, "Option2_Text"));
            SetString(serialized, "option2Target", scriptTable.Get(i, "Option2_Target").Trim());
            SetString(serialized, "nextTarget", scriptTable.Get(i, "NextDialogueID").Trim());
            SetString(serialized, "background", scriptTable.Get(i, "Background").Trim());
            SetObject(serialized, "rewardClue", clueMap.GetValueOrDefault(scriptTable.Get(i, "RewardClueID").Trim()));
            SetString(serialized, "mainCharacters", scriptTable.Get(i, "MainCharacters"));
            SetString(serialized, "searchStartZone", scriptTable.Get(i, "SearchStartZone"));
            SetString(serialized, "stageAction", scriptTable.Get(i, "StageAction"));
            SetString(serialized, "conditionGroup", scriptTable.Get(i, "ConditionGroup"));
            SetString(serialized, "conditionStatement", scriptTable.Get(i, "ConditionStatement"));
            SetString(serialized, "conditionValue", scriptTable.Get(i, "ConditionValue"));
            SetString(serialized, "alternateTarget", scriptTable.Get(i, "AlternateTarget"));
            serialized.FindProperty("holdSeconds").floatValue = float.TryParse(scriptTable.Get(i, "HoldSeconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hold) ? hold : 0;
            string nodeType = scriptTable.Get(i, "NodeType").Trim();
            serialized.FindProperty("isTransition").boolValue = nodeType == "Transition" || nodeType == "Search"
                || scriptTable.Get(i, "Speaker").Trim() == "【玩法】";

            string searchRaw = scriptTable.Get(i, "SearchClueIDs").Trim();
            string[] searchClueIds = string.IsNullOrEmpty(searchRaw)
                ? System.Array.Empty<string>()
                : searchRaw.Split('|', System.StringSplitOptions.RemoveEmptyEntries);
            for (int s = 0; s < searchClueIds.Length; s++)
            {
                searchClueIds[s] = searchClueIds[s].Trim();
            }

            SetStringArray(serialized, "searchClueIds", searchClueIds);
            string optionalRaw = scriptTable.Get(i, "OptionalClueIDs").Trim();
            string[] optionalIds = optionalRaw.Split('|', System.StringSplitOptions.RemoveEmptyEntries);
            for (int s = 0; s < optionalIds.Length; s++) optionalIds[s] = optionalIds[s].Trim();
            SetStringArray(serialized, "optionalClueIds", optionalIds);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dialogue);
            dialogueOrder.Add(dialogue);
        }

        var questionOrder = new List<QuestionData>();
        var questionIds = new HashSet<string>();
        for (int i = 0; i < assemblyTable.RowCount; i++)
        {
            string id = assemblyTable.Get(i, "QuestionID").Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            questionIds.Add(id);
            QuestionData question = CreateOrUpdate<QuestionData>($"{outputRoot}/Questions/{id}.asset");
            var serialized = new SerializedObject(question);
            SetString(serialized, "questionId", id);
            SetString(serialized, "statement", assemblyTable.Get(i, "StatementText"));

            string optionsRaw = assemblyTable.Get(i, "Options");
            string[] options = string.IsNullOrEmpty(optionsRaw)
                ? new string[0]
                : optionsRaw.Split('|');
            SetStringArray(serialized, "options", options);

            int correctOption = int.TryParse(assemblyTable.Get(i, "CorrectOption"), out int parsed) ? parsed : -1;
            if (options.Length == 0)
            {
                correctOption = -1;
            }

            serialized.FindProperty("correctOption").intValue = correctOption;
            SetString(serialized, "successTarget", assemblyTable.Get(i, "SuccessDialogueID").Trim());
            SetObject(serialized, "successClue", clueMap.GetValueOrDefault(assemblyTable.Get(i, "SuccessClueID").Trim()));
            SetString(serialized, "wrongTarget", assemblyTable.Get(i, "WrongDialogueID").Trim());
            SetString(serialized, "wrongPortraitPath", assemblyTable.Get(i, "WrongPortraitPath").Trim());
            SetString(serialized, "hesitantPortraitPath", assemblyTable.Get(i, "HesitantPortraitPath").Trim());
            SetObject(serialized, "targetClue", clueMap.GetValueOrDefault(assemblyTable.Get(i, "TargetClueID").Trim()));
            SetString(serialized, "characterId", assemblyTable.Get(i, "CharacterID").Trim());
            SetStringArray(serialized, "wrongClueTargets", assemblyTable.Get(i, "WrongClueTargets").Split('|', System.StringSplitOptions.RemoveEmptyEntries));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(question);
            questionOrder.Add(question);
        }

        LevelDatabase database = CreateOrUpdate<LevelDatabase>($"{outputRoot}/LevelDatabase.asset");
        var dbSerialized = new SerializedObject(database);
        SetString(dbSerialized, "startDialogueId",
            scriptTable.RowCount > 0 ? scriptTable.Get(0, "DialogueID").Trim() : string.Empty);
        SetObjectArray(dbSerialized, "clues", clueOrder);
        SetObjectArray(dbSerialized, "dialogues", dialogueOrder);
        SetObjectArray(dbSerialized, "questions", questionOrder);
        dbSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
        LevelDataExtensionsImporter.Import(inputDir, outputRoot, database);

        AssetDatabase.SaveAssets();

        // 清理 CSV 已删除行对应的旧 asset（只动三个子目录，LevelDatabase 自身永不删）。
        int removed = 0;
        removed += CleanupOrphans($"{outputRoot}/Clues", clueMap.Keys);
        removed += CleanupOrphans($"{outputRoot}/Dialogues", dialogueIds);
        removed += CleanupOrphans($"{outputRoot}/Questions", questionIds);

        AssetDatabase.Refresh();
        Debug.Log($"关卡 {level} 导入完成：{clueOrder.Count} 条线索、{dialogueOrder.Count} 句对话、{questionOrder.Count} 道题目，清理失效 asset {removed} 个。");
        return database;
    }

    /// <summary>删除 folder 下文件名（不含扩展名）不在 validIds 中的 .asset。</summary>
    private static int CleanupOrphans(string folder, IEnumerable<string> validIds)
    {
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var valid = new HashSet<string>(validIds);
        int removed = 0;
        foreach (string assetPath in Directory.GetFiles(folder, "*.asset"))
        {
            string normalized = assetPath.Replace('\\', '/');
            string id = Path.GetFileNameWithoutExtension(normalized);
            if (!valid.Contains(id))
            {
                AssetDatabase.DeleteAsset(normalized);
                removed++;
            }
        }

        return removed;
    }

    private static void ValidateRowWidth(string level, string tableName, CsvTable table)
    {
        for (int i = 0; i < table.RowCount; i++)
        {
            int width = table.GetRowLength(i);
            if (width != table.ColumnCount)
            {
                // 数据行的第 1 列是 ID，方便在 CSV 里定位出错的那一行。
                string firstColumn = table.Get(i, "ClueID") != string.Empty
                    ? table.Get(i, "ClueID")
                    : table.Get(i, "DialogueID") != string.Empty
                        ? table.Get(i, "DialogueID")
                        : table.Get(i, "QuestionID");
                Debug.LogError(
                    $"[{level}] {tableName} 第 {i + 2} 行（{firstColumn}）有 {width} 列，表头是 {table.ColumnCount} 列——多了或少了逗号会导致后续字段整体错位。");
            }
        }
    }

    private static T CreateOrUpdate<T>(string assetPath) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            // 关键：先落盘，再取回 AssetDatabase 已注册的持久对象。
            // 若直接用 CreateAsset 返回的临时实例做 SerializedObject 写入，
            // 修改不会落盘——表现为生成的 asset 存在但字段全空，运行时对话全部找不到。
            AssetDatabase.SaveAssets();
            asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        return asset;
    }

    private static Sprite LoadSprite(string level, string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        string path = resourcePath.Trim();
        if (path.StartsWith("Assets/Resources/")) path = path.Substring("Assets/Resources/".Length);
        if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 4);
        // 正式证据图使用 Multiple Sprite 导入，主资源是 Texture，必须读取切片。
        Sprite[] sprites = Resources.LoadAll<Sprite>(path);
        if (sprites.Length > 0) return sprites[0];

        string leaf = resourcePath.Trim();
        int slash = leaf.LastIndexOf('/');
        if (slash >= 0)
        {
            leaf = leaf.Substring(slash + 1);
        }

        if (path.StartsWith("Art/"))
            Debug.LogWarning($"[{level}] 找不到正式证据图标 Resources/{path}。");
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedRoot}/{level}/{leaf}.png");
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        serialized.FindProperty(propertyName).stringValue = value ?? string.Empty;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        serialized.FindProperty(propertyName).objectReferenceValue = value;
    }

    private static void SetStringArray(SerializedObject serialized, string propertyName, string[] values)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }

    private static void SetObjectArray<T>(SerializedObject serialized, string propertyName, IList<T> values)
        where T : Object
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
#endif
