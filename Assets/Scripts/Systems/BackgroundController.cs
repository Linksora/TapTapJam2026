using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>每章配置背景分组，切换时连同其子物体和相机边界一起切换。未配置分组的旧场景仍支持换图。</summary>
public sealed class BackgroundController : MonoBehaviour
{
    [Serializable]
    private sealed class BackgroundEntry
    {
        public string id;
        [Tooltip("兼容剧本中已有的背景名，例如“便利店草图”。")]
        public string[] aliases = Array.Empty<string>();
        [Tooltip("整个背景分组，包含背景图片、线索和交互物品。")]
        public GameObject root;
        [Tooltip("只指定背景底图，用于计算相机边界，不要指定线索图片。")]
        public SpriteRenderer boundsRenderer;
        [Tooltip("音频表使用的背景名；留空时使用 Id。")]
        public string audioBackground;
    }

    [SerializeField] private BackgroundEntry[] backgrounds = Array.Empty<BackgroundEntry>();
    [SerializeField] private string initialBackground;

    private Image backgroundImage;
    private SpriteRenderer backgroundRenderer;
    private Transform charactersRoot;
    private BackgroundEntry activeEntry;
    private string currentBackground;

    public string CurrentBackground => currentBackground;
    public Transform CurrentRoot => activeEntry?.root != null ? activeEntry.root.transform : null;
    public SpriteRenderer CurrentRenderer => activeEntry != null ? activeEntry.boundsRenderer : backgroundRenderer;

    private void Awake()
    {
        if (backgrounds.Length == 0) CollectBackgrounds();
        if (backgrounds.Length == 0) { ResolveLegacy(); return; }
        if (!string.IsNullOrWhiteSpace(initialBackground)) Show(initialBackground);
        else
        {
            // 未指定初始背景时，沿用场景中第一个已激活的分组。
            BackgroundEntry initial = Array.Find(backgrounds, entry => entry?.root != null && entry.root.activeSelf);
            if (initial != null) Show(initial.id);
        }
    }

    private void Reset() => CollectBackgrounds();

    [ContextMenu("从 world 收集背景分组")]
    public void CollectBackgrounds()
    {
        Transform world = FindSceneTransform("world");
        if (world == null) return;
        var entries = new List<BackgroundEntry>();
        GameAudioSettings audio = Resources.Load<GameAudioSettings>("GameAudioSettings");
        for (int i = 0; i < world.childCount; i++)
        {
            Transform group = world.GetChild(i);
            SpriteRenderer renderer = group.Find("背景")?.GetComponent<SpriteRenderer>();
            if (renderer == null) continue;
            string legacyId = group.name + "草图";
            bool legacyAudio = audio != null && Array.Exists(audio.backgrounds, cue => cue != null && cue.background == legacyId);
            entries.Add(new BackgroundEntry
            {
                id = group.name,
                aliases = new[] { legacyId },
                root = group.gameObject,
                boundsRenderer = renderer,
                audioBackground = legacyAudio ? legacyId : group.name
            });
        }
        if (entries.Count == 0) return;
        backgrounds = entries.ToArray();
        if (string.IsNullOrWhiteSpace(initialBackground))
        {
            BackgroundEntry initial = entries.Find(entry => entry.root.activeSelf)
                ?? entries.Find(entry => entry.id == "事务所") ?? entries[0];
            initialBackground = initial.id;
        }
    }

    /// <summary>Inspector UnityEvent 和 StoryManager 均可传入背景 Id 或别名。</summary>
    public void Show(string backgroundId)
    {
        if (string.IsNullOrWhiteSpace(backgroundId)) return;
        if (backgrounds.Length > 0)
        {
            BackgroundEntry target = Array.Find(backgrounds, entry => entry != null &&
                (entry.id == backgroundId || Array.Exists(entry.aliases ?? Array.Empty<string>(), alias => alias == backgroundId)));
            if (target?.root == null)
            {
                Debug.LogWarning($"[BackgroundController] 未配置背景分组“{backgroundId}”，保留当前背景。", this);
                return;
            }
            if (target.boundsRenderer == null)
            {
                Transform image = target.root.transform.Find("背景");
                if (image != null) target.boundsRenderer = image.GetComponent<SpriteRenderer>();
            }

            bool changed = activeEntry != target;
            foreach (BackgroundEntry entry in backgrounds)
                if (entry?.root != null) entry.root.SetActive(entry.root == target.root);
            activeEntry = target;
            currentBackground = backgroundId;
            if (changed) UpdateCamera(target.boundsRenderer, true);
            GameAudio.Instance.SetBackground(string.IsNullOrWhiteSpace(target.audioBackground) ? target.id : target.audioBackground);
            return;
        }

        ShowLegacy(backgroundId);
    }

    private void UpdateCamera(SpriteRenderer renderer, bool center)
    {
        foreach (ScenePanner panner in FindObjectsByType<ScenePanner>(FindObjectsInactive.Include))
            if (panner.gameObject.scene == gameObject.scene) panner.SetBackground(renderer, center);
    }

    private void ShowLegacy(string backgroundArtName)
    {
        if (backgroundArtName == currentBackground) return;
        ResolveLegacy();
        Sprite sprite = Resources.Load<Sprite>($"Art/场景/{backgroundArtName}");
        if (sprite == null || (backgroundImage == null && backgroundRenderer == null))
        {
            Debug.LogWarning($"[BackgroundController] 找不到背景对象或贴图 Resources/Art/场景/{backgroundArtName}", this);
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
            UpdateCamera(backgroundRenderer, false);
        }
        if (charactersRoot != null)
        {
            for (int i = 0; i < charactersRoot.childCount; i++)
            {
                Transform group = charactersRoot.GetChild(i);
                group.gameObject.SetActive(group.name == "BG_" + backgroundArtName);
            }
        }
        currentBackground = backgroundArtName;
        GameAudio.Instance.SetBackground(backgroundArtName);
    }

    private void ResolveLegacy()
    {
        if (backgroundImage == null && backgroundRenderer == null)
        {
            Transform backgroundObject = FindSceneTransform("背景");
            if (backgroundObject != null)
            {
                backgroundRenderer = backgroundObject.GetComponentInChildren<SpriteRenderer>(true);
                backgroundImage = backgroundObject.GetComponentInChildren<Image>(true);
            }
        }
        if (charactersRoot == null) charactersRoot = FindSceneTransform("Characters");
    }

    private Transform FindSceneTransform(string objectName)
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (candidate.gameObject.scene == gameObject.scene && candidate.name == objectName) return candidate;
        return null;
    }
}
