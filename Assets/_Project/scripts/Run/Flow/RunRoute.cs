public static class RunRoute
{
    public const int FirstSector = 1;
    public const int TotalSectors = 3;
    public const int ExplorationSectorCount = TotalSectors;
    public const int FinalSector = TotalSectors;

    public static bool HasNextSector(int sectorNumber)
    {
        return IsExplorationSector(sectorNumber) && sectorNumber < TotalSectors;
    }

    public static bool IsExplorationSector(int sectorNumber)
    {
        return sectorNumber >= FirstSector &&
            sectorNumber <= ExplorationSectorCount;
    }

    public static bool IsFinalSector(int sectorNumber)
    {
        return sectorNumber == FinalSector;
    }
}
