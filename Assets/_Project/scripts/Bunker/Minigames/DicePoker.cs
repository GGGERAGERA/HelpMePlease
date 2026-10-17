using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DicePoker : ArcadeMinigame
{
    public enum Hand { Pair, TwoPairs, Three, Straight, FullHouse, Four, Five, Chance }
    private const int Rounds = 6, Target = 250;
    protected override string RecordKey => "BunkerDicePokerBest";
    protected override string Title => L("ПОКЕР НА КОСТЯХ", "DICE POKER");
    private readonly int[] dice = new int[5], recorded = new int[8];
    private readonly bool[] held = new bool[5], used = new bool[8];
    private readonly Button[] dieButtons = new Button[5], rows = new Button[8];
    private readonly Image[,] pips = new Image[5, 9];
    private readonly TMP_Text[] holdLabels = new TMP_Text[5], rowLabels = new TMP_Text[8];
    private Button rollButton, bankButton;
    private TMP_Text message, roundHistory, costs;
    private int round, rolls, selected;
    private float rolling;
    private System.Random random;
    private string history;
    public int Round => round;
    public int Rolls => rolls;
    public bool IsRolling => rolling > 0;
    public int Die(int index) => dice[index];
    public bool IsHeld(int index) => held[index];

    protected override void BuildGame()
    {
        Box(Content, new Vector2(-285, -7), new Vector2(510, 626), Panel);
        message = Label(Content, "", new Vector2(-285, 221), new Vector2(485, 130), 23, Mint);
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            dieButtons[i] = Button(Content, "", new Vector2(-485 + i * 100, 97), new Vector2(84, 84), () => ToggleHold(index));
            dieButtons[i].transition = Selectable.Transition.None;
            dieButtons[i].GetComponent<Image>().color = Gold;
            for (int p = 0; p < 9; p++)
                pips[i, p] = Box(dieButtons[i].transform, new Vector2((p % 3 - 1) * 21, (1 - p / 3) * 21), Vector2.one * 11, Ink);
            holdLabels[i] = Label(Content, "", new Vector2(-485 + i * 100, 31), new Vector2(96, 40), 14, Mint);
        }
        rollButton = Button(Content, L("БРОСОК [ПРОБЕЛ]", "ROLL [SPACE]"), new Vector2(-285, -21), new Vector2(450, 48), Roll);
        bankButton = Button(Content, L("ЗАПИСАТЬ [ENTER]", "BANK HAND [ENTER]"), new Vector2(-285, -82), new Vector2(450, 48), Bank);
        Label(Content, L("Клик по кости / 1-5: удержать\nДо 3 бросков. Записывай раньше или рискуй.\nВыбери строку справа, затем ЗАПИСАТЬ.\nКаждая строка доступна ОДИН раз.\nНеподходящая комбинация: 0 очков.\n6 раундов / цель: 250 очков", "Click dice / 1-5: hold\nUp to 3 rolls. Bank early or take a risk.\nSelect a row, then BANK HAND.\nEach row can be used ONCE.\nAn unmatched hand scores zero.\n6 rounds / target: 250 points"), new Vector2(-285, -190), new Vector2(480, 145), 17, Color.white);
        roundHistory = Label(Content, "", new Vector2(-285, -292), new Vector2(480, 50), 15, Gold);
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            rows[i] = Button(Content, "", new Vector2(280, 222 - i * 33), new Vector2(520, 29), () => { selected = index; RefreshStatus(); });
            rowLabels[i] = rows[i].GetComponentInChildren<TMP_Text>();
            rowLabels[i].fontSize = 16;
        }
        costs = Label(Content, L("Пара 10 / две пары 30 / тройка 45 + сумма\nСтрит 85 / фулл-хаус 100 / каре 125\nПокер 180 / шанс: сумма всех костей", "Pair 10 / two pairs 30 / three 45 + dice sum\nStraight 85 / full house 100 / four 125\nFive 180 / chance: sum of all dice"), new Vector2(280, -45), new Vector2(520, 65), 15, Mint);
    }

    protected override void BeginRound()
    {
        random = new System.Random();
        round = 1; history = ""; roundHistory.text = "";
        System.Array.Clear(used, 0, used.Length);
        System.Array.Clear(recorded, 0, recorded.Length);
        NewHand();
    }
    private void NewHand()
    {
        rolls = 0; rolling = 0; selected = (int)Hand.Chance;
        System.Array.Clear(held, 0, held.Length);
        System.Array.Clear(dice, 0, dice.Length);
        DrawDice(false);
    }
    protected override void Tick(float dt)
    {
        if (rolling > 0)
        {
            rolling = Mathf.Max(0, rolling - dt);
            DrawDice(rolling > 0);
            if (rolling == 0)
            {
                int best = -1;
                for (int i = 0; i < used.Length; i++)
                    if (!used[i] && (best < 0 || Evaluate(dice, (Hand)i) > Evaluate(dice, (Hand)best))) best = i;
                selected = best;
            }
            return;
        }
        for (int i = 0; i < 5; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) ToggleHold(i);
        if (Input.GetKeyDown(KeyCode.Space)) Roll();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Bank();
    }
    public void ToggleHold(int index)
    {
        if (!IsRunning || rolling > 0 || rolls == 0 || rolls >= 3 || index < 0 || index >= 5) return;
        held[index] = !held[index];
        DrawDice(false);
    }
    private bool AllHeld()
    {
        for (int i = 0; i < 5; i++) if (!held[i]) return false;
        return true;
    }
    public void Roll()
    {
        if (!IsRunning || rolling > 0 || rolls >= 3 || AllHeld()) return;
        // The roll is sampled once; animation never changes the resulting dice.
        for (int i = 0; i < 5; i++) if (!held[i]) dice[i] = random.Next(1, 7);
        rolls++;
        rolling = .55f;
        EventSystemDeselect();
    }
    public void Bank()
    {
        if (!IsRunning || rolling > 0 || rolls == 0 || selected < 0 || used[selected]) return;
        int points = Evaluate(dice, (Hand)selected);
        used[selected] = true; recorded[selected] = points;
        Score += points;
        history += (round > 1 ? " / " : "") + points;
        roundHistory.text = history;
        EventSystemDeselect();
        if (round == Rounds)
            Finish(Score >= Target, L($"Итого: {Score} / {Target}\nРезультаты: {history}", $"Total: {Score} / {Target}\nRounds: {history}"));
        else { round++; NewHand(); }
    }
    private void EventSystemDeselect() => UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

    public static int Evaluate(int[] values, Hand hand)
    {
        if (values == null || values.Length != 5) return 0;
        int[] counts = new int[7];
        int sum = 0, max = 0, pairs = 0, distinct = 0;
        foreach (int value in values)
        {
            if (value < 1 || value > 6) return 0;
            counts[value]++; sum += value;
        }
        for (int i = 1; i <= 6; i++)
        {
            max = Mathf.Max(max, counts[i]);
            if (counts[i] == 2) pairs++;
            if (counts[i] > 0) distinct++;
        }
        return hand switch
        {
            Hand.Pair => max >= 2 ? 10 + sum : 0,
            Hand.TwoPairs => pairs == 2 ? 30 + sum : 0,
            Hand.Three => max >= 3 ? 45 + sum : 0,
            Hand.Straight => distinct == 5 && (counts[1] == 0 || counts[6] == 0) ? 85 : 0,
            Hand.FullHouse => max == 3 && pairs == 1 ? 100 : 0,
            Hand.Four => max >= 4 ? 125 : 0,
            Hand.Five => max == 5 ? 180 : 0,
            Hand.Chance => sum,
            _ => 0
        };
    }
    private string Name(Hand hand) => hand switch
    {
        Hand.Pair => L("ПАРА", "PAIR"), Hand.TwoPairs => L("ДВЕ ПАРЫ", "TWO PAIRS"),
        Hand.Three => L("ТРОЙКА", "THREE OF A KIND"), Hand.Straight => L("СТРИТ", "STRAIGHT"),
        Hand.FullHouse => L("ФУЛЛ-ХАУС", "FULL HOUSE"), Hand.Four => L("КАРЕ", "FOUR OF A KIND"),
        Hand.Five => L("ПОКЕР", "FIVE OF A KIND"), _ => L("ШАНС", "CHANCE")
    };
    private void DrawDice(bool animate)
    {
        for (int i = 0; i < 5; i++)
        {
            // A deterministic cosmetic cycle keeps the actual RNG exclusive to rolls.
            int value = animate && !held[i] ? 1 + ((int)(Time.unscaledTime * 28) + i * 3) % 6 : dice[i];
            for (int p = 0; p < 9; p++)
            {
                bool show = value == 1 ? p == 4 : value == 2 ? p == 0 || p == 8 : value == 3 ? p == 0 || p == 4 || p == 8
                    : value == 4 ? p == 0 || p == 2 || p == 6 || p == 8
                    : value == 5 ? p == 0 || p == 2 || p == 4 || p == 6 || p == 8
                    : value == 6 && (p % 3 == 0 || p % 3 == 2);
                pips[i, p].enabled = show;
            }
            dieButtons[i].GetComponent<Image>().color = held[i] ? Mint : Gold;
            holdLabels[i].text = held[i] ? L("УДЕРЖАН", "HELD") : $"[{i + 1}]";
        }
    }
    protected override void RefreshStatus()
    {
        Status.text = L($"ОЧКИ {Score}   РЕКОРД {BestScore}\nРАУНД {round}/{Rounds}   БРОСОК {rolls}/3", $"SCORE {Score}   BEST {BestScore}\nROUND {round}/{Rounds}   ROLL {rolls}/3");
        bool ready = IsRunning && rolling <= 0;
        costs.gameObject.SetActive(IsRunning);
        rollButton.interactable = ready && rolls < 3 && !AllHeld();
        bankButton.interactable = ready && rolls > 0 && selected >= 0 && !used[selected];
        for (int i = 0; i < 8; i++)
        {
            rows[i].interactable = ready && rolls > 0 && !used[i];
            int points = used[i] ? recorded[i] : rolling > 0 ? 0 : Evaluate(dice, (Hand)i);
            rowLabels[i].text = (selected == i && !used[i] ? "> " : "") + Name((Hand)i) + "   " +
                (used[i] ? L($"ЗАПИСАНО {points}", $"BANKED {points}") : $"+{points}");
            rowLabels[i].color = used[i] ? Color.gray : selected == i ? Gold : Color.white;
        }
        for (int i = 0; i < 5; i++) dieButtons[i].interactable = ready && rolls > 0 && rolls < 3;
        message.text = !IsRunning ? L("ПАРТИЯ ЗАВЕРШЕНА", "GAME OVER") : rolling > 0 ? L("БРОСОК...", "ROLLING...")
            : rolls == 0 ? L("БРОСЬ ПЯТЬ КОСТЕЙ", "ROLL FIVE DICE")
            : L($"{Name((Hand)selected)}: +{Evaluate(dice, (Hand)selected)}\n", $"{Name((Hand)selected)}: +{Evaluate(dice, (Hand)selected)}\n") +
                (rolls == 3 ? L("Выбери строку и запиши результат", "Select a row and bank your hand")
                : L("Сохранить или перебросить?", "Bank it or risk another roll?"));
    }
}
