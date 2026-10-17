using System.Linq;
using UnityEngine;

/// <summary>
/// 在搜证阶段限制可切换的背景。每个背景仍使用原来的完整场景图，
/// 左右按钮继续由 ScenePanner 用于在图内移动。
/// </summary>
public sealed class ExplorationNavigator : MonoBehaviour
{
    private LevelDatabase database;
    private BackgroundController background;
    private string phase;
    private bool active;

    public void Begin(LevelDatabase data, StoryManager manager, BackgroundController controller, string searchId, string start)
    {
        database = data;
        background = controller;
        phase = searchId;
        active = true;
    }

    public bool TryGo(string destination)
    {
        if (!active || background == null || string.IsNullOrWhiteSpace(destination)) return false;
        bool available = database != null && database.Zones.Any(zone =>
            zone.searchNodeId == phase && zone.background == destination);
        if (!available) return false;

        background.Show(destination);
        return true;
    }

    public void End()
    {
        active = false;
    }
}
