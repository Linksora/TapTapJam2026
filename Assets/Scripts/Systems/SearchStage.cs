using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>将当前搜证节点配置映射到场景里的可调查物品。</summary>
public sealed class SearchStage : MonoBehaviour
{
    public void BeginSearch(DialogueData dialogue, LevelDatabase database, InventoryManager inventory)
    {
        if (inventory == null) { Debug.LogError("[SearchStage] 缺少 InventoryManager。", this); return; }
        Camera camera = Camera.main;
        if (camera != null && camera.GetComponent<Physics2DRaycaster>() == null) camera.gameObject.AddComponent<Physics2DRaycaster>();
        foreach (var rule in database.WorldInteractions.Where(rule => rule.searchNodeId == dialogue.DialogueId))
        {
            Transform target = FindWorld(rule.worldPath);
            if (target == null) { Debug.LogError($"[SearchStage] 找不到调查物 {rule.worldPath}"); continue; }
            if (target.GetComponent<WorldItem>() == null) target.gameObject.AddComponent<WorldItem>();
        }
        foreach (string id in dialogue.SearchClueIds)
            if (!database.WorldInteractions.Any(rule => rule.searchNodeId == dialogue.DialogueId && rule.clueId == id)
                && !inventory.Items.Any(item => item.ItemId == id))
                Debug.LogError($"[SearchStage] 必需证据 {id} 尚未配置调查入口。", this);
    }

    private Transform FindWorld(string path)
    {
        foreach (Transform root in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (root.gameObject.scene == gameObject.scene && root.name == "world") return root.Find(path);
        return null;
    }

}
