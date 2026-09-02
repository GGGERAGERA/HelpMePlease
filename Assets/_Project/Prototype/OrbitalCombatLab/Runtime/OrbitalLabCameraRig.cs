using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalLabCameraRig : MonoBehaviour
    {
        public bool AutoCamera = true;
        public float BaseSize = 4.8f;
        public float RadiusMultiplier = .78f;
        public float EdgePadding = 1.25f;
        public float ManualSize = 9f;
        public float SmoothTime = .42f;
        public OrbitalCameraMode Mode = OrbitalCameraMode.CombatFocus;
        public float MaximumAutoCameraSize = 13f;
        [Range(.01f, .12f)] public float MinimumPlayerScreenSize = .028f;
        public float OuterRingMargin = 1.35f;
        public bool TemporaryFullStation => Input.GetKey(KeyCode.Tab);
        public float CurrentSize => cameraComponent != null ? cameraComponent.orthographicSize : 0f;
        public float ApproximateObjectScreenSize => cameraComponent != null
            ? .7f / Mathf.Max(.1f, cameraComponent.orthographicSize * 2f) : 0f;

        private Camera cameraComponent;
        private Vector3 positionVelocity;
        private float sizeVelocity;
        private float impulse;

        public void Configure(OrbitalCombatLabController lab, Camera existingCamera = null,
            bool createCamera = true)
        {
            if (existingCamera != null)
            {
                cameraComponent = existingCamera;
                return;
            }
            if (!createCamera) return;
            GameObject cameraObject = new("Orbital Lab Camera");
            cameraObject.tag = "MainCamera";
            cameraComponent = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 6f;
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(.008f, .012f, .022f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        public void Tick(Vector2 center, float outerRadius)
        {
            if (cameraComponent == null) return;
            float targetSize = AutoCamera ? CalculateSize(outerRadius, TemporaryFullStation) : ManualSize;
            cameraComponent.orthographicSize = Mathf.SmoothDamp(cameraComponent.orthographicSize,
                targetSize, ref sizeVelocity, SmoothTime, 100f, Time.unscaledDeltaTime);
            impulse = Mathf.MoveTowards(impulse, 0f, Time.unscaledDeltaTime * .42f);
            Vector2 shake = impulse > 0f ? Random.insideUnitCircle * impulse : Vector2.zero;
            Vector3 target = new(center.x + shake.x, center.y + shake.y, -10f);
            cameraComponent.transform.position = Vector3.SmoothDamp(cameraComponent.transform.position,
                target, ref positionVelocity, .14f, 100f, Time.unscaledDeltaTime);
        }

        public void Snap(Vector2 center, float outerRadius)
        {
            if (cameraComponent == null) return;
            cameraComponent.orthographicSize = AutoCamera ? CalculateSize(outerRadius, TemporaryFullStation) : ManualSize;
            cameraComponent.transform.position = new Vector3(center.x, center.y, -10f);
            sizeVelocity = 0f;
            positionVelocity = Vector3.zero;
        }

        public void AddImpulse(float amount) => impulse = Mathf.Max(impulse, amount);

        public void Zoom(float delta)
        {
            if (cameraComponent == null) return;
            float next = Mathf.Clamp(cameraComponent.orthographicSize - delta, 3f, 80f);
            cameraComponent.orthographicSize = next;
            ManualSize = next;
        }

        private float CalculateSize(float outerRadius, bool temporaryFull)
        {
            float aspect = cameraComponent != null ? Mathf.Max(1f, cameraComponent.aspect) : 1.777f;
            float full = Mathf.Max(BaseSize, outerRadius + OuterRingMargin);
            if (aspect < 1f) full /= aspect;
            if (temporaryFull || Mode == OrbitalCameraMode.FullStation) return full;
            float playerReadableLimit = .7f / Mathf.Max(.01f, MinimumPlayerScreenSize * 2f);
            return Mathf.Min(full, Mathf.Min(MaximumAutoCameraSize, playerReadableLimit));
        }
    }
}
