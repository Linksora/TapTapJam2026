using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "TapJam/Item Data")]
public sealed class ItemData : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string itemName;
    [SerializeField] private Sprite icon;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public Sprite Icon => icon;
}
