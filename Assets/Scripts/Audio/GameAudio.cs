using System.Collections;
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
    private AudioSource click;
    private readonly List<AudioSource> ambience = new List<AudioSource>();
    private readonly List<AudioSource> transientEffects = new List<AudioSource>();
    private readonly Dictionary<GameSound, AudioSource> storyLoops = new Dictionary<GameSound, AudioSource>();
    private readonly Dictionary<AudioSource, Coroutine> volumeFades = new Dictionary<AudioSource, Coroutine>();
    private Coroutine clickStop;
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
        click = CreateSource(false);
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
        if (mode == LoadSceneMode.Single)
        {
            // Old scene cues must not keep playing over the next chapter.
            StopAllCoroutines();
            foreach (AudioSource source in GetComponents<AudioSource>())
                if (source != music && source != effects && source != click)
                {
                    source.Stop();
                    Destroy(source);
                }
            ambience.Clear();
            transientEffects.Clear();
            storyLoops.Clear();
            volumeFades.Clear();
            currentBackground = null;
            effects.Stop();
            click.Stop();
            clickStop = null;
        }
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
        if (sound?.clip == null) return;
        if (id == GameSound.Click)
        {
            // The supplied file contains several clicks; use its first pop for each button press.
            if (clickStop != null) StopCoroutine(clickStop);
            click.Stop();
            click.clip = sound.clip;
            click.volume = sound.volume;
            click.Play();
            clickStop = StartCoroutine(StopClickAfter(0.22f));
            return;
        }
        effects.PlayOneShot(sound.clip, sound.volume);
    }

    private IEnumerator StopClickAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        click.Stop();
        clickStop = null;
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
            if (cue != null && cue.levelId == levelId && cue.nodeId == nodeId)
            {
                switch (cue.action)
                {
                    case StorySoundAction.StartLoop: StartStoryLoop(cue.sound, cue.fadeSeconds); break;
                    case StorySoundAction.StopLoop: StopStoryLoop(cue.sound, cue.fadeSeconds); break;
                    default: PlayStoryOnce(cue.sound, cue.fadeSeconds); break;
                }
            }
    }

    private void PlayStoryOnce(GameSound id, float fadeSeconds)
    {
        GameAudioSettings.Sound sound = settings.Find(id);
        if (sound?.clip == null) return;
        AudioSource source = CreateSource(false);
        source.clip = sound.clip;
        source.volume = fadeSeconds > 0f ? 0f : sound.volume;
        source.Play();
        transientEffects.Add(source);
        StartCoroutine(FinishOneShot(source, sound.volume, fadeSeconds));
    }

    private IEnumerator FinishOneShot(AudioSource source, float volume, float fadeSeconds)
    {
        while (source != null && source.isPlaying)
        {
            if (fadeSeconds > 0f)
            {
                float envelope = Mathf.Min(source.time / fadeSeconds,
                    (source.clip.length - source.time) / fadeSeconds, 1f);
                source.volume = volume * Mathf.Clamp01(envelope);
            }
            yield return null;
        }
        transientEffects.Remove(source);
        if (source != null) Destroy(source);
    }

    private void StartStoryLoop(GameSound id, float fadeSeconds)
    {
        if (storyLoops.ContainsKey(id)) return;
        GameAudioSettings.Sound sound = settings.Find(id);
        if (sound?.clip == null) return;
        AudioSource source = CreateSource(true);
        source.clip = sound.clip;
        source.volume = fadeSeconds > 0f ? 0f : sound.volume;
        source.Play();
        storyLoops[id] = source;
        if (fadeSeconds > 0f) StartFade(source, sound.volume, fadeSeconds, false);
    }

    private void StopStoryLoop(GameSound id, float fadeSeconds)
    {
        if (!storyLoops.TryGetValue(id, out AudioSource source)) return;
        storyLoops.Remove(id);
        StartFade(source, 0f, fadeSeconds, true);
    }

    private void StartFade(AudioSource source, float target, float seconds, bool destroy)
    {
        if (source == null) return;
        if (volumeFades.TryGetValue(source, out Coroutine previous)) StopCoroutine(previous);
        volumeFades.Remove(source);
        if (seconds <= 0f)
        {
            source.volume = target;
            if (destroy) { source.Stop(); Destroy(source); }
            return;
        }
        volumeFades[source] = StartCoroutine(FadeVolume(source, target, seconds, destroy));
    }

    private IEnumerator FadeVolume(AudioSource source, float target, float seconds, bool destroy)
    {
        if (source == null) yield break;
        float initial = source.volume;
        float elapsed = 0f;
        while (source != null && elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(initial, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        if (source == null) yield break;
        source.volume = target;
        volumeFades.Remove(source);
        if (destroy) { source.Stop(); Destroy(source); }
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
        foreach (AudioSource source in ambience)
            StartFade(source, 0f, 0.6f, true);
        ambience.Clear();
        currentBackground = background;
        if (settings == null) return;
        foreach (GameAudioSettings.BackgroundSound cue in settings.backgrounds)
        {
            if (cue == null || cue.background != background) continue;
            GameAudioSettings.Sound sound = settings.Find(cue.sound);
            if (sound?.clip == null) continue;
            AudioSource source = CreateSource(true);
            source.clip = sound.clip;
            source.volume = 0f;
            source.Play();
            ambience.Add(source);
            StartFade(source, sound.volume, 0.6f, false);
        }
    }

    public void StopAmbience()
    {
        foreach (AudioSource source in ambience)
        {
            if (source == null) continue;
            if (volumeFades.TryGetValue(source, out Coroutine fade)) StopCoroutine(fade);
            volumeFades.Remove(source);
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
