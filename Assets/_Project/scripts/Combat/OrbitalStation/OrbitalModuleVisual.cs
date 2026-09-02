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
        private int[] spriteOrders;
        private SortingGroup[] sortingGroups;
        private int[] sortingGroupOrders;
        private float effectStopAt;
        private float flashUntil;
        private bool dragging;
        private bool dragValid;
        private Transform fallbackBody;

        public GameObject GameObject => root;
        public Transform Transform => root.transform;

        public OrbitalModuleVisual(OrbitalStationRuntime station,
            OrbitalModuleKind kind, string name, Color fallbackColor)
        {
            OrbitalPresentationConfig config = OrbitalPresentationConfig.Active;
            this.kind = kind;
            baseScale = config.GetScale(kind);
            root = new GameObject(name + " Visual Wrapper");
            root.transform.SetParent(station.RuntimeRoot, false);
            root.transform.localScale = Vector3.one;

            GameObject haloObject = station.CreateCircleVisual(name + " Selection Halo",
                new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b, 0.22f),
                Vector2.one * Mathf.Max(0.42f, baseScale * 0.44f), 12);
            haloObject.transform.SetParent(root.transform, false);
            halo = haloObject.GetComponent<SpriteRenderer>();
            halo.enabled = false;

            GameObject prefab = config.GetPrefab(kind);
            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, root.transform);
                instance.name = prefab.name + " [ORBITAL VISUAL ONLY]";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * baseScale;
                DisableCombatComponents(instance);
                CachePresentation(instance, station.VisualMaterial,
                    config.MountedWeaponSortingOffset);
            }
            else
            {
                CreateFallback(station, kind, fallbackColor, config);
            }
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

        public void Tick()
        {
            if (instance == null)
            {
                if (fallbackBody != null && kind == OrbitalModuleKind.LinkNode)
                    fallbackBody.localScale = Vector3.one * baseScale *
                        (1f + Mathf.Sin(Time.unscaledTime * 4.6f) * 0.12f);
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
            if (instance == null)
                return;
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

        private void CreateFallback(OrbitalStationRuntime station,
            OrbitalModuleKind kind, Color color, OrbitalPresentationConfig config)
        {
            if (kind == OrbitalModuleKind.ArcEmitter)
            {
                GameObject body = station.CreateCircleVisual("Arc Emitter", 
                    new Color(0.82f, 0.66f, 1f),
                    Vector2.one * baseScale, 14);
                body.transform.SetParent(root.transform, false);
                fallbackBody = body.transform;
                GameObject core = station.CreatePixelVisual("Arc Emitter Core", Color.white,
                    Vector2.one * baseScale * 0.34f, 15);
                core.transform.SetParent(root.transform, false);
            }
            else
            {
                GameObject body = station.CreateCircleVisual("Link Node", color,
                    Vector2.one * baseScale, 14);
                body.transform.SetParent(root.transform, false);
                fallbackBody = body.transform;
                GameObject core = station.CreateCircleVisual("Link Node Core",
                    new Color(1f, 0.72f, 1f), Vector2.one * baseScale * 0.46f, 15);
                core.transform.SetParent(root.transform, false);
            }
        }

        private void CachePresentation(GameObject target, Material material, int offset)
        {
            sprites = target.GetComponentsInChildren<SpriteRenderer>(true);
            spriteColors = new Color[sprites.Length];
            spriteOrders = new int[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                spriteColors[i] = sprites[i].color;
                spriteOrders[i] = sprites[i].sortingOrder;
                sprites[i].sharedMaterial = material;
                sprites[i].sortingLayerName = "Player";
                sprites[i].sortingOrder = offset + spriteOrders[i];
            }
            sortingGroups = target.GetComponentsInChildren<SortingGroup>(true);
            sortingGroupOrders = new int[sortingGroups.Length];
            for (int i = 0; i < sortingGroups.Length; i++)
            {
                sortingGroupOrders[i] = sortingGroups[i].sortingOrder;
                sortingGroups[i].sortingLayerName = "Player";
                sortingGroups[i].sortingOrder = offset + sortingGroupOrders[i];
            }
            ParticleSystemRenderer[] particles =
                target.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].sortingLayerName = "Player";
                particles[i].sortingOrder = offset + 2;
            }
            animator = target.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = kind == OrbitalModuleKind.Pistol;
                animator.speed = 0f;
                if (animator.enabled)
                {
                    animator.Play("PistolShoot1", 0, 0.99f);
                    animator.Update(0f);
                }
            }
            this.particles = target.GetComponentsInChildren<ParticleSystem>(true);
            StopParticles();
        }

        private static void DisableCombatComponents(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                string typeName = behaviours[i].GetType().FullName ?? string.Empty;
                if (!typeName.Contains("Rendering.Universal.Light2D"))
                    behaviours[i].enabled = false;
            }
            Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
            Rigidbody2D[] bodies = instance.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
                bodies[i].simulated = false;
            AudioSource[] audio = instance.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audio.Length; i++)
            {
                audio[i].Stop();
                audio[i].enabled = false;
            }
        }

        private void ApplyTint()
        {
            if (sprites == null || spriteColors == null)
                return;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color color = spriteColors[i];
                if (dragging)
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
