using UnityEngine;

[CreateAssetMenu(menuName = "Bunker/Shop Item")]
public sealed class BunkerShopItemData : ScriptableObject
{
    public string Id;
    public string Title;
    [TextArea] public string Description;
    public Sprite Icon;
    public int Price;
}