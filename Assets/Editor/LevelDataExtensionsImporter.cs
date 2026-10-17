#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TapJam.EditorTools;
using UnityEditor;
using UnityEngine;

public static class LevelDataExtensionsImporter
{
    public static void Import(string input, string output, LevelDatabase database)
    {
        var serialized = new SerializedObject(database);
        var groups = new List<DeductionData>();
        if (File.Exists(input + "/ReasoningTable.csv"))
        {
            CsvTable table = CsvParser.Parse(File.ReadAllText(input + "/ReasoningTable.csv"));
            var rows = new Dictionary<string,List<int>>();
            for(int i=0;i<table.RowCount;i++)
            {
                string id=table.Get(i,"GroupID"); if(string.IsNullOrEmpty(id))continue;
                if(!rows.ContainsKey(id))rows[id]=new List<int>();rows[id].Add(i);
            }
            Directory.CreateDirectory(output+"/Deductions");
            foreach(var pair in rows)
            {
                string path=output+"/Deductions/"+pair.Key+".asset";
                var group=AssetDatabase.LoadAssetAtPath<DeductionData>(path);
                if(group==null){group=ScriptableObject.CreateInstance<DeductionData>();AssetDatabase.CreateAsset(group,path);}
                int first=pair.Value[0];
                group.groupId=pair.Key;group.title=table.Get(first,"Title");group.mode=table.Get(first,"Mode");
                group.confirmText=table.Get(first,"ConfirmText");group.nextTarget=table.Get(first,"NextTarget");
                group.initialFromGroup=table.Get(first,"InitialFromGroup");group.requiredClueIds=Split(table.Get(first,"RequiredClueIDs"));
                group.statements=pair.Value.Select(row=>new DeductionData.Statement{
                    id=table.Get(row,"StatementID"),sentence=table.Get(row,"Sentence"),options=Split(table.Get(row,"Options")),
                    correctOption=int.TryParse(table.Get(row,"CorrectOption"),out int index)?index:-1,
                    wrongFeedback=table.Get(row,"WrongFeedback").Split('|'),hint=table.Get(row,"Hint"),
                    sourceClueIds=Split(table.Get(row,"SourceClueIDs"))}).ToArray();
                EditorUtility.SetDirty(group);groups.Add(group);
            }
            foreach(string path in Directory.GetFiles(output+"/Deductions","*.asset"))
                if(!rows.ContainsKey(Path.GetFileNameWithoutExtension(path)))AssetDatabase.DeleteAsset(path);
        }
        var deductions=serialized.FindProperty("deductions");deductions.arraySize=groups.Count;
        for(int i=0;i<groups.Count;i++)deductions.GetArrayElementAtIndex(i).objectReferenceValue=groups[i];
        ImportRows(serialized,"worldInteractions",input+"/WorldInteractionTable.csv",(row,table,entry)=>{
            Set(entry,"worldPath",table.Get(row,"WorldPath"));Set(entry,"searchNodeId",table.Get(row,"SearchNodeID"));
            Set(entry,"clueId",table.Get(row,"ClueID"));Set(entry,"dialogueId",table.Get(row,"DialogueID"));Set(entry,"requiredCharacter",table.Get(row,"RequiredCharacter"));
        });
        ImportRows(serialized,"zones",input+"/NavigationTable.csv",(row,table,entry)=>{
            string searchNodeId=table.Get(row,"SearchNodeID");
            string backgroundName=table.Get(row,"Background");
            Set(entry,"id",backgroundName);Set(entry,"background",backgroundName);
            Set(entry,"left",string.Empty);Set(entry,"right",string.Empty);Set(entry,"searchNodeId",searchNodeId);
            entry.FindPropertyRelative("focus").floatValue=.5f;
        });
        serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(database);
    }
    private static string[] Split(string value)=>value.Split('|',StringSplitOptions.RemoveEmptyEntries);
    private static void Set(SerializedProperty entry,string name,string value)=>entry.FindPropertyRelative(name).stringValue=value;
    private static void ImportRows(SerializedObject database,string field,string path,Action<int,CsvTable,SerializedProperty> write)
    {
        var target=database.FindProperty(field);
        if(!File.Exists(path)){target.arraySize=0;return;}
        var table=CsvParser.Parse(File.ReadAllText(path));target.arraySize=table.RowCount;
        for(int row=0;row<table.RowCount;row++)write(row,table,target.GetArrayElementAtIndex(row));
    }
}
#endif
