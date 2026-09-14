using System.Collections;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerOrbitalSlotPanel : MonoBehaviour
{
    [SerializeField] private Image[] reels;
    [SerializeField] private TMP_Text[] reelLabels;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private Button spinButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Sprite goldIcon;
    [SerializeField] private Sprite skullIcon;

    [Header("Presentation")]
    [SerializeField] private RectTransform panelBody;
    [SerializeField] private Image[] upperSymbols;
    [SerializeField] private Image[] lowerSymbols;
    [SerializeField] private Image[] reelBorders;
    [SerializeField] private Image flash;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private GameObject pendingBanner;
    [SerializeField] private Image pendingIcon;
    [SerializeField] private TMP_Text pendingText;

    private static readonly Color Cyan = new Color(.12f, .88f, .94f);
    private static readonly Color Violet = new Color(.72f, .38f, 1f);
    private static readonly Color IdleBorder = new Color(.12f, .36f, .42f);
    private readonly OrbitalRewardIconResolver.Icon[] icons = new OrbitalRewardIconResolver.Icon[9];
    private OrbitalSlotSymbol[] result = { OrbitalSlotSymbol.Gun, OrbitalSlotSymbol.Ring, OrbitalSlotSymbol.Gold };
    private readonly float[] nextChange = new float[3];
    private readonly bool[] stopped = new bool[3];
    private Vector2 bodyPosition;
    private bool spinning;
    private bool hasSpun;
    private CurrencyManager currency;
    public bool IsOpen => gameObject.activeInHierarchy;

    private void Awake()
    {
        for (var symbol = OrbitalSlotSymbol.Gun; symbol <= OrbitalSlotSymbol.Link; symbol++)
            icons[(int)symbol] = OrbitalRewardIconResolver.Resolve(OrbitalSlotMachine.ModuleKind(symbol));
        icons[(int)OrbitalSlotSymbol.Ring] = OrbitalRewardIconResolver.Resolve(OrbitalRewardKind.NewRing);
        icons[(int)OrbitalSlotSymbol.Gold] = new OrbitalRewardIconResolver.Icon(goldIcon, Color.white);
        icons[(int)OrbitalSlotSymbol.Skull] = new OrbitalRewardIconResolver.Icon(skullIcon, Color.white);
        bodyPosition = panelBody.anchoredPosition;
        spinButton.onClick.AddListener(Spin);
        closeButton.onClick.AddListener(Hide);
    }

    private void OnEnable()
    {
        currency = CurrencyManager.Instance;
        if (currency != null) currency.OnGoldUpdated += Refresh;
        ResetEffects();
        ShowReels(result);
        if (hasSpun) ShowResult();
        else
        {
            statusText.text = "ИСПЫТАЙ УДАЧУ";
            rewardText.text = "3 ОДИНАКОВЫХ — JACKPOT · 2 — ВОЗВРАТ";
        }
        Refresh(0);
    }

    private void OnDisable()
    {
        if (currency != null) currency.OnGoldUpdated -= Refresh;
        StopAllCoroutines();
        spinning = false;
        ResetEffects();
        // The outcome was committed before animation; closing never cancels a paid spin.
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    public void Spin()
    {
        if (spinning || SceneTransitionOverlay.IsTransitioning) return;
        spinning = true;
        if (!OrbitalSlotMachine.TrySpin(currency, out var rolled))
        {
            spinning = false;
            Refresh(0);
            return;
        }
        result = rolled;
        hasSpun = true;
        ResetEffects();
        statusText.text = "ВРАЩЕНИЕ";
        rewardText.text = "";
        Refresh(0);
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        for (int i = 0; i < 3; i++)
        {
            nextChange[i] = i * .015f;
            stopped[i] = false;
        }
        float elapsed = 0f;
        int cycle = 0;
        while (elapsed < 1.2f)
        {
            bool tick = false;
            for (int i = 0; i < 3; i++)
            {
                float stopAt = .8f + i * .2f;
                if (elapsed >= stopAt)
                {
                    if (!stopped[i]) StopReel(i);
                    continue;
                }
                if (elapsed >= nextChange[i])
                {
                    // Cosmetic cycling must not consume the gameplay random stream.
                    SetReel(i, (OrbitalSlotSymbol)(1 + (cycle++ + i * 3) % 8));
                    float progress = elapsed / stopAt;
                    nextChange[i] = elapsed + Mathf.Lerp(.035f, .14f, progress * progress);
                    tick = true;
                }
                float travel = Mathf.Clamp01((nextChange[i] - elapsed) / .14f);
                reels[i].rectTransform.anchoredPosition = new Vector2(0, travel * 16f);
            }
            if (tick) PlayCue(AudioCueId.UIHover);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        for (int i = 0; i < 3; i++)
            if (!stopped[i]) StopReel(i);

        ShowResult();
        yield return Feedback();
        spinning = false;
        Refresh(0);
    }

    private void StopReel(int index)
    {
        stopped[index] = true;
        SetReel(index, result[index]);
        reelBorders[index].color = Cyan;
        PlayCue(AudioCueId.UIConfirm);
    }

    private bool Triple => result[0] == result[1] && result[1] == result[2];
    private bool Pair => result[0] == result[1] || result[0] == result[2] || result[1] == result[2];

    private static string RewardLabel(OrbitalSlotSymbol symbol) =>
        symbol == OrbitalSlotSymbol.Gold ? "+250 GOLD" :
        symbol == OrbitalSlotSymbol.Ring ? "+1 RING" : "+1 " + OrbitalSlotMachine.Label(symbol);

    private void ShowResult()
    {
        bool jackpot = Triple && result[0] != OrbitalSlotSymbol.Skull;
        statusText.text = jackpot ? "JACKPOT" : Triple ? "СИСТЕМА ШУТИТ" :
            Pair ? "СТАВКА ВОЗВРАЩЕНА" : "БЕЗ ВЫИГРЫША";
        statusText.color = jackpot ? Cyan : Triple ? Violet : Pair ? Cyan : new Color(.55f, .64f, .68f);
        rewardText.text = jackpot ? RewardLabel(result[0]) :
            Triple ? "SKULL ×3 · БЕЗ ВЫИГРЫША" : Pair ? "+50 GOLD" : "ПОПРОБУЙ ЕЩЁ";
        for (int i = 0; i < 3; i++)
            reelBorders[i].color = jackpot ? Cyan : Triple ? Violet : IdleBorder;
    }

    private IEnumerator Feedback()
    {
        bool jackpot = Triple && result[0] != OrbitalSlotSymbol.Skull;
        bool skull = Triple && !jackpot;
        Color color = jackpot ? Cyan : skull ? Violet : Pair ? Cyan : Color.black;
        if (jackpot) PlayCue(AudioCueId.RewardSelect);
        for (float t = 0; t < .28f; t += Time.unscaledDeltaTime)
        {
            float fade = 1f - t / .28f;
            flash.color = new Color(color.r, color.g, color.b, fade * (jackpot ? .45f : .18f));
            if (jackpot || skull)
            {
                panelBody.anchoredPosition = bodyPosition + new Vector2(
                    Mathf.Sin(t * 155f), Mathf.Cos(t * 117f)) * fade * (skull ? 5f : 3f);
                statusText.rectTransform.localScale = Vector3.one * (1f + fade * .12f);
            }
            if (skull)
                flash.color = Color.Lerp(new Color(1f,.1f,.2f,.22f*fade),
                    new Color(Violet.r,Violet.g,Violet.b,.25f*fade), Mathf.PingPong(t * 30f, 1f));
            for (int i = 0; i < 3; i++)
                reelBorders[i].color = Color.Lerp(jackpot ? Cyan : skull ? Violet : IdleBorder,
                    color, fade);
            yield return null;
        }
        flash.color = Color.clear;
        panelBody.anchoredPosition = bodyPosition;
        statusText.rectTransform.localScale = Vector3.one;
    }

    private void Refresh(int unused)
    {
        walletText.text = $"GOLD  {(currency != null ? currency.TotalGold : 0)}";
        spinButton.interactable = !spinning && OrbitalSlotMachine.CanSpin(currency);
        OrbitalSlotSymbol pending = OrbitalSlotMachine.Pending;
        bool showPending = !spinning && pending != OrbitalSlotSymbol.None;
        pendingBanner.SetActive(showPending);
        if (showPending)
        {
            SetIcon(pendingIcon, pending, 1f);
            pendingText.text = "NEXT RUN BONUS:  " + RewardLabel(pending);
        }
        hintText.text = spinning ? "СИНХРОНИЗАЦИЯ БАРАБАНОВ…" :
            showPending ? "НАЧНИТЕ RUN, ЧТОБЫ ИГРАТЬ СНОВА" :
            currency == null || currency.TotalGold < OrbitalSlotMachine.Stake ?
                "НЕДОСТАТОЧНО GOLD — НУЖНО 50" : "БОНУС ДЕЙСТВУЕТ НА СЛЕДУЮЩИЙ RUN";
    }

    private void ResetEffects()
    {
        panelBody.anchoredPosition = bodyPosition;
        flash.color = Color.clear;
        statusText.color = Cyan;
        statusText.rectTransform.localScale = Vector3.one;
        for (int i = 0; i < 3; i++) reelBorders[i].color = IdleBorder;
    }

    private void ShowReels(OrbitalSlotSymbol[] symbols)
    {
        for (int i = 0; i < 3; i++) SetReel(i, symbols[i]);
    }

    private void SetReel(int index, OrbitalSlotSymbol symbol)
    {
        SetIcon(reels[index], symbol, 1f);
        reels[index].rectTransform.anchoredPosition = Vector2.zero;
        SetIcon(upperSymbols[index], (OrbitalSlotSymbol)(1 + ((int)symbol + 6) % 8), .22f);
        SetIcon(lowerSymbols[index], (OrbitalSlotSymbol)(1 + (int)symbol % 8), .22f);
        reelLabels[index].text = OrbitalSlotMachine.Label(symbol);
    }

    private void SetIcon(Image image, OrbitalSlotSymbol symbol, float alpha)
    {
        var icon = icons[(int)symbol];
        image.sprite = icon.Sprite;
        Color tint = icon.ImageTint;
        tint.a *= alpha;
        image.color = tint;
    }

    private static void PlayCue(AudioCueId cue)
    {
        // Audio is optional in the existing Bunker service lifecycle.
        if (AudioService.Instance != null) AudioService.Instance.Play(cue);
    }
}

