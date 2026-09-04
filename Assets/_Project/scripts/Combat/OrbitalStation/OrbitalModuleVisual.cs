using UnityEngine;
using UnityEngine.Rendering;

namespace Subject42.Combat.OrbitalStation
{
    internal sealed class OrbitalModuleVisual
    {
        private readonly GameObject root;
        private readonly SpriteRenderer halo;
        private readonly float baseScale;
        private readonly OrbitalModuleKind kind;
        private GameObject instance;
        private Animator animator;
        private ParticleSystem[] particles;
        private SpriteRenderer[] sprites;
        private Color[] spriteColors;
        private float effectStopAt;
        private float flashUntil;
        private bool dragging;
        private bool dragValid;
        private bool previewing;
        private bool previewSnapped;
        private Transform pulseBody;

        public GameObject GameObject => root;
        public Transform Transform => root.transform;

        public OrbitalModuleVisual(OrbitalStationRuntime station,
            OrbitalModuleKind kind, string name, Color fallbackColor)
        {
            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            this.kind = kind;
            baseScale = config.GetScale(kind);
            GameObject prefab = config.GetPrefab(kind);
            if (prefab == null) throw new System.InvalidOperationException($"required {kind} visual prefab missing");
            root = Object.Instantiate(prefab, station.RuntimeRoot, false);
            root.name = name + " Visual";
            var view = root.GetComponent<OrbitalModuleView>();
            if (view == null || !view.IsValid) throw new System.InvalidOperationException($"required {kind} visual references missing");
            halo = view.Halo;
            animator = view.Animator;
            particles = view.Particles;
            sprites = view.Sprites;
            spriteColors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;
            if (view.PulseBody != null) pulseBody = view.PulseBody;
            else instance = view.Body.gameObject;
            if (animator != null && kind == OrbitalModuleKind.Pistol)
            {
                animator.speed = 0f;
                animator.Play("PistolShoot1", 0, 0.99f);
                animator.Update(0f);
            }
            StopParticles();
        }

        public void SetHighlighted(bool value)
        {
            if (halo != null)
                halo.enabled = value;
        }

        public bool HitTest(Vector2 world, float padding)
        {
            if (sprites == null)
                return ((Vector2)root.transform.position - world).sqrMagnitude <=
                    Mathf.Pow(padding + baseScale * 0.5f, 2f);
            Vector3 point = new(world.x, world.y, 0f);
            float paddingSqr = padding * padding;
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null && sprites[i].enabled &&
                    sprites[i].bounds.SqrDistance(point) <= paddingSqr)
                    return true;
            return false;
        }

        public void SetDragState(bool active, bool valid)
        {
            dragging = active;
            dragValid = valid;
            if (active)
                StopParticles();
            ApplyTint();
        }

        public void SetPreviewState(bool snapped)
        {
            previewing = true;
            previewSnapped = snapped;
            StopParticles();
            if (halo != null)
                halo.enabled = snapped;
            ApplyTint();
        }

        public void SetWorldPosition(Vector2 position) =>
            root.transform.position = position;

        public void SetWorldRotation(float radians) =>
            root.transform.rotation = Quaternion.Euler(0f, 0f,
                radians * Mathf.Rad2Deg);

        public void Teardown()
        {
            if (root != null)
                Object.Destroy(root);
        }

        public void Tick()
        {
            if (instance == null)
            {
                if (pulseBody != null)
                {
                    float fallbackScale = Time.unscaledTime < flashUntil
                        ? 1.14f
                        : kind == OrbitalModuleKind.LinkNode
                            ? 1f + Mathf.Sin(Time.unscaledTime * 4.6f) * 0.12f
                            : 1f;
                    pulseBody.localScale = Vector3.one * baseScale * fallbackScale;
                }
                ApplyTint();
                return;
            }
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            float scale = baseScale * (Time.unscaledTime < flashUntil ? 1.14f : 1f);
            instance.transform.localScale = Vector3.one * scale;
            if (effectStopAt > 0f && Time.unscaledTime >= effectStopAt)
            {
                StopParticles();
                if (animator != null && kind == OrbitalModuleKind.Pistol)
                {
                    animator.Play("PistolShoot1", 0, 0.99f);
                    animator.Update(0f);
                    animator.speed = 0f;
                }
                effectStopAt = 0f;
            }
            ApplyTint();
        }

        public void Trigger()
        {
            flashUntil = Time.unscaledTime +
                (kind == OrbitalModuleKind.ImpulseGun ? 0.16f : 0.08f);
            if (animator != null && kind == OrbitalModuleKind.Pistol)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.Play("PistolShoot1", 0, 0f);
            }
            if (particles != null)
                for (int i = 0; i < particles.Length; i++)
                {
                    if (particles[i] == null) continue;
                    particles[i].Clear(true);
                    particles[i].Play(true);
                }
            effectStopAt = Time.unscaledTime +
                (kind == OrbitalModuleKind.ImpulseGun ? 0.22f : 0.12f);
        }

        private void ApplyTint()
        {
            if (sprites == null || spriteColors == null)
                return;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color color = spriteColors[i];
                if (previewing)
                {
                    if (previewSnapped)
                        color = Color.Lerp(color,
                            new Color(0.35f, 1f, 0.62f, color.a), 0.28f);
                    color.a *= previewSnapped ? 0.9f : 0.52f;
                }
                else if (dragging)
                    color = Color.Lerp(color, dragValid
                        ? new Color(0.25f, 1f, 0.55f, color.a)
                        : new Color(1f, 0.12f, 0.18f, color.a), 0.72f);
                else if (Time.unscaledTime < flashUntil)
                    color = Color.Lerp(color, Color.white, 0.72f);
                sprites[i].color = color;
            }
        }

        private void StopParticles()
        {
            if (particles == null)
                return;
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null)
                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
