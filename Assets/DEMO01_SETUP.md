# Demo01 使用说明

打开 `Assets/Scenes/Demo01.unity` 后进入 Play Mode。

## 验收操作

1. 点击左侧毛线团、右侧苹果，它们会隐藏并进入底部物品栏。
2. 从 Slot 中拖动毛线团到中间的 Player，显示“小毛：我的毛线团！”。
3. 拖动苹果到 Player，显示“小毛：我现在不想吃苹果。”。
4. 拖到空白区域不会消耗物品；当前所有反应都不会消耗物品。

## 射线与组件配置

- `Main Camera`：`Camera` + `Physics2DRaycaster`。
- `Player`：`BoxCollider2D` + `ItemReceiver` + `PlayerReaction`。
- `Yarn` / `Apple`：`CircleCollider2D` + `WorldItem`。
- `Canvas`：`GraphicRaycaster`。
- `EventSystem`：`InputSystemUIInputModule`，使用新版 Input System 默认 UI Actions。
- Slot 背景和非空 Item Icon 的 `Raycast Target` 开启，以接收 UI 拖拽事件。
- `DialoguePanel`、`DialogueText` 的 `Raycast Target` 关闭，避免遮挡世界目标。
- `DragLayer` 覆盖整个 Canvas 且不阻挡射线；临时 Drag Icon 的 `raycastTarget` 与
  `CanvasGroup.blocksRaycasts` 均关闭，因此松手时可以检测图标下方的 Player。

## 重新生成

如需恢复或重新装配场景，使用 Unity 菜单 `TapJam > Rebuild Demo01 Scene`。
该操作会重建 `Demo01.unity`，不会修改其他已打开场景。
