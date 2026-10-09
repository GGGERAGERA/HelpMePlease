using UnityEngine;

// All geometry and renderers are authored in the transmitter prefab.
public sealed class FalseSignalPointView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body, indicator, stableResult, brokenResult;
    [SerializeField] private SpriteRenderer[] interference, pulses;
    [SerializeField] private Transform antenna;
    private readonly Color cyan = new(.45f, 1f, .95f);
    private readonly Color amber = new(1f, .65f, .22f);
    public bool IsValid => body != null && indicator != null && stableResult != null && brokenResult != null && antenna != null &&
        interference != null && interference.Length == 3 && pulses != null && pulses.Length == 3;
    public void Render(FalseSignalPointState state, bool real, float elapsed, float duration, Vector2 target)
    {
        bool result = state == FalseSignalPointState.Verified || state == FalseSignalPointState.Warning || state == FalseSignalPointState.Activated;
        stableResult.enabled = result && real;
        brokenResult.enabled = result && !real;
        float rhythm = .72f + Mathf.Sin(elapsed * 3) * .18f;
        indicator.color = state switch
        {
            FalseSignalPointState.Scanning => new Color(.9f, 1f, 1f, rhythm),
            FalseSignalPointState.Verified => real ? cyan : amber,
            FalseSignalPointState.Warning => new Color(1, .2f, .1f, Mathf.PingPong(elapsed * 8, 1)),
            FalseSignalPointState.Activated => Color.white,
            FalseSignalPointState.Disabled => new Color(.2f, .25f, .28f),
            _ => new Color(.8f, .85f, .95f, rhythm)
        };
        body.color = state == FalseSignalPointState.Disabled ? new Color(.4f, .45f, .5f) : Color.white;
        for (int i = 0; i < interference.Length; i++)
        {
            // Unknown devices all use the same rhythm; the answer only appears during diagnostics.
            bool broken = result && !real;
            interference[i].enabled = state != FalseSignalPointState.Disabled;
            interference[i].color = new Color(.7f, .82f, .9f, broken
                ? Mathf.PingPong(elapsed * (5 + i) + i * .7f, .55f)
                : .15f + Mathf.PingPong(elapsed * .5f + i * .2f, .2f));
        }
        Vector2 origin = antenna.position;
        Vector2 forward = (target - origin).normalized;
        Vector2 end = target + forward * 1.25f;
        Vector2 perpendicular = new(-forward.y, forward.x);
        float progress = elapsed / duration;
        for (int i = 0; i < pulses.Length; i++)
        {
            float t = progress * 1.25f - i * .12f;
            bool visible = state == FalseSignalPointState.Scanning && t >= 0 && t <= 1;
            if (!real && t > .66f) visible = false;
            pulses[i].enabled = visible;
            if (!visible) continue;
            float travel = real ? (t < .5f ? t * 2 : (1 - t) * 2) : Mathf.Clamp01(t * 1.65f);
            float jitter = !real && t > .22f ? Mathf.Sin(t * 70 + i * 2) * (t - .22f) * 1.7f : 0;
            pulses[i].transform.position = Vector2.Lerp(origin, end, travel) + perpendicular * jitter;
            pulses[i].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg + (real ? 0 : Mathf.Sin(t * 45) * 35));
            pulses[i].transform.localScale = real ? Vector3.one : new Vector3(1, 1 - travel * .75f, 1);
            pulses[i].color = real ? new Color(cyan.r, cyan.g, cyan.b, .95f) : new Color(amber.r, amber.g, amber.b, 1 - Mathf.Clamp01((t - .32f) / .34f));
        }
    }
}
