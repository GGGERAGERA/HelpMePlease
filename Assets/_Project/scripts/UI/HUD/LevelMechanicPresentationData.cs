public readonly struct LevelMechanicPresentationData
{
    private readonly string titleKey;
    public string Title => LocalizationService.EnsureExists().Get(titleKey);
    private readonly string descriptionKey;
    public string Description => LocalizationService.EnsureExists().Get(descriptionKey);
    private readonly string pinnedDescriptionKey;
    public string PinnedDescription => LocalizationService.EnsureExists().Get(pinnedDescriptionKey);

    public LevelMechanicPresentationData(
        string title,
        string description,
        string pinnedDescription)
    {
        titleKey = title;
        descriptionKey = description;
        pinnedDescriptionKey = pinnedDescription;
    }
}
