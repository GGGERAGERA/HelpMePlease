using System.Collections;
using UnityEngine;

namespace Subject42.Bunker.Gallery
{
    /// <summary>Owns visual-only clones and their short demonstrations. Never activates combat scripts.</summary>
    public sealed class EnemyGalleryPreviewController : MonoBehaviour
    {
        public enum DemoKind { Basic, Elite, Bomber, Shooter, Boss }

        [SerializeField] private Transform contentRoot;
        [SerializeField] private Transform effectsRoot;
        [SerializeField] private Transform stagingRoot;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private Vector2 modelSize = new Vector2(4.2f, 3.5f);

        private EnemyGalleryController.Entry entry;
        private GameObject avatar;
        private Animator animator;
        private Vector3 restPosition;
        private float elapsed;
        public GameObject CurrentPreview => avatar;
        public int CompletedDemoCycles { get; private set; }

        public void Show(EnemyGalleryController.Entry selection)
        {
            Clear();
            entry = selection;
            CompletedDemoCycles = 0;
            elapsed = 0f;
            StartCoroutine(Present());
        }

        public void Clear()
        {
            StopAllCoroutines();
            contentRoot.gameObject.SetActive(false);
            // Deactivate synchronously; Destroy is deferred until the end of the frame.
            foreach (var root in new[] { contentRoot, effectsRoot, stagingRoot })
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    var child = root.GetChild(i).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            avatar = null;
            animator = null;
            entry = null;
        }

        private void OnDisable() => Clear();

        private GameObject PrepareVisual(GameObject prefab)
        {
            // Staging is authored inactive. Awake/OnEnable/Start cannot run on the source scripts.
            var instance = Instantiate(prefab, stagingRoot, false);
            instance.name = prefab.name;
            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            // Dependents are appended after required components in the production prefabs.
            for (int i = behaviours.Length - 1; i >= 0; i--)
                Destroy(behaviours[i]);
            foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var source in instance.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = stagingRoot.gameObject.layer;
                child.gameObject.tag = "Untagged";
            }
            foreach (var a in instance.GetComponentsInChildren<Animator>(true))
            {
                a.fireEvents = false;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sharedMaterial = spriteMaterial;
            foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
            }
            return instance;
        }

        private IEnumerator Present()
        {
            avatar = PrepareVisual(entry.enemyPrefab);
            // Wait for removal while still inactive; no combat component ever gets an Awake.
            yield return null;
            avatar.transform.SetParent(contentRoot, false);
            avatar.transform.localPosition = Vector3.zero;
            avatar.SetActive(true);
            contentRoot.gameObject.SetActive(true);
            animator = avatar.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.name == "IsRunning" && parameter.type == AnimatorControllerParameterType.Bool)
                        animator.SetBool(parameter.nameHash, entry.demo != DemoKind.Boss);
                    if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float)
                        animator.SetFloat(parameter.nameHash, 0f);
                }
                animator.speed = entry.demo == DemoKind.Elite ? 1.25f : 1f;
                animator.Update(0f);
            }
            var bounds = VisibleBounds();
            float scale = Mathf.Min(modelSize.x / bounds.size.x, modelSize.y / bounds.size.y);
            avatar.transform.localScale *= scale;
            if (entry.demo == DemoKind.Shooter)
            {
                var facing = avatar.transform.localScale;
                facing.x = -Mathf.Abs(facing.x);
                avatar.transform.localScale = facing;
            }
            bounds = VisibleBounds();
            avatar.transform.position += contentRoot.position - bounds.center;
            if (entry.demo == DemoKind.Shooter) avatar.transform.position += Vector3.left * .55f;
            restPosition = avatar.transform.position;

            while (true)
            {
                yield return new WaitForSeconds(1.2f);
                switch (entry.demo)
                {
                    case DemoKind.Bomber:
                        yield return BomberDemo();
                        break;
                    case DemoKind.Shooter:
                        yield return Fly(entry.projectilePrefab, contentRoot.position + new Vector3(.8f, .15f),
                            contentRoot.position + new Vector3(3f, .15f), .45f);
                        break;
                    case DemoKind.Boss:
                        yield return BossDemo();
                        break;
                }
                CompletedDemoCycles++;
            }
        }

        private Bounds VisibleBounds()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (var renderer in avatar.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!renderer.enabled || renderer.sprite == null) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private void Update()
        {
            if (avatar == null || !avatar.activeInHierarchy || entry == null) return;
            elapsed += Time.deltaTime;
            if (entry.demo == DemoKind.Basic || entry.demo == DemoKind.Elite)
                avatar.transform.position = restPosition + Vector3.right *
                    (Mathf.Sin(elapsed * (entry.demo == DemoKind.Elite ? 2.4f : 1.6f)) * .3f);
        }

        private IEnumerator BomberDemo()
        {
            yield return Effect(entry.warningPrefab, contentRoot.position, 1f, 1.4f, true);
            avatar.SetActive(false);
            StartCoroutine(Effect(entry.explosionPrefab, contentRoot.position, 2.4f, 1.5f));
            if (entry.shockwavePrefab != null)
                StartCoroutine(Effect(entry.shockwavePrefab, contentRoot.position, .6f, 1f));
            yield return new WaitForSeconds(.35f);
            avatar.SetActive(true);
            yield return new WaitForSeconds(1f);
        }

        private IEnumerator BossDemo()
        {
            animator.SetTrigger("PAttack");
            yield return new WaitForSeconds(.8f);
            animator.SetTrigger("Attack");
            foreach (var particles in avatar.GetComponentsInChildren<ParticleSystem>()) particles.Play(false);
            for (int side = -1; side <= 1; side += 2)
            {
                var target = contentRoot.position + new Vector3(side * 1.5f, -1.2f);
                StartCoroutine(Effect(entry.warningPrefab, target, .5f, .65f, true));
                StartCoroutine(Fly(entry.projectilePrefab,
                    contentRoot.position + new Vector3(side * .7f, 1.5f), target, .65f));
            }
            yield return new WaitForSeconds(.7f);
            for (int side = -1; side <= 1; side += 2)
                StartCoroutine(Effect(entry.explosionPrefab,
                    contentRoot.position + new Vector3(side * 1.5f, -1.2f), .45f, 1f));
            yield return new WaitForSeconds(1.1f);
        }

        // These are presentation timelines, not projectiles: no EnemyProjectile, damage or targeting.
        private IEnumerator Fly(GameObject prefab, Vector3 from, Vector3 to, float duration)
        {
            var visual = PrepareVisual(prefab);
            yield return null;
            visual.transform.SetParent(effectsRoot, false);
            visual.transform.position = from;
            visual.transform.localScale *= .45f;
            visual.SetActive(true);
            foreach (var particles in visual.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startSpeed = 0f;
                particles.Play(false);
            }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                visual.transform.position = Vector3.Lerp(from, to, t / duration);
                yield return null;
            }
            visual.SetActive(false);
            Destroy(visual);
        }

        private IEnumerator Effect(GameObject prefab, Vector3 position, float scale, float duration, bool telegraph = false)
        {
            var visual = PrepareVisual(prefab);
            yield return null;
            visual.transform.SetParent(effectsRoot, false);
            visual.transform.position = position;
            visual.transform.localScale = Vector3.one * scale;
            visual.SetActive(true);
            foreach (var particles in visual.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = false;
                if (telegraph) main.simulationSpeed = main.duration / duration;
                particles.Play(false);
            }
            yield return new WaitForSeconds(duration);
            visual.SetActive(false);
            Destroy(visual);
        }
    }
}
