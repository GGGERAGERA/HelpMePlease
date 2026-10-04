using UnityEngine;
using UnityEngine.UI;

public sealed class SurfaceMarkerPulse : MonoBehaviour
{
    public bool Pulsing { get; set; }
    private Graphic graphic;
    private void Awake() => graphic = GetComponent<Graphic>();
    private void Update()
    {
        float pulse = Pulsing ? .5f + .5f * Mathf.Sin(Time.unscaledTime * 3f) : 1f;
        transform.localScale = Vector3.one * (1f + (Pulsing ? .08f * pulse : 0f));
        if (graphic != null) graphic.canvasRenderer.SetAlpha(Pulsing ? .65f + .35f * pulse : 1f);
    }
}
