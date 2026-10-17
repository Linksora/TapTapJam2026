using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>自动创建、跨场景保留的二维音频播放器。资源入口：Resources/GameAudioSettings。</summary>
public sealed class GameAudio : MonoBehaviour
{
    private const string VolumeKey = "Audio.MasterLevel";
    private static GameAudio instance;
    private GameAudioSettings settings;
    private AudioSource music;
    private AudioSource effects;
    private readonly List<AudioSource> ambience = new List<AudioSource>();
    private AudioListener fallbackListener;
    private string currentBackground;
    private float nextButtonScan;
    private readonly HashSet<GameMusic> missingMusic = new HashSet<GameMusic>();

    public GameMusic CurrentMusic { get; private set; } = GameMusic.Daily;

    public static GameAudio Instance
    {
        get
        {
            if (instance == null) new GameObject("GameAudio").AddComponent<GameAudio>();
            return instance;
        }
    }

    public static int MasterVolumeLevel => Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, 5), 0, 5);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() => _ = Instance;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        settings = Resources.Load<GameAudioSettings>("GameAudioSettings");
        music = CreateSource(true);
        effects = CreateSource(false);
        music.clip = settings != null ? settings.backgroundMusic : Resources.Load<AudioClip>("Music/夢");
        music.volume = settings != null ? settings.musicVolume : 0.4f;
        AudioListener.volume = MasterVolumeLevel / 5f;
        if (music.clip != null) music.Play();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0;
        return source;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) StopAmbience();
        if (scene.name == "TitleScene") PlayMusic(GameMusic.Daily);
        EnsureListener();
        BindButtons();
    }

    private void EnsureListener()
    {
        bool otherListener = false;
        foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener != fallbackListener && listener.isActiveAndEnabled) otherListener = true;
        if (!otherListener && fallbackListener == null)
            fallbackListener = gameObject.AddComponent<AudioListener>();
        if (fallbackListener != null) fallbackListener.enabled = !otherListener;
    }

    private void Update()
    {
        // 选项等按钮会在剧情中动态创建，也要接上点击音效。
        if (Time.unscaledTime < nextButtonScan) return;
        nextButtonScan = Time.unscaledTime + 0.25f;
        EnsureListener();
        BindButtons();
    }

    private static void BindButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!button.TryGetComponent<GameAudioButton>(out _)) button.gameObject.AddComponent<GameAudioButton>();
    }

    public void SetMasterVolumeLevel(int level)
    {
        level = Mathf.Clamp(level, 0, 5);
        AudioListener.volume = level / 5f;
        PlayerPrefs.SetInt(VolumeKey, level);
        PlayerPrefs.Save();
    }

    public void Play(GameSound id)
    {
        GameAudioSettings.Sound sound = settings != null ? settings.Find(id) : null;
        if (sound?.clip != null) effects.PlayOneShot(sound.clip, sound.volume);
    }

    // 支持 Inspector UnityEvent 传字符串，例如 DoorOpen、DoorLock。
    public void PlayEffect(string soundId)
    {
        if (System.Enum.TryParse(soundId, out GameSound id)) Play(id);
    }

    public void EnterStoryNode(string levelId, string nodeId)
    {
        if (settings == null) return;
        foreach (GameAudioSettings.StoryMusic cue in settings.musicCues)
            if (cue != null && cue.levelId == levelId && cue.nodeId == nodeId) PlayMusic(cue.music);
        foreach (GameAudioSettings.StorySound cue in settings.storyCues)
            if (cue != null && cue.levelId == levelId && cue.nodeId == nodeId) Play(cue.sound);
    }

    public void PlayMusic(GameMusic id)
    {
        AudioClip clip = settings != null ? settings.FindMusic(id)
            : id == GameMusic.Daily ? Resources.Load<AudioClip>("Music/夢") : null;
        if (clip == null)
        {
            if (missingMusic.Add(id))
                Debug.LogWarning($"[GameAudio] 尚未配置 {id} BGM，继续播放当前音乐。");
            return;
        }

        missingMusic.Remove(id);
        CurrentMusic = id;
        // 重复进入节点或多个类型引用同一曲目时，不从头重播。
        if (music.clip == clip && music.isPlaying) return;
        music.clip = clip;
        music.Play();
    }

    // UnityEvent 可填 Daily、DayInvestigation 或 NightInvestigation。
    public void SwitchMusic(string musicId)
    {
        if (System.Enum.TryParse(musicId, out GameMusic id) && System.Enum.IsDefined(typeof(GameMusic), id))
            PlayMusic(id);
    }

    public void SetBackground(string background)
    {
        if (currentBackground == background) return;
        StopAmbience();
        currentBackground = background;
        if (settings == null) return;
        foreach (GameAudioSettings.BackgroundSound cue in settings.backgrounds)
        {
            if (cue == null || cue.background != background) continue;
            GameAudioSettings.Sound sound = settings.Find(cue.sound);
            if (sound?.clip == null) continue;
            AudioSource source = CreateSource(true);
            source.clip = sound.clip;
            source.volume = sound.volume;
            source.Play();
            ambience.Add(source);
        }
    }

    public void StopAmbience()
    {
        foreach (AudioSource source in ambience)
        {
            source.Stop();
            Destroy(source);
        }
        ambience.Clear();
        currentBackground = null;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }
}
