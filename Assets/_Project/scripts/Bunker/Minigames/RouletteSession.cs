using System;
using UnityEngine;

// A single colour wager uses the bunker wallet. Debit and locked outcome are
// persisted together; closing or reloading settles the same round exactly once.
public sealed class RouletteSession
{
    public const string PendingKey = "BUNKER_ROULETTE_PENDING";
    public const int Red = 0, Black = 1, Green = 2;
    public static readonly int[] Wheel = { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };
    private readonly CurrencyManager wallet;
    private int outcome = -1, colour;
    private string roundId;
    [Serializable] private sealed class PendingRound
    {
        public int version;
        public string id;
        public int colour, stake, outcome;
        public bool spinning;
        public int[] bets; // Read-only migration of an already paid classic round.
    }
    public int Balance => wallet.TotalGold;
    public int Stake { get; private set; }
    public int LastReturn { get; private set; }
    public int LastDelta { get; private set; }
    public int LastNumber { get; private set; } = -1;
    public bool Spinning { get; private set; }

    public RouletteSession(CurrencyManager currency)
    {
        wallet = currency != null ? currency : throw new ArgumentNullException(nameof(currency));
        RecoverPending();
    }
    public bool CanStake(int selectedColour, int amount) => !Spinning && selectedColour >= Red && selectedColour <= Green &&
        amount > 0 && amount <= Balance && (long)Balance + (long)amount * (Multiplier(selectedColour) - 1) <= int.MaxValue;

    public bool StartSpin(int selectedColour, int amount, int winningNumber)
    {
        if (!CanStake(selectedColour, amount) || winningNumber < 0 || winningNumber > 36 || PlayerPrefs.HasKey(PendingKey)) return false;
        return wallet.TryChangeGoldExact(-amount, () =>
        {
            roundId = Guid.NewGuid().ToString("N");
            colour = selectedColour; Stake = amount; outcome = winningNumber; Spinning = true;
            PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(new PendingRound
            { version = 2, id = roundId, colour = colour, stake = Stake, spinning = true, outcome = outcome }));
        });
    }
    public bool Resolve(int number)
    {
        if (!Spinning || number != outcome || !OwnsPending()) return false;
        long paid = (long)Stake * ReturnMultiplier(colour, number);
        int credit = (int)Math.Min(paid, int.MaxValue - (long)Balance);
        return wallet.TryChangeGoldExact(credit, () =>
        {
            LastReturn = credit; LastDelta = credit - Stake; LastNumber = number;
            Stake = 0; Spinning = false; outcome = -1;
            PlayerPrefs.DeleteKey(PendingKey);
        });
    }
    public void Close() { if (Spinning) Resolve(outcome); }
    private bool OwnsPending()
    {
        try { return JsonUtility.FromJson<PendingRound>(PlayerPrefs.GetString(PendingKey, ""))?.id == roundId; }
        catch (ArgumentException) { return false; }
    }
    private void RecoverPending()
    {
        if (!PlayerPrefs.HasKey(PendingKey)) return;
        PendingRound saved = null;
        try { saved = JsonUtility.FromJson<PendingRound>(PlayerPrefs.GetString(PendingKey)); } catch (ArgumentException) { }
        if (saved != null && !string.IsNullOrEmpty(saved.id) && saved.stake > 0)
        {
            if (saved.version == 2 && saved.spinning && saved.colour >= Red && saved.colour <= Green &&
                saved.stake <= int.MaxValue / Multiplier(saved.colour) && saved.outcome >= 0 && saved.outcome <= 36)
            {
                roundId = saved.id; Stake = saved.stake; colour = saved.colour; outcome = saved.outcome; Spinning = true;
                Resolve(outcome); return;
            }
            if (saved.version == 1 && RecoverClassic(saved)) return;
        }
        PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save();
    }
    private bool RecoverClassic(PendingRound saved)
    {
        if (saved.bets == null || saved.bets.Length != 46 || saved.stake > int.MaxValue / 36 ||
            (saved.spinning && (saved.outcome < 0 || saved.outcome > 36))) return false;
        long sum = 0, paid = 0;
        for (int i = 0; i < saved.bets.Length; i++)
        {
            if (saved.bets[i] < 0) return false;
            sum += saved.bets[i];
            paid += (long)saved.bets[i] * LegacyReturn(i, saved.outcome);
        }
        if (sum != saved.stake) return false;
        int credit = (int)Math.Min(saved.spinning ? paid : sum, int.MaxValue - (long)Balance);
        return wallet.TryChangeGoldExact(credit, () => PlayerPrefs.DeleteKey(PendingKey));
    }
    private static int LegacyReturn(int id, int n)
    {
        if (id <= 36) return id == n ? 36 : 0;
        if (n <= 0) return 0;
        return id switch
        {
            37 => IsRed(n) ? 2 : 0, 38 => !IsRed(n) ? 2 : 0,
            39 => n % 2 == 0 ? 2 : 0, 40 => n % 2 == 1 ? 2 : 0,
            41 => n <= 18 ? 2 : 0, 42 => n >= 19 ? 2 : 0,
            43 => n <= 12 ? 3 : 0, 44 => n >= 13 && n <= 24 ? 3 : 0,
            45 => n >= 25 ? 3 : 0, _ => 0
        };
    }
    public static bool IsRed(int n) => n is 1 or 3 or 5 or 7 or 9 or 12 or 14 or 16 or 18 or 19 or 21 or 23 or 25 or 27 or 30 or 32 or 34 or 36;
    public static int ColourOf(int number) => number == 0 ? Green : IsRed(number) ? Red : Black;
    public static int Multiplier(int selectedColour) => selectedColour == Green ? 35 : 2;
    public static int ReturnMultiplier(int selectedColour, int number) => selectedColour >= Red && selectedColour <= Green &&
        number >= 0 && number <= 36 && selectedColour == ColourOf(number) ? Multiplier(selectedColour) : 0;
}
