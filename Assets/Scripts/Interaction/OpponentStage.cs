using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对面对手舞台：管理世界空间里的 NPC 立绘（约定在名为 world 的根物体下、带 SpriteRenderer）。
/// 对话阶段：谁说话就显示谁，主角内心独白时全部隐藏；
/// 搜证阶段：全部隐藏，玩家在场景里自由调查；
/// 质询等待阶段：目标角色沉默在场，并临时开启碰撞体接收背包证物的拖拽。
/// 物体名必须等于剧本 Speaker（如“守宫妹妹”），无需 Inspector 拖线。
/// </summary>
public sealed class OpponentStage : MonoBehaviour
{
    private const string WorldRootName = "world";

    private static readonly string[] ExcludedNames = { "背景", "人物", "人物背景框" };

    private readonly Dictionary<string, GameObject> actors = new();
    private readonly Dictionary<GameObject, ItemReceiver> wiredReceivers = new();

    private GameObject activeActor;
    private BoxCollider2D activeCollider;
    private Action<ItemData> dropHandler;
    private bool dropArmed;
    private bool collected;

    public static OpponentStage Ensure()
    {
        var stage = FindFirstObjectByType<OpponentStage>(FindObjectsInactive.Include);
        if (stage == null)
        {
            stage = new GameObject("OpponentStage").AddComponent<OpponentStage>();
        }

        stage.CollectActors();
        return stage;
    }

    public bool HasActor(string speakerName)
    {
        return !string.IsNullOrEmpty(speakerName) && actors.ContainsKey(speakerName);
    }

    /// <summary>只显示指定角色，其余隐藏；找不到该角色时全部隐藏。</summary>
    public bool Show(string speakerName)
    {
        SetDropArmed(false, null);

        if (string.IsNullOrEmpty(speakerName) || !actors.TryGetValue(speakerName, out GameObject target))
        {
            HideAll();
            return false;
        }

        foreach (KeyValuePair<string, GameObject> pair in actors)
        {
            if (pair.Value != null)
            {
                pair.Value.SetActive(pair.Value == target);
            }
        }

        activeActor = target;
        return true;
    }

    public void HideAll()
    {
        SetDropArmed(false, null);

        foreach (GameObject actor in actors.Values)
        {
            if (actor != null)
            {
                actor.SetActive(false);
            }
        }

        activeActor = null;
    }

    /// <summary>
    /// 质询等待态开关：开启时给当前角色挂/启用贴合贴图的 BoxCollider2D，
    /// 背包证物拖到身上即触发 onDrop；关闭时碰撞体停用，正常对话期间不会误收拖拽。
    /// </summary>
    public void SetDropArmed(bool armed, Action<ItemData> onDrop)
    {
        dropArmed = armed;
        dropHandler = armed ? onDrop : null;

        if (activeActor == null)
        {
            return;
        }

        if (armed)
        {
            EnsureDropReceiver(activeActor);
            EnsureCollider(activeActor);
            if (activeCollider != null)
            {
                activeCollider.enabled = true;
            }
        }
        else if (activeCollider != null)
        {
            activeCollider.enabled = false;
        }
    }

    private void CollectActors()
    {
        if (collected)
        {
            return;
        }

        collected = true;
        actors.Clear();

        Transform worldRoot = FindRoot(WorldRootName);
        if (worldRoot == null)
        {
            return;
        }

        var renderers = worldRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            GameObject go = renderer.gameObject;
            if (Array.Exists(ExcludedNames, excluded => go.name == excluded))
            {
                continue;
            }

            // 同名以后出现的为准（正常场景里角色名唯一）。
            actors[go.name] = go;
            go.SetActive(false);
        }
    }

    private void EnsureDropReceiver(GameObject actor)
    {
        if (wiredReceivers.TryGetValue(actor, out ItemReceiver existing))
        {
            existing.enabled = true;
            return;
        }

        ItemReceiver receiver = actor.GetComponent<ItemReceiver>();
        if (receiver == null)
        {
            receiver = actor.AddComponent<ItemReceiver>();
        }

        receiver.enabled = true;
        receiver.OnItemReceived.AddListener(OnActorReceived);
        wiredReceivers[actor] = receiver;
    }

    private void OnActorReceived(ItemData item)
    {
        if (dropArmed)
        {
            dropHandler?.Invoke(item);
        }
    }

    private void EnsureCollider(GameObject actor)
    {
        if (activeCollider != null && activeCollider.gameObject == actor)
        {
            return;
        }

        activeCollider = actor.GetComponent<BoxCollider2D>();
        if (activeCollider == null)
        {
            activeCollider = actor.AddComponent<BoxCollider2D>();
        }

        var renderer = actor.GetComponent<SpriteRenderer>();
        Sprite sprite = renderer != null ? renderer.sprite : null;
        if (sprite != null)
        {
            activeCollider.size = sprite.bounds.size;
            activeCollider.offset = sprite.bounds.center;
        }

        activeCollider.isTrigger = true;
    }

    private static Transform FindRoot(string rootName)
    {
        foreach (GameObject rootObject in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObject.name == rootName)
            {
                return rootObject.transform;
            }

            Transform found = FindRecursive(rootObject.transform, rootName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindRecursive(Transform current, string targetName)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform deeper = FindRecursive(child, targetName);
            if (deeper != null)
            {
                return deeper;
            }
        }

        return null;
    }
}
