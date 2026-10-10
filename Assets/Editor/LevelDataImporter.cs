#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TapJam.EditorTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 Assets/Data/{level}/ 下的三张 CSV 导入为 Assets/Resources/LevelData/{level}/ 下的 ScriptableObject，
/// 供运行时 Resources.Load("LevelData/{level}/LevelDatabase") 读取。
/// 物品图标仍按 IconPath 文件名在 Assets/Generated/{level} 内查找 PNG 并连线；
/// 立绘只存文件名，运行时从 Resources/Portraits 按名字加载。
/// </summary>
public static class LevelDataImporter
{
    private const string DataRoot = "Assets/Data";
    private const string ResourceRoot = "Assets/Resources/LevelData";
    private const string GeneratedRoot = "Assets/Generated";

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
        for (int i = 0; i < scriptTable.RowCount; i++)
        {
            string id = scriptTable.Get(i, "DialogueID").Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dialogue);
            dialogueOrder.Add(dialogue);
        }

        var questionOrder = new List<QuestionData>();
        for (int i = 0; i < assemblyTable.RowCount; i++)
        {
            string id = assemblyTable.Get(i, "QuestionID").Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"关卡 {level} 导入完成：{clueOrder.Count} 条线索、{dialogueOrder.Count} 句对话、{questionOrder.Count} 道题目。");
        return database;
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
        }

        return asset;
    }

    private static Sprite LoadSprite(string level, string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        string leaf = resourcePath.Trim();
        int slash = leaf.LastIndexOf('/');
        if (slash >= 0)
        {
            leaf = leaf.Substring(slash + 1);
        }

        // 物品图标仍由 Generated 里的占位 PNG 提供，以后换正式图标覆盖同名文件即可。
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
