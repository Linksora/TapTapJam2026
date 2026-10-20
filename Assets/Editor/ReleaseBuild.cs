#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ReleaseBuild
{
    [MenuItem("TapJam/Build Windows Release")]
    public static void BuildWindows()
    {
        LevelDataImporter.ImportAll();
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path).ToArray();
        if (scenes.Length == 0 || scenes[0] != "Assets/Scenes/TitleScene.unity")
            throw new InvalidOperationException("构建场景必须从 TitleScene 开始。");
        foreach (string scene in scenes)
            if (!File.Exists(scene)) throw new FileNotFoundException("构建场景不存在。", scene);
        if (File.Exists("Assets/Scripts/Systems/StoryManager.CGPreview.cs"))
            throw new InvalidOperationException("玩家版本不能包含临时 CG 跳转按钮。");

        string folder = GetArgument("-playerBuildOutput") ?? "Builds/Windows-Player-20261021";
        Directory.CreateDirectory(folder);
        string output = Path.GetFullPath(folder + "/TapJam.exe");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        string summary = $"Result: {report.summary.result}\nOutput: {output}\n"
            + $"Size: {report.summary.totalSize} bytes\nDuration: {report.summary.totalTime}\n"
            + $"Errors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\n"
            + "Scenes:\n" + string.Join("\n", scenes);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(GetArgument("-playerBuildSummary") ?? "Logs/WindowsPlayerBuildSummary.txt", summary);
        Debug.Log(summary);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows 玩家版构建失败，请查看 Logs/WindowsPlayerBuild.log。");
    }

    private static string GetArgument(string key)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, key);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}
#endif
