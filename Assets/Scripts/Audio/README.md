# 音频配置

`Assets/Resources/GameAudioSettings.asset` 统一配置三个章节的音效。`Sounds` 关联 `Assets/Resources/Music/音效` 中的素材并设置音量；`Backgrounds` 根据背景名循环播放环境声；`Story Cues` 根据 Level Id 和 Node Id 在进入剧情节点时触发音效，不需要在剧本 CSV 中新增字段。

第一章在事务所、学校侧院、街道播放鸟鸣和街道环境声，在便利店播放人声环境声；事务所请进后播放开门声，小狗进店时播放便利店提示音。第二章的窗边和录音重放节点播放刮擦、风声、脚步声。第三章在守宫家播放钟摆声，在妹妹离家时播放锁门声。第一章事务所、第二章开场和第三章结尾的敲门节点已配置好触发点；原始 `door_knocking.m4a` 被 Unity 当成普通文件，且系统音频解码器无法解码，因此 UrgentKnock 槽位暂时留空，需换一份可正常解码的 WAV/MP3 素材。切换背景时旧环境声会淡出；剧情循环声在对应的 StopLoop 节点淡出，切换章节时所有旧场景音效停止。

`Story Cues` 的 `Action` 可选 `OneShot`、`StartLoop`、`StopLoop`。一次性音效可通过 `Fade Seconds` 设置开头和结尾的淡入淡出；循环音效用对应的开始、停止规则控制音量变化。切换背景时环境声交叉淡化约 0.6 秒。所有淡化使用非缩放时间，暂停或对话速度变化不会卡住过渡。

按钮的鼠标左键点击和键盘确认自动使用 Click。现有 `clicking_pop.wav` 包含多次点击，播放器每次只截取开头的一次。悬停、右键、禁用按钮不播放。代码可调用 `GameAudio.Instance.Play(GameSound.DoorOpen)`，或从 UnityEvent 调用 `PlayEffect(string)` 并填 `DoorOpen`。

`Music Cues` 可按 Level Id 和 Node Id 切换 BGM。目前只配置日常 BGM；白天侦查和夜晚侦查的素材与切换点仍待确定。设置面板控制全局音量，音量选择保存在 PlayerPrefs。
