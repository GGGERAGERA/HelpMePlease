using UnityEngine;
using UnityEngine.Rendering;

namespace Subject42.Prototype.OrbitalCombatLab
{
    /// <summary>
    /// A visual-only adapter for production actor prefabs. The Lab owns movement,
    /// health and combat; the nested production prefab only supplies art and animation.
    /// </summary>
    public sealed class OrbitalActorVisual
    {
        public const string PlayerResourcePath = "OrbitalCombatLab/PlayerVisual";
        public const string ZombieResourcePath = "OrbitalCombatLab/ZombieVisual";

        public bool IsAvailable => instance != null && spriteRenderers.Length > 0;
        public int SpriteCount => spriteRenderers.Length;
        public int AnimatorCount => animators.Length;
        public bool UsesLitSpriteMaterial { get; private set; }
        public int DisabledProductionBehaviours { get; private set; }
        public int DisabledProductionColliders { get; private set; }
        public bool ProductionPhysicsDisabled { get; private set; }

        private readonly GameObject instance;
        private readonly SpriteRenderer[] spriteRenderers = System.Array.Empty<SpriteRenderer>();
        private readonly Color[] sourceColors = System.Array.Empty<Color>();
        private readonly Animator[] animators = System.Array.Empty<Animator>();
        private readonly bool[] hasSpeedParameter = System.Array.Empty<bool>();
        private readonly bool[] hasRunParameter = System.Array.Empty<bool>();
        private readonly float baseScale;
        private float facingSign = 1f;
        private bool motionInitialized;
        private bool lastMoving;

        private OrbitalActorVisual(GameObject prefab, Transform parent, float scale, int sortingOrder)
        {
            baseScale = Mathf.Max(.01f, scale);
            if (prefab == null) return;

            instance = Object.Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * baseScale;

            DisableProductionRuntime();

            spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            sourceColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];
                sourceColors[i] = renderer.color;
                renderer.sortingLayerID = 0;
                renderer.sortingOrder += sortingOrder;
                Material material = renderer.sharedMaterial;
                if (material != null && material.shader != null &&
                    material.shader.name.IndexOf("Sprite-Lit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    UsesLitSpriteMaterial = true;
            }

            SortingGroup[] groups = instance.GetComponentsInChildren<SortingGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].sortingLayerID = 0;
                groups[i].sortingOrder += sortingOrder;
            }

            animators = instance.GetComponentsInChildren<Animator>(true);
            hasSpeedParameter = new bool[animators.Length];
            hasRunParameter = new bool[animators.Length];
            for (int i = 0; i < animators.Length; i++)
            {
                hasSpeedParameter[i] = HasParameter(animators[i], "Speed",
                    AnimatorControllerParameterType.Float);
                hasRunParameter[i] = HasParameter(animators[i], "IsRunning",
                    AnimatorControllerParameterType.Bool);
            }
            instance.SetActive(true);
        }

        public static OrbitalActorVisual CreatePlayer(Transform parent) =>
            new(Resources.Load<GameObject>(PlayerResourcePath), parent, .45f, 15);

        public static OrbitalActorVisual CreateZombie(Transform parent) =>
            new(Resources.Load<GameObject>(ZombieResourcePath), parent, 1f, 8);

        public void SetMotion(Vector2 direction, bool moving)
        {
            if (!IsAvailable) return;
            if (Mathf.Abs(direction.x) > .05f)
                facingSign = -Mathf.Sign(direction.x);

            instance.transform.localScale = new Vector3(
                baseScale * facingSign,
                baseScale,
                baseScale);

            if (motionInitialized && lastMoving == moving) return;
            motionInitialized = true;
            lastMoving = moving;
            for (int i = 0; i < animators.Length; i++)
            {
                if (hasSpeedParameter[i]) animators[i].SetFloat("Speed", moving ? 1f : 0f);
                if (hasRunParameter[i]) animators[i].SetBool("IsRunning", moving);
            }
        }

        public void SetAppearance(float alpha, bool hitFlash)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                Color color = sourceColors[i];
                if (hitFlash)
                    color = Color.Lerp(color, new Color(1f, .28f, .18f, color.a), .82f);
                color.a = sourceColors[i].a * Mathf.Clamp01(alpha);
                spriteRenderers[i].color = color;
            }

            float pulse = hitFlash ? 1.12f : 1f;
            instance.transform.localScale = new Vector3(
                baseScale * facingSign * pulse,
                baseScale * pulse,
                baseScale);
        }

        private void DisableProductionRuntime()
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;
                behaviour.enabled = false;
                DisabledProductionBehaviours++;
            }

            Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                DisabledProductionColliders++;
            }

            Rigidbody2D[] bodies = instance.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].linearVelocity = Vector2.zero;
                bodies[i].angularVelocity = 0f;
                bodies[i].simulated = false;
            }
            ProductionPhysicsDisabled = bodies.Length == 0 || AllBodiesDisabled(bodies);

            AudioSource[] audioSources = instance.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                audioSources[i].Stop();
                audioSources[i].enabled = false;
            }

            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer[] particleRenderers =
                instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < particleRenderers.Length; i++)
                particleRenderers[i].enabled = false;

            TrailRenderer[] trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++) trails[i].enabled = false;
            LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++) lines[i].enabled = false;
        }

        private static bool AllBodiesDisabled(Rigidbody2D[] bodies)
        {
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i].simulated) return false;
            return true;
        }

        private static bool HasParameter(
            Animator animator,
            string name,
            AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == type && parameters[i].name == name) return true;
            }
            return false;
        }
    }
}
