#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SoraPhaseBuilder
{
    [MenuItem("TapJam/Build Sora Phase 1 Features")]
    public static void BuildAll()
    {
        SampleSceneDemoInstaller.Install();
        TitleSceneBuilder.Build();
        Debug.Log("Sora 功能构建完成：线索详情面板 + TitleScene。");
    }
}
#endif
