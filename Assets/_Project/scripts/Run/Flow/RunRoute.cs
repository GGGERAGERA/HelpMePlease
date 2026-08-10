public static class RunRoute
{
    public const int FirstSector = 1;
    public const int ExplorationSectorCount = 4;
    public const int FinalBossSector = 5;
    public const int TotalSectors = FinalBossSector;

    public static bool IsExplorationSector(int sectorNumber)
    {
        return sectorNumber >= FirstSector &&
            sectorNumber <= ExplorationSectorCount;
    }

    public static bool IsBossSector(int sectorNumber)
    {
        return sectorNumber == FinalBossSector;
    }
}
