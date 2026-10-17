# 章节背景

每章在 `LevelSystems` 上配置一个 `BackgroundController`，Backgrounds 列表包含本章全部背景。

- Id：背景名，例如「便利店」。剧本 CSV 的 Background 列填写这个名字。
- Aliases：旧剧本使用的名字，例如「便利店草图」，也能激活同一组。
- Root：完整背景分组，下面放背景、线索、装饰和碰撞体。
- Bounds Renderer：分组内的背景底图，用于计算相机移动范围。
- Audio Background：环境声配置使用的背景名；留空使用 Id。
- Initial Background：章节初始背景。

当前测试场景已配置「事务所、便利店、街道、学校侧院」，初始为事务所。切换只改变分组根对象是否激活，保留子物体各自的显隐状态。新的背景会让相机水平居中并重新计算边界；重复请求同一组不会重置相机。

也可以从按钮的 UnityEvent 调用 `BackgroundController.Show(string)`，或从代码调用 `Show("街道")`。背景切换本身不会跳转剧情。

新增章节时可以复制组件并重新拖引用，或按下面的结构摆放后，使用组件菜单「从 world 收集背景分组」自动填表。自动收集会重建列表，之后仍可编辑 Id、别名和音频名。

```
world
  事务所
    背景（SpriteRenderer）
  便利店
    背景（SpriteRenderer）
    促销牌
    线索物品
```

组件未配置列表时，运行时也会按该结构自动识别背景；完全没有背景分组的旧场景继续使用原来的换图方式。每个背景保持一张完整场景图，左右按钮按住时在当前图内平移；点击场景里配置了 `BackgroundSwitch` 的出口物件才切换到另一张背景。搜证只查找当前背景分组内、由 `WorldInteractionTable.csv` 配置的物件，不会额外生成临时调查点。
