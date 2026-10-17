using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>项目文字统一使用 Resources/PANDAPAN，缺失的中文使用系统后备字形。</summary>
public static class GameFont
{
    private static Font primary;
    private static TMP_FontAsset tmp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        primary = null;
        tmp = null;
    }

    public static Font Primary => primary != null ? primary : primary = Resources.Load<Font>("PANDAPAN");

    public static TMP_FontAsset TextMeshProFont
    {
        get
        {
            if (tmp != null) return tmp;
            tmp = Resources.Load<TMP_FontAsset>("PANDAPAN SDF");
            if (tmp == null && Primary != null) tmp = TMP_FontAsset.CreateFontAsset(Primary);
            if (tmp == null) return null;
            tmp.fallbackFontAssetTable.RemoveAll(asset => asset == null);

            // TMP 的字形查找独立于旧版 Text，显式补上中文后备字体。
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string family in new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial Unicode MS" })
            {
                if (System.Array.IndexOf(installed, family) < 0) continue;
                if (tmp.fallbackFontAssetTable.Exists(asset => asset.faceInfo.familyName == family)) break;
                TMP_FontAsset fallback = TMP_FontAsset.CreateFontAsset(family, "Regular");
                if (fallback == null) continue;
                tmp.fallbackFontAssetTable.Add(fallback);
                break;
            }
            return tmp;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++) ApplyScene(SceneManager.GetSceneAt(i));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyScene(scene);

    public static void ApplyScene(Scene scene)
    {
        if (Primary == null)
        {
            Debug.LogError("[GameFont] 找不到 Resources/PANDAPAN 字体。");
            return;
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true)) text.font = Primary;
            foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true))
            {
                text.font = Primary;
                Renderer renderer = text.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = Primary.material;
            }
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (TextMeshProFont != null) text.font = TextMeshProFont;
        }
    }
}
