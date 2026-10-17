using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public sealed class ItemReceivedEvent : UnityEvent<ItemData> { }

public sealed class ItemReceiver : MonoBehaviour
{
    [SerializeField] private ItemReceivedEvent onItemReceived = new();

    public ItemReceivedEvent OnItemReceived => onItemReceived;

    public void ReceiveItem(ItemData item)
    {
        if (isActiveAndEnabled && item != null)
        {
            onItemReceived.Invoke(item);
        }
    }
}
