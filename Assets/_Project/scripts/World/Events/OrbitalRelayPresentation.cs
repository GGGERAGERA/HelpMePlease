using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OrbitalRelayPresentation : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI phaseLabel, timeLabel, activationsLabel, goldLabel, comboLabel;
    [SerializeField] private Animator transitionFx, comboFx, urgentFx;
    [SerializeField] private Image flashOverlay;
    private OrbitalRelaySettings settings;
    private OrbitalRelayNode[] nodes;
    private int previousCombo;
    private OrbitalRelayPhase previousPhase;
    public bool IsValid => panel != null && phaseLabel != null && timeLabel != null &&
        activationsLabel != null && goldLabel != null && comboLabel != null && transitionFx != null &&
        comboFx != null && urgentFx != null && flashOverlay != null && transitionFx.runtimeAnimatorController != null;
    public void Bind(OrbitalRelaySettings settings, OrbitalRelayNode[] nodes)
    { this.settings = settings; this.nodes = nodes; previousCombo = 0; previousPhase = OrbitalRelayPhase.Inactive; }
    private static string L(string key) => LocalizationService.EnsureExists().Get(key);
    public void Render(OrbitalRelaySnapshot snapshot)
    {
        if (nodes == null) return;
        panel.SetActive(snapshot.Phase != OrbitalRelayPhase.Inactive);
        bool bonus = snapshot.Phase == OrbitalRelayPhase.Bonus;
        bool transition = snapshot.Phase == OrbitalRelayPhase.Transition;
        for (int i = 0; i < nodes.Length; i++) nodes[i].Apply(!transition && i == snapshot.ActiveNodeIndex, i == snapshot.ActiveNodeIndex ? snapshot.ContactProgress : 0);
        phaseLabel.text = L(transition ? "event.relay.stabilized" : bonus ? "event.relay.bonus" : "event.relay.stabilize");
        timeLabel.text = transition ? string.Empty : string.Format(L("event.relay.time"), snapshot.RemainingTime.ToString("0.0"));
        activationsLabel.text = bonus ? string.Format(L("event.relay.activations"), snapshot.BonusActivations)
            : snapshot.StabilizationActivations + " / " + settings.RequiredActivations;
        goldLabel.gameObject.SetActive(bonus);
        goldLabel.text = string.Format(L("event.relay.gold"), OrbitalRelayResult.Calculate(true, snapshot.BonusActivations, settings.GoldPerActivation).Gold);
        comboLabel.gameObject.SetActive(!transition);
        comboLabel.text = string.Format(L("event.relay.combo"), snapshot.Combo);
        if (snapshot.Combo > previousCombo) comboFx.Play("ComboPunch", 0, 0);
        if (snapshot.Phase != previousPhase) urgentFx.Rebind();
        bool urgent = !transition && snapshot.RemainingTime <= 5f;
        urgentFx.enabled = urgent;
        if (!urgent) { timeLabel.color = Color.white; timeLabel.transform.localScale = Vector3.one; }
        previousCombo = snapshot.Combo; previousPhase = snapshot.Phase;
    }
    public void PlayTransition()
    {
        transitionFx.speed = .75f / settings.TransitionDuration;
        transitionFx.Play("StabilizedTransition", 0, 0);
        foreach (var node in nodes) node.PlayActivation();
        Camera camera = Camera.main;
        if (camera != null && camera.gameObject.scene == gameObject.scene)
            camera.GetComponentInChildren<CameraShake>()?.Shake(.18f, .1f);
    }
    public void ShowResult(OrbitalRelayResult result)
    {
        RunMessageService.Instance?.ShowCustom(result.Success ? "event.relay.stabilized" : "event.relay.failed",
            string.Format(L("event.relay.activations"), result.BonusActivations) + "\n" + string.Format(L("event.relay.gold"), result.Gold) + "\n" +
            L(result.Success ? "event.relay.upgradeEarned" : "event.relay.upgradeNotEarned"), 4f);
    }
    public void Clear() { if (panel != null) panel.SetActive(false); if (flashOverlay != null) flashOverlay.color = Color.clear; }
}
