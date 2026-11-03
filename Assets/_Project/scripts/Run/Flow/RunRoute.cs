public static class RunRoute
{
    public const int TutorialSector = 0;
    public const int FirstSector = 1;
    public const int LevelCount = 5;
    public const int SectorsPerLevel = 3;
    public const int TotalSectors = LevelCount * SectorsPerLevel;
    public const int ExplorationSectorCount = TotalSectors;
    public const int FinalSector = TotalSectors;
    public const float SectorDuration = 60f;

    public static int LevelOf(int sector) => sector == TutorialSector ? 0 : (sector - 1) / SectorsPerLevel + 1;
    public static int ColumnOf(int sector) => (sector - 1) % SectorsPerLevel;
    public static bool CompletesLevel(int sector) => IsExplorationSector(sector) && ColumnOf(sector) == SectorsPerLevel - 1;
    public static bool HasNextSector(int sector) => sector >= TutorialSector && sector < FinalSector;
    public static bool IsExplorationSector(int sector) => sector >= FirstSector && sector <= FinalSector;
    public static bool IsFinalSector(int sector) => sector == FinalSector;
}
