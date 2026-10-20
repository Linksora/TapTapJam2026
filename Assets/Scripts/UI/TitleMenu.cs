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
    [Header("标题精灵摆动")]
    [SerializeField] private RectTransform titleGraphic;
    [SerializeField, Range(0f, 15f)] private float titleSwingDegrees = 5f;
    [SerializeField, Min(0.1f)] private float titleSwingPeriod = 2.4f;
    private Quaternion titleRestRotation;
    private float titleTime;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (titleGraphic != null) titleRestRotation = titleGraphic.localRotation;
        Font chineseFont = GameFont.Primary;
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

    private void Update()
    {
        if (titleGraphic == null) return;
        titleTime += Time.unscaledDeltaTime;
        float angle = Mathf.Sin(titleTime * Mathf.PI * 2f / Mathf.Max(.1f, titleSwingPeriod)) * titleSwingDegrees;
        titleGraphic.localRotation = titleRestRotation * Quaternion.Euler(0, 0, angle);
    }

    private void OnDisable()
    {
        if (titleGraphic != null) titleGraphic.localRotation = titleRestRotation;
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
