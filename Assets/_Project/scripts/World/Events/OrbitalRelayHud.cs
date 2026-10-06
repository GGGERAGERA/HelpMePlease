using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hierarchy, sprites and labels are authored in the production prefab.
public sealed class OrbitalRelayHud : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI phase, timeCaption, timeValue, activationsCaption, activationsValue,
        goldCaption, goldValue, comboCaption, comboValue, goldPopup;
    [SerializeField] private GameObject goldRow, comboRow;
    [SerializeField] private Image border, phaseStrip, transitionWipe;
    [Header("Production UI palette")]
    [SerializeField] private Color stabilizationTint = new(.239216f, .552941f, 1f, 1);
    [SerializeField] private Color bonusTint = new(.08f, .82f, .86f, 1);
    [SerializeField] private Color alertTint = new(.94902f, .662745f, .231373f, 1);
    [SerializeField] private Color valueTint = new(.88f, .96f, 1f, 1);
    [Header("Stepped feedback")]
    [SerializeField, Range(.08f, .3f)] private float valueFlashDuration = .18f;
    [SerializeField, Range(.08f, .3f)] private float transitionFlashDuration = .24f;
    [SerializeField, Range(.2f, .6f)] private float urgentPeriod = .4f;
    [SerializeField, Range(1f, 1.2f)] private float valuePunch = 1.12f;
    private OrbitalRelayPhase previousPhase = OrbitalRelayPhase.Inactive;
    private int previousActivations, previousGold, previousCombo;
    private float activationFlash, goldFlash, comboFlash, transitionFlash, urgentElapsed;
    private bool urgent, bonus;
    public bool IsValid => phase != null && timeCaption != null && timeValue != null && activationsCaption != null &&
        activationsValue != null && goldCaption != null && goldValue != null && comboCaption != null && comboValue != null &&
        goldPopup != null && border != null && phaseStrip != null && transitionWipe != null && goldRow != null && comboRow != null;
    private static string L(string key) => LocalizationService.EnsureExists().Get(key);
    private static string Caption(string key)
    {
        string format = L(key); int at = format.IndexOf("{0}", System.StringComparison.Ordinal);
        return (at < 0 ? format : format.Substring(0, at)).TrimEnd(' ', ':', 'x', '×');
    }
    public void Render(OrbitalRelaySnapshot snapshot, OrbitalRelaySettings settings)
    {
        bonus = snapshot.Phase == OrbitalRelayPhase.Bonus;
        bool transition = snapshot.Phase == OrbitalRelayPhase.Transition;
        bool changedPhase = previousPhase != snapshot.Phase;
        int activations = bonus ? snapshot.BonusActivations : snapshot.StabilizationActivations;
        int gold = OrbitalRelayResult.Calculate(true, snapshot.BonusActivations, settings.GoldPerActivation).Gold;
        if (activations > previousActivations && (!changedPhase || transition)) activationFlash = valueFlashDuration;
        if (gold > previousGold) { goldFlash = valueFlashDuration; goldPopup.text = "+" + (gold - previousGold); }
        if (snapshot.Combo > previousCombo) comboFlash = valueFlashDuration;
        if (changedPhase) { transitionFlash = transitionFlashDuration; urgentElapsed = 0; }
        phase.text = L(transition ? "event.relay.stabilized" : bonus ? "event.relay.bonus" : "event.relay.stabilize");
        timeCaption.text = Caption("event.relay.time"); timeValue.text = transition ? "--.-" : snapshot.RemainingTime.ToString("00.0");
        activationsCaption.text = Caption("event.relay.activations");
        activationsValue.text = bonus ? activations.ToString("00") : activations + " / " + settings.RequiredActivations;
        goldCaption.text = Caption("event.relay.gold"); goldValue.text = gold.ToString("00");
        comboCaption.text = Caption("event.relay.combo"); comboValue.text = "x" + snapshot.Combo;
        goldRow.SetActive(bonus); comboRow.SetActive(!transition);
        urgent = !transition && snapshot.RemainingTime <= 5;
        if (!urgent) urgentElapsed = 0;
        previousPhase = snapshot.Phase; previousActivations = activations; previousGold = gold; previousCombo = snapshot.Combo;
        DrawFeedback();
    }
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        activationFlash = Mathf.Max(0, activationFlash - dt); goldFlash = Mathf.Max(0, goldFlash - dt);
        comboFlash = Mathf.Max(0, comboFlash - dt); transitionFlash = Mathf.Max(0, transitionFlash - dt);
        if (urgent) urgentElapsed += dt;
        DrawFeedback();
    }
    private void DrawFeedback()
    {
        Color accent = bonus ? bonusTint : stabilizationTint;
        bool brightAlert = urgent && urgentElapsed % urgentPeriod < urgentPeriod * .5f;
        border.color = brightAlert ? alertTint : transitionFlash > 0 ? valueTint : accent;
        phase.color = accent; phaseStrip.color = accent;
        timeValue.color = urgent ? (brightAlert ? alertTint : valueTint) : valueTint;
        Flash(activationsValue, activationFlash); Flash(goldValue, goldFlash); Flash(comboValue, comboFlash);
        goldPopup.gameObject.SetActive(goldFlash > 0 && bonus); goldPopup.color = bonusTint;
        transitionWipe.enabled = transitionFlash > 0;
        if (transitionWipe.enabled)
        {
            float step = Mathf.Ceil(transitionFlash / transitionFlashDuration * 3) / 3;
            transitionWipe.rectTransform.localScale = new Vector3(step, 1, 1);
            Color color = accent; color.a = .16f; transitionWipe.color = color;
        }
    }
    private void Flash(TMP_Text value, float remaining)
    {
        value.color = remaining > 0 ? Color.white : valueTint;
        float scale = remaining > valueFlashDuration * .66f ? valuePunch : remaining > 0 ? 1.04f : 1;
        value.transform.localScale = Vector3.one * scale;
    }
    public void Clear()
    {
        previousPhase = OrbitalRelayPhase.Inactive; previousActivations = previousGold = previousCombo = 0;
        activationFlash = goldFlash = comboFlash = transitionFlash = urgentElapsed = 0; urgent = bonus = false;
        if (!IsValid) return;
        DrawFeedback(); transitionWipe.enabled = false; goldPopup.gameObject.SetActive(false);
    }
    private void OnDisable() => Clear();
}
