using TMPro;
using UnityEngine;

// Authored start area matches the gameplay circle. Only light and flow animate.
public sealed class CorridorStartView : MonoBehaviour
{
    [SerializeField] private Transform graphics;
    [SerializeField] private SpriteRenderer area;
    [SerializeField] private SpriteRenderer[] beacons;
    [SerializeField] private Transform[] flow;
    [SerializeField] private TMP_Text prompt;
    private bool started;
    private float age;
    public bool IsValid => graphics != null && area != null && prompt != null && beacons.Length == 2 && flow.Length == 3;
    public void Configure(Vector2 direction)
    {
        graphics.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        prompt.text = LocalizationService.EnsureExists().Get("event.corridor.start");
        started = false; age = 0;
    }
    public void Begin() { started = true; age = 0; prompt.gameObject.SetActive(false); }
    public void Tick(float delta)
    {
        age += delta;
        float strength = started ? Mathf.Clamp01(1 - age / .6f) : .78f + Mathf.Sin(age * 2.3f) * .12f;
        area.color = new Color(1, 1, 1, strength);
        foreach (var beacon in beacons) beacon.color = new Color(1, 1, 1, started ? Mathf.Lerp(.5f, 1, strength) : 1);
        for (int i = 0; i < flow.Length; i++)
        {
            flow[i].gameObject.SetActive(!started || age < .6f);
            flow[i].localPosition = new Vector3(-1.65f + Mathf.Repeat(age * (started ? 6 : 1.2f) + i * 1.1f, 3.3f), 0, 0);
        }
    }
    public void Clear() => gameObject.SetActive(false);
}
