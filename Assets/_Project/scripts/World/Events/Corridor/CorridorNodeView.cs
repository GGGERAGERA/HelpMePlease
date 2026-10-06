using UnityEngine;

// Presentation references are authored in the prefab. Animation never creates geometry.
public sealed class CorridorNodeView : MonoBehaviour
{
    [SerializeField] private GameObject inactive, active, completed;
    [SerializeField] private Transform pulseVisual;
    [SerializeField] private float pulseAmount = .06f;
    private int state;
    private float phase, burst;
    private Vector3 restScale = Vector3.one;
    private void Awake() { if (pulseVisual != null) restScale = pulseVisual.localScale; }
    public void SetState(int value)
    {
        state = Mathf.Clamp(value, 0, 2);
        if (inactive != null) inactive.SetActive(state == 0);
        if (active != null) active.SetActive(state == 1);
        if (completed != null) completed.SetActive(state == 2);
    }
    public void Pulse() => burst = 1f;
    public void Tick(float delta, bool finishing = false)
    {
        phase += Mathf.Max(0, delta) * (finishing ? 11f : 5f);
        burst = Mathf.MoveTowards(burst, 0, delta * 2f);
        if (pulseVisual != null)
            pulseVisual.localScale = restScale * (1f + (state == 1 || finishing ? (Mathf.Sin(phase) + 1f) * .5f * pulseAmount : 0f) + burst * .12f);
    }
}
