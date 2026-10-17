using System;
using UnityEngine;

public enum GameSound
{
    Click, Birds, Street, DoorOpen, Crowd, StoreAnnouncement,
    UrgentKnock, Scratching, WindAndSand, ApproachingFootsteps, Pendulum, DoorLock
}

public enum GameMusic
{
    [InspectorName("日常")] Daily,
    [InspectorName("白天侦查")] DayInvestigation,
    [InspectorName("夜晚侦查")] NightInvestigation
}

[CreateAssetMenu(menuName = "TapJam/Audio Settings")]
public sealed class GameAudioSettings : ScriptableObject
{
    [InspectorName("日常 BGM")] public AudioClip backgroundMusic;
    [InspectorName("白天侦查 BGM")] public AudioClip dayInvestigationMusic;
    [InspectorName("夜晚侦查 BGM")] public AudioClip nightInvestigationMusic;
    [Range(0, 1)] public float musicVolume = 0.4f;
    public Sound[] sounds = Array.Empty<Sound>();
    public BackgroundSound[] backgrounds = Array.Empty<BackgroundSound>();
    public StorySound[] storyCues = Array.Empty<StorySound>();
    public StoryMusic[] musicCues = Array.Empty<StoryMusic>();

    [Serializable]
    public sealed class StoryMusic
    {
        public string levelId = "Level_01";
        public string nodeId;
        public GameMusic music;
    }

    public AudioClip FindMusic(GameMusic id)
    {
        switch (id)
        {
            case GameMusic.Daily: return backgroundMusic;
            case GameMusic.DayInvestigation: return dayInvestigationMusic;
            case GameMusic.NightInvestigation: return nightInvestigationMusic;
            default: return null;
        }
    }

    [Serializable]
    public sealed class Sound
    {
        public GameSound id;
        public AudioClip clip;
        [Range(0, 1)] public float volume = 0.7f;
        [TextArea] public string note;
    }

    [Serializable]
    public sealed class BackgroundSound
    {
        [Tooltip("BackgroundController.Show 使用的背景名；同名规则可叠加多个环境声。")]
        public string background;
        public GameSound sound;
    }

    [Serializable]
    public sealed class StorySound
    {
        public string levelId = "Level_01";
        [Tooltip("进入这个对话或问题节点时播放一次。")]
        public string nodeId;
        public GameSound sound;
    }

    public Sound Find(GameSound id) => Array.Find(sounds, sound => sound != null && sound.id == id);
}
