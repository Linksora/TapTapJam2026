using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景背景切换：替换 Canvas/背景 上的 Image 贴图（资源在 Resources/Art/场景），
/// 同时按名字显隐 Characters 容器下的 BG_背景名 分组，让 NPC 立绘随场景收放。
/// 全部按约定名字查找，无需 Inspector 拖线。
/// </summary>
public sealed class BackgroundController : MonoBehaviour
{
    private const string BackgroundObjectName = "背景";
    private const string CharactersRootName = "Characters";
    private const string GroupPrefix = "BG_";

    private Image backgroundImage;
    private SpriteRenderer backgroundRenderer;
    private Transform charactersRoot;
    private string currentBackground;

    public string CurrentBackground => currentBackground;

    private void Awake()
    {
        Resolve();
    }

    public void Show(string backgroundArtName)
    {
        if (string.IsNullOrEmpty(backgroundArtName) || backgroundArtName == currentBackground)
        {
            return;
        }

        Resolve();

        Sprite sprite = Resources.Load<Sprite>($"Art/场景/{backgroundArtName}");
        if (sprite == null)
        {
            Debug.LogWarning($"[BackgroundController] 找不到背景贴图 Resources/Art/场景/{backgroundArtName}");
            return;
        }

        if (backgroundImage != null)
        {
            backgroundImage.sprite = sprite;
            backgroundImage.enabled = true;
        }

        if (backgroundRenderer != null)
        {
            backgroundRenderer.sprite = sprite;
        }

        if (charactersRoot != null)
        {
            string activeGroup = GroupPrefix + backgroundArtName;
            for (int i = 0; i < charactersRoot.childCount; i++)
            {
                Transform group = charactersRoot.GetChild(i);
                group.gameObject.SetActive(group.name == activeGroup);
            }
        }

        currentBackground = backgroundArtName;
    }

    private void Resolve()
    {
        if (backgroundImage == null && backgroundRenderer == null)
        {
            GameObject backgroundObject = FindByNameInActiveScene(BackgroundObjectName);
            if (backgroundObject != null)
            {
                // 项目的背景是世界空间 SpriteRenderer；同时兼容以后换成 UI Image。
                backgroundRenderer = backgroundObject.GetComponent<SpriteRenderer>();
                backgroundImage = backgroundObject.GetComponent<Image>();
                if (backgroundRenderer == null && backgroundImage == null)
                {
                    backgroundImage = backgroundObject.GetComponentInChildren<Image>(true);
                    backgroundRenderer = backgroundObject.GetComponentInChildren<SpriteRenderer>(true);
                }
            }
        }

        if (charactersRoot == null)
        {
            charactersRoot = FindByNameInActiveScene(CharactersRootName)?.transform;
        }
    }

    private static GameObject FindByNameInActiveScene(string objectName)
    {
        foreach (GameObject rootObject in UnityEngine.SceneManagement.SceneManager
                     .GetActiveScene().GetRootGameObjects())
        {
            GameObject found = FindRecursive(rootObject.transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindRecursive(Transform current, string objectName)
    {
        if (current.name == objectName)
        {
            return current.gameObject;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            GameObject found = FindRecursive(current.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
