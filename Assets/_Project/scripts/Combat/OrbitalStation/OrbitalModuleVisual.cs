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
        private readonly Transform body;
        private bool chargedLine;
        private float chargedLineRange;
        private LineRenderer[] chargeLines;
        private static readonly Color ChargeBlue = new(.25f, .7f, 1.5f, 1f);
        private GameObject instance;
        private readonly Vector3 authoredBodyPosition;
        private readonly Quaternion authoredBodyRotation;
        private readonly Vector3 authoredBodyScale;
        private bool bodyFeedbackActive;
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
        private bool weaponFeedback;
        private bool preserveWeaponColors;
        private Color weaponTint = Color.white;
        private float weaponRecoil, weaponRecoilAt, weaponCharge;
        private bool highlighted;
        private float shotHaloUntil;
        private readonly Vector3 authoredHaloScale;
        private readonly Color authoredHaloColor;
        public void ConfigureWeaponFeedback(Color color, bool preserveColors = false)
        {
            weaponFeedback = true; weaponTint = color; preserveWeaponColors = preserveColors;
            if (animator != null) animator.enabled = false;
            foreach (var particle in particles)
            {
                var main = particle.main;
                main.startColor = color;
                var tint = particle.colorOverLifetime;
                tint.enabled = false;
            }
        }
        public void WeaponRecoil(float strength)
        {
            weaponRecoil = strength; weaponRecoilAt = Time.time; weaponCharge = 0f;
            shotHaloUntil = Time.time + OrbitalPresentationConfig.Active.ShotHaloDuration;
            Trigger();
            UpdateShotHalo();
        }
        public void WeaponCharge(float amount)
        {
            weaponCharge = Mathf.Clamp01(amount);
            if (chargedLine) UpdateChargeLines();
        }
        public void ConfigureChargedLine(bool enabled, float range)
        {
            bool wasCharged = chargedLine;
            chargedLine = enabled;
            chargedLineRange = range;
            if (enabled && chargeLines == null)
            {
                // One guide and three converging strokes, owned/reused by this existing visual.
                chargeLines = new LineRenderer[4];
                for (int i = 0; i < chargeLines.Length; i++)
                {
                    var line = Object.Instantiate(OrbitalPresentationConfig.Active.EnergyLinePrefab, root.transform, false);
                    line.name = i == 0 ? "Charged sniper direction" : "Charged sniper pulse " + i;
                    line.transform.localPosition = Vector3.zero;
                    line.transform.localRotation = Quaternion.identity;
                    line.transform.localScale = Vector3.one;
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.sortingLayerID = sprites[0].sortingLayerID;
                    int order = 0;
                    foreach (var sprite in sprites) if (sprite != null) order = Mathf.Max(order, sprite.sortingOrder);
                    line.sortingOrder = order + 1;
                    line.enabled = false;
                    chargeLines[i] = line;
                }
            }
            if (!enabled && wasCharged)
            {
                weaponCharge = 0f;
                body.localPosition = authoredBodyPosition;
                body.localRotation = authoredBodyRotation;
                body.localScale = authoredBodyScale;
            }
            UpdateChargeLines();
        }
        private void UpdateChargeLines()
        {
            if (chargeLines == null) return;
            bool visible = chargedLine && weaponCharge > 0f && !dragging && !previewing;
            float final = Mathf.InverseLerp(1f - .1f / .45f, 1f, weaponCharge);
            Vector3 muzzle = new(.42f, 0f, 0f);
            for (int i = 0; i < chargeLines.Length; i++)
            {
                var line = chargeLines[i];
                line.enabled = visible;
                if (!visible) continue;
                Color color = Color.Lerp(ChargeBlue, new Color(1.7f, 2f, 2.4f, 1f), final);
                if (i == 0)
                {
                    line.SetPosition(0, muzzle);
                    line.SetPosition(1, new Vector3(chargedLineRange, 0f, 0f));
                    line.widthMultiplier = .018f + .012f * final;
                    color.a = (.08f + weaponCharge * .12f + final * .12f) * DepthOpacity;
                }
                else
                {
                    float phase = Mathf.Repeat(Time.time * (5f + final * 3f) + i * .31f, 1f);
                    Vector3 origin = new(-.55f + i * .12f, (i - 2) * .38f, 0f);
                    line.SetPosition(0, Vector3.Lerp(origin, muzzle, phase));
                    line.SetPosition(1, Vector3.Lerp(origin, muzzle, Mathf.Min(1f, phase + .36f)));
                    line.widthMultiplier = .035f + .03f * weaponCharge + .02f * final;
                    color.a = (.25f + weaponCharge * .5f + final * .25f) * DepthOpacity;
                }
                line.startColor = line.endColor = color;
            }
        }
        public void ResetWeaponFeedback()
        {
            weaponRecoil = weaponCharge = 0f; flashUntil = effectStopAt = 0f; StopParticles();
            shotHaloUntil = 0f;
            UpdateChargeLines();
            UpdateShotHalo();
            if (chargedLine)
            {
                body.localPosition = authoredBodyPosition;
                body.localRotation = authoredBodyRotation;
                body.localScale = authoredBodyScale;
            }
            if (instance != null)
            {
                instance.transform.localPosition = authoredBodyPosition;
                instance.transform.localRotation = authoredBodyRotation;
                instance.transform.localScale = authoredBodyScale;
            }
            ApplyTint();
        }
        public float DepthOpacity { get; set; } = 1f;

        public GameObject GameObject => root;
        public Transform Transform => root.transform;

        // Rotation-independent envelope of the authored sprites, excluding selection halos.
        public float FramingReach
        {
            get
            {
                float reach = 0f;
                foreach (var sprite in sprites)
                {
                    if (sprite == null || sprite == halo || !sprite.enabled) continue;
                    Bounds b = sprite.localBounds;
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                        {
                            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(x, y, 0f));
                            reach = Mathf.Max(reach, Vector2.Distance(root.transform.position,
                                sprite.transform.TransformPoint(corner)));
                        }
                }
                return reach;
            }
        }

        public OrbitalModuleVisual(OrbitalStationRuntime station,
            OrbitalModuleKind kind, string name, Color fallbackColor, GameObject visualPrefab = null)
        {
            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            this.kind = kind;
            baseScale = config.GetScale(kind);
            GameObject prefab = visualPrefab != null ? visualPrefab : config.GetPrefab(kind);
            if (prefab == null) throw new System.InvalidOperationException($"required {kind} visual prefab missing");
            root = Object.Instantiate(prefab, station.RuntimeRoot, false);
            Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(root, prefab);
            root.name = name + " Visual";
            var view = root.GetComponent<OrbitalModuleView>();
            if (view == null || !view.IsValid) throw new System.InvalidOperationException($"required {kind} visual references missing");
            body = view.Body;
            halo = view.Halo;
            if (halo != null)
            {
                authoredHaloScale = halo.transform.localScale;
                authoredHaloColor = halo.color;
            }
            animator = view.Animator;
            particles = view.Particles;
            sprites = view.Sprites;
            spriteColors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;
            if (view.PulseBody != null) pulseBody = view.PulseBody;
            else instance = view.Body.gameObject;
            authoredBodyPosition = view.Body.localPosition;
            authoredBodyRotation = view.Body.localRotation;
            authoredBodyScale = view.Body.localScale;
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
            highlighted = value;
            UpdateShotHalo();
        }

        private void UpdateShotHalo()
        {
            if (halo == null) return;
            if (previewing || dragging)
            {
                halo.enabled = previewing ? previewSnapped : highlighted;
                halo.transform.localScale = authoredHaloScale;
                return;
            }
            var config = OrbitalPresentationConfig.Active;
            float pulse = config.ShotHaloDuration > 0f
                ? Mathf.Clamp01((shotHaloUntil - Time.time) / config.ShotHaloDuration) : 0f;
            bool charging = chargedLine && weaponCharge > 0f;
            float final = charging ? Mathf.InverseLerp(1f - .1f / .45f, 1f, weaponCharge) : 0f;
            halo.enabled = highlighted || pulse > 0f || charging;
            float chargeScale = charging ? 1.1f + weaponCharge * .35f + final * .2f : 1f;
            float haloScale = Mathf.Lerp(1f, config.ShotHaloScale, pulse);
            halo.transform.localScale = authoredHaloScale * (charging ? Mathf.Max(chargeScale, haloScale) : haloScale);
            Color color = pulse > 0f ? Color.Lerp(weaponTint, Color.white, .25f * pulse) : authoredHaloColor;
            if (charging) color = Color.Lerp(ChargeBlue, Color.white * 1.8f, final);
            color.a = Mathf.Max(authoredHaloColor.a, config.ShotHaloAlpha * pulse) * DepthOpacity;
            if (charging) color.a = Mathf.Max(color.a, (.15f + weaponCharge * .45f + final * .3f) * DepthOpacity);
            halo.color = color;
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

        public bool HasBodyContact(Vector2 center, float radius)
        {
            if (dragging || previewing || sprites == null) return false;
            foreach (SpriteRenderer sprite in sprites)
                if (sprite != halo && OrbitalBodyContactDetector.IntersectsCircle(sprite, center, radius))
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
            if (chargedLine)
            {
                float kick = weaponRecoil * Mathf.Clamp01(1f - (Time.time - weaponRecoilAt) / .12f);
                body.localPosition = authoredBodyPosition - Vector3.right * kick;
                body.localRotation = authoredBodyRotation;
                body.localScale = authoredBodyScale * (1f + weaponCharge * .08f);
                if (effectStopAt > 0f && Time.time >= effectStopAt) { StopParticles(); effectStopAt = 0f; }
                UpdateChargeLines();
                ApplyTint();
                return;
            }
            if (weaponFeedback && instance != null)
            {
                float kick = weaponRecoil * Mathf.Clamp01(1f - (Time.time - weaponRecoilAt) / .16f);
                instance.transform.localPosition = authoredBodyPosition - Vector3.right * kick;
                instance.transform.localRotation = authoredBodyRotation;
                instance.transform.localScale = authoredBodyScale * (1f + weaponCharge * .12f);
                if (effectStopAt > 0f && Time.time >= effectStopAt) { StopParticles(); effectStopAt = 0f; }
                ApplyTint(); return;
            }
            // Keep the blade's authored transforms fixed, including during hit feedback.
            if (kind == OrbitalModuleKind.Saw)
            {
                ApplyTint();
                return;
            }
            if (instance == null)
            {
                if (pulseBody != null)
                {
                    float fallbackScale = Time.time < flashUntil
                        ? 1.14f
                        : kind == OrbitalModuleKind.LinkNode
                            ? 1f + Mathf.Sin(Time.time * 4.6f) * 0.12f
                            : 1f;
                    pulseBody.localScale = Vector3.one * baseScale * fallbackScale;
                }
                ApplyTint();
                return;
            }
            float feedbackScale = Time.time < flashUntil ? 1.14f : 1f;
            if (kind == OrbitalModuleKind.Shotgun)
            {
                // Recompute feedback from the authored pose; never compound offsets.
                instance.transform.localPosition = authoredBodyPosition;
                instance.transform.localRotation = authoredBodyRotation;
                instance.transform.localScale = authoredBodyScale * feedbackScale;
            }
            else if (feedbackScale != 1f || bodyFeedbackActive)
            {
                // Gun/Arc pulse only scales the body; leave the shoot animation's pose alone.
                instance.transform.localScale = authoredBodyScale * feedbackScale;
                bodyFeedbackActive = feedbackScale != 1f;
            }
            if (effectStopAt > 0f && Time.time >= effectStopAt)
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
            flashUntil = Time.time +
                (kind == OrbitalModuleKind.Shotgun ? 0.16f : 0.08f);
            if (animator != null && kind == OrbitalModuleKind.Pistol
                && !weaponFeedback
                )
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
            effectStopAt = Time.time +
                (kind == OrbitalModuleKind.Shotgun ? 0.22f : 0.12f);
        }

        private void ApplyTint()
        {
            if (sprites == null || spriteColors == null)
                return;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color color = spriteColors[i];
                if (chargedLine && weaponCharge > 0f && sprites[i] != halo)
                {
                    float final = Mathf.InverseLerp(1f - .1f / .45f, 1f, weaponCharge);
                    color = Color.Lerp(color, Color.Lerp(ChargeBlue, Color.white * 1.8f, final), weaponCharge * .45f + final * .35f);
                }
                if (weaponFeedback && !preserveWeaponColors && sprites[i] != halo) color = Color.Lerp(color, Color.Lerp(weaponTint, Color.white, weaponCharge), .65f);
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
                else if (Time.time < flashUntil)
                    color = Color.Lerp(color, weaponFeedback
                        ? Color.Lerp(chargedLine ? ChargeBlue : weaponTint, Color.white, .25f)
                        : Color.white, 0.72f);
                color.a *= DepthOpacity;
                sprites[i].color = color;
            }
            // Compose after the regular sprite pass so it cannot erase shot tint,
            // and retain the existing snapped/invalid reward-preview visibility.
            UpdateShotHalo();
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
