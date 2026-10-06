using UnityEngine;

public sealed class OrbitalRelayPresentation : MonoBehaviour
{
    [SerializeField] private GameObject startZoneVisual, startPrompt, nodesRoot, presentationCanvas;
    [SerializeField] private OrbitalRelayArenaVisual arenaVisual;
    [SerializeField] private OrbitalRelayHud hud;
    [SerializeField] private OrbitalRelaySpawnFeedback[] spawnFeedback;
    private OrbitalRelaySettings settings;
    private OrbitalRelayNode[] nodes;
    private int previousNode = -1;
    private OrbitalRelayPhase previousPhase = OrbitalRelayPhase.Inactive;
    public bool IsValid => startZoneVisual != null && startPrompt != null && nodesRoot != null && presentationCanvas != null &&
        arenaVisual != null && arenaVisual.IsValid && hud != null && hud.IsValid && spawnFeedback != null && spawnFeedback.Length >= 2 &&
        System.Array.TrueForAll(spawnFeedback, feedback => feedback != null && feedback.IsValid);
    public void Bind(OrbitalRelaySettings settings, OrbitalRelayNode[] nodes)
    {
        Clear(); this.settings = settings; this.nodes = nodes;
        previousNode = -1; previousPhase = OrbitalRelayPhase.Inactive;
    }
    private static string L(string key) => LocalizationService.EnsureExists().Get(key);
    public void ShowStartPrompt(bool visible) => startPrompt.SetActive(visible);
    public void Render(OrbitalRelaySnapshot snapshot)
    {
        if (nodes == null) return;
        bool inactive = snapshot.Phase == OrbitalRelayPhase.Inactive;
        bool active = snapshot.Phase == OrbitalRelayPhase.Stabilization || snapshot.Phase == OrbitalRelayPhase.Transition || snapshot.Phase == OrbitalRelayPhase.Bonus;
        if (!active && !inactive) { Clear(); return; }
        startZoneVisual.SetActive(inactive); nodesRoot.SetActive(active);
        presentationCanvas.SetActive(active); arenaVisual.Show(active);
        if (!active) return;
        bool transition = snapshot.Phase == OrbitalRelayPhase.Transition;
        bool started = previousPhase == OrbitalRelayPhase.Inactive;
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].Apply(!transition && i == snapshot.ActiveNodeIndex, i == snapshot.ActiveNodeIndex ? snapshot.ContactProgress : 0);
            if (i < spawnFeedback.Length && (started || !transition && i == snapshot.ActiveNodeIndex &&
                (i != previousNode || previousPhase == OrbitalRelayPhase.Transition))) spawnFeedback[i].Play();
        }
        hud.Render(snapshot, settings);
        previousNode = snapshot.ActiveNodeIndex; previousPhase = snapshot.Phase;
    }
    public void PlayTransition()
    {
        // Local node flashes and HUD wipe; no full-screen flash or long transition tween.
        foreach (var feedback in spawnFeedback) feedback.Play();
    }
    public void ShowResult(OrbitalRelayResult result)
    {
        Clear();
        RunMessageService.Instance?.ShowCustom(result.Success ? "event.relay.stabilized" : "event.relay.failed",
            string.Format(L("event.relay.activations"), result.BonusActivations) + "\n" + string.Format(L("event.relay.gold"), result.Gold) + "\n" +
            L(result.Success ? "event.relay.upgradeEarned" : "event.relay.upgradeNotEarned"), 4f);
    }
    public void Clear()
    {
        hud?.Clear();
        if (presentationCanvas != null) presentationCanvas.SetActive(false);
        if (arenaVisual != null) arenaVisual.Show(false);
        if (startPrompt != null) startPrompt.SetActive(false);
        if (startZoneVisual != null) startZoneVisual.SetActive(false);
        if (nodesRoot != null) nodesRoot.SetActive(false);
        if (spawnFeedback != null) foreach (var feedback in spawnFeedback) feedback?.Clear();
        previousNode = -1; previousPhase = OrbitalRelayPhase.Inactive;
    }
    private void OnDisable() => Clear();
}
