using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 设置面板草稿：全屏半透明遮罩 + 居中面板。
/// 继续游戏=关闭面板；退出游戏=关闭程序；返回主界面=LoadScene 标题页；
/// 音量 1~5 五个按钮，点击第 n 个把全局音量(AudioListener)设为 n/5，并切换音量条状态图。
/// 所有场景内对象按约定名字在运行时查找，不做 Inspector 拖线，方便复制到其他场景。
/// 约定物体名：SettingsButton（齿轮入口）、ContinueButton、QuitButton、TitleButton、
/// VolumeButton1~5、VolumeBarImage。
/// </summary>
public sealed class SettingsPanel : MonoBehaviour
{
    [Tooltip("标题页场景名，在这里填实际的场景名（Build Settings 里也要加入）")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Tooltip("音量条 6 个状态图：0=静音 … 5=满音量")]
    [SerializeField] private Sprite[] volumeSprites = new Sprite[6];

    private GameObject panelRoot;
    private Image volumeBarImage;

    private void Awake()
    {
        panelRoot = transform.Find("Panel")?.gameObject;
        volumeBarImage = FindRecursive(transform, "VolumeBarImage")?.GetComponent<Image>();

        Bind("ContinueButton", Close);
        Bind("QuitButton", QuitGame);
        Bind("TitleButton", ReturnToTitle);

        for (int i = 1; i <= 5; i++)
        {
            int level = i; // 闭包捕获
            Bind($"VolumeButton{i}", () => SetVolumeLevel(level));
        }

        // 齿轮入口在游戏主 Canvas 上（不在本面板内），按名字查找。
        Button settingsButton = FindFirstButton("SettingsButton");
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(Open);
        }
        else
        {
            Debug.LogWarning("SettingsPanel：场景中找不到 SettingsButton（齿轮入口），面板将无法打开。");
        }

        SetVolumeLevel(GameAudio.MasterVolumeLevel);

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void Open()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    public void Close()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void ReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            Debug.LogWarning("SettingsPanel：titleSceneName 未填写，无法返回主界面。");
            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetVolumeLevel(int level)
    {
        level = Mathf.Clamp(level, 0, 5);
        GameAudio.Instance.SetMasterVolumeLevel(level);

        if (volumeBarImage != null && volumeSprites != null && level < volumeSprites.Length && volumeSprites[level] != null)
        {
            volumeBarImage.sprite = volumeSprites[level];
        }
    }

    private void Bind(string buttonName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindRecursive(transform, buttonName)?.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
        else
        {
            Debug.LogWarning($"SettingsPanel：找不到按钮 {buttonName}");
        }
    }

    private Button FindFirstButton(string name)
    {
        return FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == name);
    }

    private static Transform FindRecursive(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindRecursive(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
