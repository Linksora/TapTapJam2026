using UnityEngine;

public sealed class PlayerReaction : MonoBehaviour
{
    [SerializeField] private DialogueUI dialogueUI;

    public void ReactToItem(ItemData item)
    {
        if (item == null || dialogueUI == null)
        {
            return;
        }

        string message = item.ItemId switch
        {
            "yarn" => "小毛：我的毛线团！",
            "apple" => "小毛：我现在不想吃苹果。",
            _ => "小毛：这个给我干什么？"
        };

        dialogueUI.ShowDialogue(message);
    }

    public void ReactToSelectedItem(ItemData item)
    {
        if (item == null || dialogueUI == null)
        {
            return;
        }

        string message = item.ItemId switch
        {
            "apple" => "小毛：苹果？还可以…",
            "yarn" => "小毛：毛线团，喜欢！",
            _ => $"小毛：{item.ItemName}？"
        };

        dialogueUI.ShowDialogue(message);
    }
}
