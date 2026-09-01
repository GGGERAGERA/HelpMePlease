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

        private Camera cameraComponent;
        private Vector3 positionVelocity;
        private float sizeVelocity;
        private float impulse;

        public void Configure(OrbitalCombatLabController lab)
        {
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
            float targetSize = AutoCamera ? CalculateSize(outerRadius) : ManualSize;
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
            cameraComponent.orthographicSize = AutoCamera ? CalculateSize(outerRadius) : ManualSize;
            cameraComponent.transform.position = new Vector3(center.x, center.y, -10f);
            sizeVelocity = 0f;
            positionVelocity = Vector3.zero;
        }

        public void AddImpulse(float amount) => impulse = Mathf.Max(impulse, amount);

        private float CalculateSize(float outerRadius) =>
            Mathf.Max(BaseSize, BaseSize + outerRadius * RadiusMultiplier + EdgePadding);
    }
}
