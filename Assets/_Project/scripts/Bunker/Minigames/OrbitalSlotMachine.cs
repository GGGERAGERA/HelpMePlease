using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Values 1..6 are persisted; keep them stable.
public enum OrbitalSlotSymbol { None, Gun, Sword, Impulse, Arc, Link, Ring, Gold, Skull }

public static class OrbitalSlotMachine
{
    public const int Stake = 50;
    private const string PendingKey = "ORBITAL_SLOT_PENDING";
    private static bool resolving;

    public static OrbitalSlotSymbol Pending => (OrbitalSlotSymbol)PlayerPrefs.GetInt(PendingKey, 0);
    public static bool CanSpin(CurrencyManager currency) => !resolving &&
        Pending == OrbitalSlotSymbol.None && currency != null && currency.TotalGold >= Stake;

    public static bool TrySpin(CurrencyManager currency, out OrbitalSlotSymbol[] reels) =>
        Spin(currency, null, out reels);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugSpin(CurrencyManager currency, OrbitalSlotSymbol a,
        OrbitalSlotSymbol b, OrbitalSlotSymbol c, out OrbitalSlotSymbol[] reels) =>
        Spin(currency, new[] { a, b, c }, out reels);
#endif

    private static bool Spin(CurrencyManager currency, OrbitalSlotSymbol[] forced,
        out OrbitalSlotSymbol[] reels)
    {
        reels = null;
        if (!CanSpin(currency)) return false;
        resolving = true;
        try
        {
            if (!currency.SpendGold(Stake)) return false;
            reels = forced ?? new[] { Roll(), Roll(), Roll() };
            var a = reels[0]; var b = reels[1]; var c = reels[2];
            if (a == b && b == c)
            {
                if (a >= OrbitalSlotSymbol.Gun && a <= OrbitalSlotSymbol.Ring)
                {
                    PlayerPrefs.SetInt(PendingKey, (int)a);
                    PlayerPrefs.Save();
                }
                else if (a == OrbitalSlotSymbol.Gold) currency.AddGoldExact(250);
            }
            else if (a == b || a == c || b == c) currency.AddGoldExact(Stake);
            return true;
        }
        finally { resolving = false; }
    }

    public static OrbitalSlotSymbol Roll() => (OrbitalSlotSymbol)Random.Range(1, 9);

    public static void ClearPending()
    {
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
    }


    public static OrbitalModuleKind ModuleKind(OrbitalSlotSymbol symbol) => symbol switch
    {
        OrbitalSlotSymbol.Gun => OrbitalModuleKind.Pistol,
        OrbitalSlotSymbol.Sword => OrbitalModuleKind.LaserSword,
        OrbitalSlotSymbol.Impulse => OrbitalModuleKind.ImpulseGun,
        OrbitalSlotSymbol.Arc => OrbitalModuleKind.ArcEmitter,
        OrbitalSlotSymbol.Link => OrbitalModuleKind.LinkNode,
        _ => throw new System.ArgumentOutOfRangeException(nameof(symbol))
    };

    public static string Label(OrbitalSlotSymbol symbol) => symbol switch
    {
        OrbitalSlotSymbol.Sword => LocalizationService.Instance.Get("bunker.slot_sword"),
        OrbitalSlotSymbol.Link => LocalizationService.Instance.Get("bunker.slot_link"),
        OrbitalSlotSymbol.Ring => LocalizationService.Instance.Get("bunker.slot_new_ring"),
        OrbitalSlotSymbol.None => LocalizationService.Instance.Get("bunker.slot_symbol.None"),
        OrbitalSlotSymbol.Gun => LocalizationService.Instance.Get("bunker.slot_symbol.Gun"),
        OrbitalSlotSymbol.Impulse => LocalizationService.Instance.Get("bunker.slot_symbol.Impulse"),
        OrbitalSlotSymbol.Arc => LocalizationService.Instance.Get("bunker.slot_symbol.Arc"),
        OrbitalSlotSymbol.Gold => LocalizationService.Instance.Get("bunker.slot_symbol.Gold"),
        OrbitalSlotSymbol.Skull => LocalizationService.Instance.Get("bunker.slot_symbol.Skull"),
        _ => symbol.ToString().ToUpperInvariant()
    };

    public static string ResultText(OrbitalSlotSymbol[] reels)
    {
        var a = reels[0]; var b = reels[1]; var c = reels[2];
        if (a == b && b == c)
            return a == OrbitalSlotSymbol.Gold ? LocalizationService.Instance.Get("bunker.slot_250") :
                a == OrbitalSlotSymbol.Skull ? LocalizationService.Instance.Get("bunker.slot_skull_none") :
                string.Format(LocalizationService.Instance.Get("bunker.slot_bonus"), Label(a));
        return a == b || a == c || b == c ? LocalizationService.Instance.Get("bunker.slot_stake_returned") : LocalizationService.Instance.Get("bunker.slot_no_reward");
    }
}
