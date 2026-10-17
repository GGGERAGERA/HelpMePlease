using System;
using UnityEngine;

// Reservations use the existing gold wallet. The pending round and wallet are
// saved together, so reopening recovers it once instead of minting a new bankroll.
public sealed class RouletteSession
{
    public const string PendingKey = "BUNKER_ROULETTE_PENDING";
    public const int Red = 37, Black = 38, Even = 39, Odd = 40, Low = 41, High = 42,
        FirstDozen = 43, SecondDozen = 44, ThirdDozen = 45, BetCount = 46;
    public static readonly int[] Wheel = { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };
    public static readonly int[] Chips = { 5, 10, 25, 100 };
    private readonly CurrencyManager wallet;
    private readonly int[] bets = new int[BetCount], previous = new int[BetCount];
    private int outcome = -1;
    private string roundId;
    [Serializable] private sealed class PendingRound
    {
        public int version = 1;
        public string id;
        public int[] bets;
        public int stake;
        public bool spinning;
        public int outcome = -1;
    }
    public int Balance => wallet.TotalGold;
    public int Stake { get; private set; }
    public int PreviousStake { get; private set; }
    public int LastReturn { get; private set; }
    public int LastDelta { get; private set; }
    public int LastNumber { get; private set; } = -1;
    public bool Spinning { get; private set; }
    public bool IsBroke => Balance < Chips[0] && Stake == 0 && !Spinning;
    public bool CanRepeat => !Spinning && PreviousStake > 0 && (long)Balance + Stake >= PreviousStake &&
        CanCoverReturns(previous, (long)Balance + Stake - PreviousStake);
    public int Bet(int id) => id >= 0 && id < BetCount ? bets[id] : 0;

    public RouletteSession(CurrencyManager currency)
    {
        wallet = currency != null ? currency : throw new ArgumentNullException(nameof(currency));
        RecoverPending();
    }
    public bool Place(int id, int chip)
    {
        if (Spinning || id < 0 || id >= BetCount || Array.IndexOf(Chips, chip) < 0 || Balance < chip ||
            Stake > int.MaxValue / 36 - chip || !OwnsPending()) return false;
        var candidate = (int[])bets.Clone(); candidate[id] += chip;
        if (!CanCoverReturns(candidate, Balance - chip)) return false;
        return wallet.TryChangeGoldExact(-chip, () =>
        {
            if (Stake == 0) roundId = Guid.NewGuid().ToString("N");
            bets[id] += chip; Stake += chip; StagePending();
        });
    }
    public bool Clear()
    {
        if (Spinning) return false;
        if (Stake == 0) return true;
        if (!OwnsPending()) return false;
        int refund = Mathf.Min(Stake, int.MaxValue - Balance);
        return wallet.TryChangeGoldExact(refund, () =>
        {
            Stake = 0; Array.Clear(bets, 0, bets.Length); PlayerPrefs.DeleteKey(PendingKey);
        });
    }
    public bool Repeat()
    {
        if (!CanRepeat || !OwnsPending()) return false;
        return wallet.TryChangeGoldExact(Stake - PreviousStake, () =>
        {
            if (Stake == 0) roundId = Guid.NewGuid().ToString("N");
            Stake = PreviousStake; Array.Copy(previous, bets, BetCount); StagePending();
        });
    }
    public bool StartSpin(int winningNumber)
    {
        if (Spinning || Stake == 0 || winningNumber < 0 || winningNumber > 36 || !OwnsPending() || !CanCoverReturns(bets, Balance)) return false;
        return wallet.TryChangeGoldExact(0, () =>
        {
            Spinning = true; outcome = winningNumber;
            PreviousStake = Stake; Array.Copy(bets, previous, BetCount); StagePending();
        });
    }
    public bool Resolve(int number)
    {
        if (!Spinning || number != outcome || !OwnsPending()) return false;
        long paid = 0;
        for (int id = 0; id < BetCount; id++) paid += bets[id] * ReturnMultiplier(id, number);
        // Respect the wallet's existing integer cap if an external debug grant
        // fills it while the wheel is spinning. Normal stakes are checked above.
        int credit = (int)Math.Min(paid, int.MaxValue - (long)Balance);
        return wallet.TryChangeGoldExact(credit, () =>
        {
            LastReturn = credit; LastDelta = credit - Stake; LastNumber = number;
            Stake = 0; Spinning = false; outcome = -1;
            Array.Clear(bets, 0, bets.Length); PlayerPrefs.DeleteKey(PendingKey);
        });
    }
    public void Close() { if (Spinning) Resolve(outcome); else Clear(); }

    private bool OwnsPending()
    {
        if (Stake == 0) return !PlayerPrefs.HasKey(PendingKey);
        try { return JsonUtility.FromJson<PendingRound>(PlayerPrefs.GetString(PendingKey, ""))?.id == roundId; }
        catch (ArgumentException) { return false; }
    }

    private static bool CanCoverReturns(int[] amounts, long balanceAfterStake)
    {
        for (int n = 0; n <= 36; n++)
        {
            long total = balanceAfterStake;
            for (int id = 0; id < BetCount; id++) total += (long)amounts[id] * ReturnMultiplier(id, n);
            if (total > int.MaxValue) return false;
        }
        return true;
    }
    private void StagePending() => PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(new PendingRound
    { id = roundId, bets = bets, stake = Stake, spinning = Spinning, outcome = outcome }));

    private void RecoverPending()
    {
        string json = PlayerPrefs.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return;
        PendingRound saved = null;
        try { saved = JsonUtility.FromJson<PendingRound>(json); } catch (ArgumentException) { }
        long sum = 0;
        if (saved?.bets != null) foreach (int value in saved.bets) { if (value < 0) { sum = -1; break; } sum += value; }
        if (saved == null || saved.version != 1 || string.IsNullOrEmpty(saved.id) || saved.bets == null || saved.bets.Length != BetCount ||
            sum <= 0 || sum != saved.stake || sum > int.MaxValue / 36 || (saved.spinning && (saved.outcome < 0 || saved.outcome > 36)))
        {
            PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); return;
        }
        Array.Copy(saved.bets, bets, BetCount); Stake = saved.stake;
        Spinning = saved.spinning; outcome = saved.outcome; roundId = saved.id;
        if (Spinning) { PreviousStake = Stake; Array.Copy(bets, previous, BetCount); Resolve(outcome); }
        else Clear();
    }
    public static bool IsRed(int n) => n is 1 or 3 or 5 or 7 or 9 or 12 or 14 or 16 or 18 or 19 or 21 or 23 or 25 or 27 or 30 or 32 or 34 or 36;
    public static int ReturnMultiplier(int id, int number)
    {
        if (id < 0 || id >= BetCount || number < 0 || number > 36) return 0;
        if (id <= 36) return id == number ? 36 : 0;
        if (number == 0) return 0;
        return id switch
        {
            Red => IsRed(number) ? 2 : 0,
            Black => !IsRed(number) ? 2 : 0,
            Even => number % 2 == 0 ? 2 : 0,
            Odd => number % 2 == 1 ? 2 : 0,
            Low => number <= 18 ? 2 : 0,
            High => number >= 19 ? 2 : 0,
            FirstDozen => number <= 12 ? 3 : 0,
            SecondDozen => number >= 13 && number <= 24 ? 3 : 0,
            ThirdDozen => number >= 25 ? 3 : 0,
            _ => 0
        };
    }
}
