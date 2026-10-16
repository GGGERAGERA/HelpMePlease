using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    // Shared by production and labs. Movement owns the origin, limit and input lifecycle.
    [DisallowMultipleComponent]
    public sealed class OrbitalCenterRangeView : MonoBehaviour
    {
        [SerializeField] private OrbitalCenterShift centerShift;
        [SerializeField] private LineRenderer line;
        [SerializeField] private bool showRange = true;
        [SerializeField, Min(.001f)] private float thickness = .025f;
        [SerializeField] private Color color = new(.5f, .8f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float opacity = .4f;
        [SerializeField, Min(.01f)] private float fadeSeconds = .18f;
        private float visibility;

        private void LateUpdate()
        {
            // Interruptions hide immediately; only a normal pointer release fades.
            if (!showRange || !centerShift.CanControl)
            {
                Hide();
                return;
            }
            visibility = Mathf.MoveTowards(visibility, centerShift.IsPointerSteering ? 1f : 0f,
                Time.unscaledDeltaTime / fadeSeconds);
            line.enabled = visibility > 0f;
            if (!line.enabled) return;
            line.widthMultiplier = thickness;
            Color tint = color; tint.a *= opacity * visibility;
            line.startColor = line.endColor = tint;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, centerShift.RangeCenter +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * centerShift.MaximumOffset);
            }
        }

        private void Hide() { visibility = 0f; line.enabled = false; }
        private void OnDisable() => Hide();
    }
}
