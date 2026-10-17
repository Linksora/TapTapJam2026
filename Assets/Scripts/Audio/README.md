# 音频配置

选中 `Assets/Resources/GameAudioSettings.asset`，在 Inspector 修改配置。

- **日常 BGM／白天侦查 BGM／夜晚侦查 BGM**：三个独立资源槽。日常使用现有的 `Resources/Music/夢.mp3`；其余两首等待素材。启动播放日常音乐，所有曲目循环播放、跨场景持续。Music Volume 控制音乐与其他声音的相对音量。
- **Music Cues**：按 Level Id + Node Id 指定切换到日常、白天侦查或夜晚侦查；进入节点后持续播放，直到下一次切换。具体切换位置尚未指定，因此此表暂时为空。未配置的音乐保留当前曲目，并提示一次；重复切换同一曲目不从头重播。返回 `TitleScene` 自动恢复日常音乐。
- **Sounds**：已按音效清单建好 12 个槽位，Note 标注用途。把音效拖入 Clip，Volume 调整单个音效音量。空槽位保持静音。
- **Backgrounds**：Background 填剧本实际背景名（不含扩展名），Sound 选要循环播放的环境声。同一背景可配置多条；切换背景时停止上一组环境声。已配置第一章事务所的鸟鸣／街道声，以及便利店的嘈杂声／广播，等待素材。
- **Story Cues**：Level Id 填关卡（例如 `Level_01`），Node Id 填实际对话／问题 ID，Sound 选单次音效。每次进入节点播放一次，回到该节点会再次播放。开门、敲门、刮擦、脚步、锁门可在这里指定准确时机，当前没有凭空绑定剧情。

按钮的鼠标左键点击和键盘确认自动使用 Click；悬停、右键、禁用按钮不播放。音效不打断 BGM，多个单次音效可以重叠。

代码也可调用 `GameAudio.Instance.Play(GameSound.DoorOpen)`，或从 UnityEvent 调用 `PlayEffect(string)` 并填 `DoorOpen`。以后改成背景对象分组切换时，在切换成功处调用 `GameAudio.Instance.SetBackground(背景名)` 即可沿用环境声配置。

音乐可调用 `GameAudio.Instance.PlayMusic(GameMusic.DayInvestigation)`，或从 UnityEvent 调用 `SwitchMusic(string)` 并填 `Daily`、`DayInvestigation`、`NightInvestigation`。例如在 Music Cues 配置 `Level_01 / D_302 / 白天侦查`，进入该搜证节点时就切换；再为恢复日常的节点配置一条日常规则。

设置面板控制全局音量，音量选择保存到 PlayerPrefs，重新进入场景或重启游戏后保留。

## 待补素材

| 章节 | 音效 |
| --- | --- |
| 通用 | 可爱风格的鼠标点击声 |
| 第一章 | 清脆的鸟鸣、微弱的街道声、开门声、嘈杂声、便利店自动欢迎语音 |
| 第二章 | 急促敲门声、轻微刮擦、低低的风呼声与沙沙声、渐行渐近的大型动物踩木地板声 |
| 第三章 | 墙钟摆动声、锁门声 |

持续的风沙声、墙钟声通过 Backgrounds 循环；逐渐靠近的脚步建议使用本身带有渐近变化的音效片段，通过 Story Cues 播放。
