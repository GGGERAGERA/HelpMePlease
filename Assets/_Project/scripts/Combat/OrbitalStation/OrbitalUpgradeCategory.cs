namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalUpgradeCategory { Shooting = 0, Contact = 1, Orbits = 2, Character = 3 }
    public static class OrbitalCategories
    {
        public static OrbitalUpgradeCategory ForModule(OrbitalModuleKind kind) => kind switch
        {
            OrbitalModuleKind.Saw or OrbitalModuleKind.LinkNode => OrbitalUpgradeCategory.Contact,
            _ => OrbitalUpgradeCategory.Shooting
        };
        public static string Label(OrbitalUpgradeCategory category) => category switch
        {
            OrbitalUpgradeCategory.Shooting => "СТРЕЛКОВОЕ",
            OrbitalUpgradeCategory.Contact => "КОНТАКТНОЕ",
            OrbitalUpgradeCategory.Orbits => "ОРБИТЫ",
            _ => "ПЕРСОНАЖ"
        };
    }
}
