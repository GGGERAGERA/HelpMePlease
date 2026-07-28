public readonly struct LevelMechanicPresentationData
{
    public string Title { get; }
    public string Description { get; }
    public string PinnedDescription { get; }

    public LevelMechanicPresentationData(
        string title,
        string description,
        string pinnedDescription)
    {
        Title = title;
        Description = description;
        PinnedDescription = pinnedDescription;
    }
}
