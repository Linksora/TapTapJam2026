using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "TapJam/Item Data")]
public sealed class ItemData : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string itemName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea] private string description;
    [SerializeField] private ItemData nextClue;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public Sprite Icon => icon;
    public string Description => description;
    public ItemData NextClue => nextClue;
}
