using UnityEngine;
using UnityEngine.Rendering;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public enum OrbitalVisualAction { GunFire, BladeHit, PusherPulse }

    internal sealed class OrbitalMiniWeaponVisual
    {
        private static readonly GameObject[] CachedPrefabs = new GameObject[3];
        private static readonly string[] ResourcePaths =
        {
            "OrbitalCombatLab/PistolVisual",
            "OrbitalCombatLab/LaserSwardVisual",
            "OrbitalCombatLab/ImpulsGunVisual"
        };

        private readonly OrbitalMountType type;
        private readonly Transform mount;
        private readonly OrbitalCombatLabController lab;
        private readonly Material unlitMaterial;
        private GameObject instance;
        private Transform muzzle;
        private Animator animator;
        private ParticleSystem[] particles;
        private Vector3[] particleScales;
        private bool[] particleScaleRoots;
        private SpriteRenderer[] sprites;
        private Color[] spriteColors;
        private int[] spriteOrders;
        private SortingGroup[] sortingGroups;
        private int[] groupOrders;
        private float particlesStopAt;
        private float animatorStopAt;
        private float flashUntil;
        private bool dragging;
        private bool dragValid;

        public bool HasInstance => instance != null;
        public int DisabledColliderCount { get; private set; }
        public int DisabledRuntimeBehaviourCount { get; private set; }
        public int ParticleCount => particles != null ? particles.Length : 0;
        public bool HasAnimator => animator != null;
        public bool IsEffectPlaying => particlesStopAt > 0f || animatorStopAt > 0f;
        public bool UsesLabUnlitMaterial
        {
            get
            {
                if (instance == null || sprites == null || sprites.Length == 0) return false;
                for (int i = 0; i < sprites.Length; i++)
                    if (sprites[i] != null && sprites[i].sharedMaterial != unlitMaterial) return false;
                return true;
            }
        }
        public bool AllProductionCollidersDisabled
        {
            get
            {
                if (instance == null) return false;
                Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null && colliders[i].enabled) return false;
                return true;
            }
        }
        public Vector2 MuzzlePosition => muzzle != null ? muzzle.position : mount.position;
        public Vector2 Forward => instance == null ? (Vector2)mount.right :
            type == OrbitalMountType.Blade ? (Vector2)instance.transform.up : (Vector2)instance.transform.right;

        public OrbitalMiniWeaponVisual(OrbitalMountType type, Transform mount,
            OrbitalCombatLabController lab, Material unlitMaterial)
        {
            this.type = type;
            this.mount = mount;
            this.lab = lab;
            this.unlitMaterial = unlitMaterial;
        }

        public void RefreshMode()
        {
            if (lab.WeaponVisuals.Mode == OrbitalWeaponVisualMode.MiniWeapons)
            {
                if (instance == null) CreateInstance();
            }
            else if (instance != null)
                DestroyInstance();
        }

        public void Tick()
        {
            RefreshMode();
            if (instance == null) return;
            Transform visual = instance.transform;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.Euler(0f, 0f, RotationOffset());
            float scale = VisualScale();
            if (Time.unscaledTime < flashUntil) scale *= 1.14f;
            visual.localScale = Vector3.one * scale;
            ApplySorting();
            ApplySpriteTint();

            if (particles != null)
            {
                float intensityScale = Mathf.Lerp(.5f, 1.15f,
                    Mathf.Clamp01(lab.WeaponVisuals.EffectIntensity));
                for (int i = 0; i < particles.Length; i++)
                {
                    if (particles[i] == null) continue;
                    // Visual scale makes the weapon readable, but must not inflate its
                    // nested muzzle/AOE particle hierarchy into permanent screen noise.
                    particles[i].transform.localScale = particleScaleRoots[i]
                        ? particleScales[i] * (intensityScale / Mathf.Max(.01f, scale))
                        : particleScales[i];
                }
                if (particlesStopAt > 0f && Time.unscaledTime >= particlesStopAt)
                {
                    StopParticles();
                    particlesStopAt = 0f;
                }
            }
            if (animator != null && animatorStopAt > 0f && Time.unscaledTime >= animatorStopAt)
            {
                animator.Play("PistolShoot1", 0, .99f);
                animator.Update(0f);
                animator.speed = 0f;
                animatorStopAt = 0f;
            }
        }

        public void Trigger(OrbitalVisualAction action)
        {
            if (instance == null || !lab.WeaponVisuals.EffectsEnabled ||
                lab.WeaponVisuals.EffectIntensity <= .01f) return;
            float now = Time.unscaledTime;
            float durationScale = Mathf.Lerp(.5f, 1f, Mathf.Clamp01(lab.WeaponVisuals.EffectIntensity));
            if (action == OrbitalVisualAction.GunFire && animator != null)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.Play("PistolShoot1", 0, 0f);
                animatorStopAt = now + .34f * durationScale;
            }
            if (action == OrbitalVisualAction.BladeHit)
            {
                flashUntil = now + .09f;
                return;
            }
            PlayParticles();
            particlesStopAt = now + (action == OrbitalVisualAction.PusherPulse ? .22f : .10f) * durationScale;
            flashUntil = now + (action == OrbitalVisualAction.PusherPulse ? .16f : .07f) * durationScale;
        }

        public void SetDragState(bool active, bool valid)
        {
            dragging = active;
            dragValid = valid;
            if (active) StopParticles();
        }

        public bool HitTest(Vector2 world, float padding)
        {
            if (instance == null || sprites == null) return false;
            Vector3 point = new(world.x, world.y, 0f);
            float paddingSqr = padding * padding;
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite != null && sprite.enabled && sprite.bounds.SqrDistance(point) <= paddingSqr)
                    return true;
            }
            return false;
        }

        public void Flash(float duration) =>
            flashUntil = Mathf.Max(flashUntil, Time.unscaledTime + duration);

        public void Destroy() => DestroyInstance();

        private void CreateInstance()
        {
            int index = (int)type;
            if (index < 0 || index >= CachedPrefabs.Length) return;
            if (CachedPrefabs[index] == null) CachedPrefabs[index] = Resources.Load<GameObject>(ResourcePaths[index]);
            GameObject prefab = CachedPrefabs[index];
            if (prefab == null)
            {
                Debug.LogWarning($"[OrbitalCombatLab] Mini weapon wrapper not found: {ResourcePaths[index]}");
                return;
            }
            instance = Object.Instantiate(prefab, mount, false);
            instance.name = type + " Mini Weapon Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);
            DisabledColliderCount = colliders.Length;
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            Rigidbody2D[] bodies = instance.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++) bodies[i].simulated = false;
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            DisabledRuntimeBehaviourCount = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                string fullName = behaviour.GetType().FullName ?? string.Empty;
                if (fullName.Contains("Rendering.Universal.Light2D")) continue;
                behaviour.enabled = false;
                DisabledRuntimeBehaviourCount++;
            }

            animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (type == OrbitalMountType.Gun)
                {
                    animator.enabled = true;
                    animator.speed = 0f;
                    animator.Play("PistolShoot1", 0, .99f);
                    animator.Update(0f);
                }
                else animator.enabled = false;
            }
            particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            particleScales = new Vector3[particles.Length];
            particleScaleRoots = new bool[particles.Length];
            for (int i = 0; i < particles.Length; i++)
            {
                particleScales[i] = particles[i].transform.localScale;
                particleScaleRoots[i] = !HasParticleAncestor(particles[i].transform);
                ParticleSystem.MainModule main = particles[i].main;
                main.loop = false;
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            sprites = instance.GetComponentsInChildren<SpriteRenderer>(true);
            spriteColors = new Color[sprites.Length];
            spriteOrders = new int[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                spriteColors[i] = sprites[i].color;
                spriteOrders[i] = sprites[i].sortingOrder;
                // Production mini-weapons use scene-light-dependent materials. The
                // isolated Lab has no production 2D light rig, so keep their sprites,
                // colors and hierarchy but render them through the Lab's unlit material.
                sprites[i].sharedMaterial = unlitMaterial;
            }
            sortingGroups = instance.GetComponentsInChildren<SortingGroup>(true);
            groupOrders = new int[sortingGroups.Length];
            for (int i = 0; i < sortingGroups.Length; i++) groupOrders[i] = sortingGroups[i].sortingOrder;
            muzzle = FindTransform("FirePoint1");
            if (muzzle == null && type == OrbitalMountType.Gun)
            {
                muzzle = new GameObject("Lab Muzzle Socket").transform;
                muzzle.SetParent(instance.transform, false);
                muzzle.localPosition = new Vector3(.38f, 0f, 0f);
            }
        }

        private Transform FindTransform(string targetName)
        {
            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i].name == targetName) return transforms[i];
            return null;
        }

        private static bool HasParticleAncestor(Transform child)
        {
            Transform current = child.parent;
            while (current != null)
            {
                if (current.GetComponent<ParticleSystem>() != null) return true;
                current = current.parent;
            }
            return false;
        }

        private void ApplySorting()
        {
            int offset = lab.WeaponVisuals.SortingOffset;
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i].sortingLayerID = 0;
                sprites[i].sortingOrder = offset + spriteOrders[i];
            }
            for (int i = 0; i < sortingGroups.Length; i++)
            {
                sortingGroups[i].sortingLayerID = 0;
                sortingGroups[i].sortingOrder = offset + groupOrders[i];
            }
        }

        private void ApplySpriteTint()
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                Color color = spriteColors[i];
                if (dragging) color = Color.Lerp(color,
                    dragValid ? new Color(.25f, 1f, .55f, color.a) : new Color(1f, .12f, .18f, color.a), .72f);
                else if (Time.unscaledTime < flashUntil) color = Color.Lerp(color, Color.white, .72f);
                sprites[i].color = color;
            }
        }

        private float VisualScale() => type == OrbitalMountType.Gun ? lab.WeaponVisuals.PistolScale :
            type == OrbitalMountType.Blade ? lab.WeaponVisuals.LaserSwardScale : lab.WeaponVisuals.ImpulsGunScale;

        private float RotationOffset() => type == OrbitalMountType.Gun ? lab.WeaponVisuals.PistolRotationOffset :
            type == OrbitalMountType.Blade ? lab.WeaponVisuals.LaserSwardRotationOffset :
            lab.WeaponVisuals.ImpulsGunRotationOffset;

        private void PlayParticles()
        {
            if (particles == null) return;
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }
        }

        private void StopParticles()
        {
            if (particles == null) return;
            for (int i = 0; i < particles.Length; i++)
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void DestroyInstance()
        {
            if (instance == null) return;
            StopParticles();
            instance.SetActive(false);
            Object.Destroy(instance);
            instance = null;
            muzzle = null;
            animator = null;
            particles = null;
            particleScales = null;
            particleScaleRoots = null;
            sprites = null;
            sortingGroups = null;
            DisabledColliderCount = DisabledRuntimeBehaviourCount = 0;
        }
    }
}
