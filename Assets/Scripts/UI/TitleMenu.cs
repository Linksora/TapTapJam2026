using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>标题页入口。场景名由 Inspector 配置，避免把流程写死在按钮上。</summary>
public sealed class TitleMenu : MonoBehaviour
{
    private const string LastSceneKey = "TapJam.LastGameplayScene";

    [SerializeField] private string firstGameplayScene = "Celia_TestScene";
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        Font chineseFont = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Microsoft YaHei", "SimHei", "Arial" }, 32);
        if (chineseFont != null)
        {
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = chineseFont;
            }
        }

        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        if (continueButton != null)
        {
            continueButton.interactable = PlayerPrefs.HasKey(LastSceneKey);
        }
    }

    public void StartGame()
    {
        LoadGameplayScene(firstGameplayScene);
    }

    public void ContinueGame()
    {
        LoadGameplayScene(PlayerPrefs.GetString(LastSceneKey, firstGameplayScene));
    }

    private void LoadGameplayScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"TitleMenu：场景 {sceneName} 未加入 Build Settings。被阻止加载。 ");
            return;
        }

        PlayerPrefs.SetString(LastSceneKey, sceneName);
        PlayerPrefs.Save();
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
