using UnityEngine;

[CreateAssetMenu(menuName = "Bunker/Content Data")]
public sealed class BunkerContentData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private BunkerContentCategory category;

    [Header("Shop")]
    [SerializeField] private string title;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private int price;

    public BunkerContentCategory Category => category;

    public string Id => id;
    public string Title => title;
    public string Description => description;
    public Sprite Icon => icon;
    public int Price => price;
}