using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Optional scene-local lab interception. Release gameplay has no hook or dependency on lab types.
    public interface IOrbitalCombatLabInterceptor
    {
        bool HandlesContact(OrbitalModuleRuntime module);
        void TickContacts(float deltaTime);
        bool TryResolveDamage(EnemyHealth target, float damage, Vector2 point, out bool applied);
        bool InterceptProjectile(EnemyHealth target, Vector2 from, Vector2 to, float damage, Vector2 incomingDirection);
    }
#endif
    public interface IOrbitalOwnerAdapter
    {
        Transform Transform { get; }
        GameObject DamageOwner { get; }
        Vector2 MoveDirection { get; }
        bool CanControl { get; }
        bool IsDead { get; }
    }

    public interface IOrbitalCombatAdapter
    {
        event Action<EnemyHealth, float> Hit;
        event Action<EnemyHealth> Death;
        EnemyHealth FindNearest(Vector2 origin, float range);
        List<EnemyHealth> FindNearestMany(Vector2 origin, float range, int count);
        bool ApplyDamage(EnemyHealth enemy, float damage, Vector2 hitPoint);
        void SpawnProjectile(Vector2 origin, EnemyHealth target, float speed,
            float damage, Color color);
        void Tick(float deltaTime);
        void Teardown();
    }

    public sealed class ProductionOrbitalOwnerAdapter : IOrbitalOwnerAdapter
    {
        private readonly GameObject player;
        private readonly CharacterMovement2D movement;
        private readonly PlayerHealth health;
        private readonly Rigidbody2D body;

        public ProductionOrbitalOwnerAdapter(GameObject owner)
        {
            player = owner;
            movement = owner != null ? owner.GetComponent<CharacterMovement2D>() : null;
            health = owner != null ? owner.GetComponent<PlayerHealth>() : null;
            body = owner != null ? owner.GetComponent<Rigidbody2D>() : null;
        }

        public Transform Transform => player != null ? player.transform : null;
        public GameObject DamageOwner => player;
        public bool IsDead => player == null || (health != null && health.IsDead);
        public bool CanControl => !IsDead && (movement == null || movement.enabled);
        public Vector2 MoveDirection
        {
            get
            {
                if (body != null && body.linearVelocity.sqrMagnitude > 0.01f)
                    return body.linearVelocity.normalized;
                return movement != null ? movement.LastMoveDirection : Vector2.right;
            }
        }
    }

    public sealed class ProductionOrbitalCombatAdapter : IOrbitalCombatAdapter
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static IOrbitalCombatLabInterceptor LabInterceptor { get; set; }
        public Func<OrbitalStationRuntime, OrbitalModuleState, OrbitalModuleRuntime> LabModuleFactory;
        public event Action<long, int, Vector2> LabProjectileSpawned;
        public event Action<long, float, bool, bool> LabProjectileContact;
        public event Action<long, bool> LabProjectileEnded;
        private long labProjectileSequence;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLabInterceptor() => LabInterceptor = null;
#endif
        public bool LabStraightProjectiles { get; set; } = true;
        public sealed class ProjectileVolley
        {
            public readonly long Id;
            internal readonly HashSet<EnemyHealth> Shields = new();
            internal readonly HashSet<EnemyHealth> Pushed = new();
            public ProjectileVolley(long id) => Id = id;
        }
        public void SpawnWeaponProjectile(Vector2 origin, EnemyHealth target, Vector2 direction,
            OrbitalWeaponSettings settings, ProjectileVolley volley, float damage)
            => SpawnWeaponProjectileCore(origin, target, direction, settings, volley, damage, null);

        public void SpawnDirectionalWeaponProjectile(Vector2 origin, Vector2 direction,
            OrbitalWeaponSettings settings, ProjectileVolley volley, float damage, int ricochets = 0)
        {
            var shot = SpawnWeaponProjectileCore(origin, null, direction, settings, volley, damage, direction);
            shot.RicochetsRemaining = ricochets;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SpawnLabDirectionalProjectile(Vector2 origin, Vector2 direction,
            OrbitalWeaponSettings settings, ProjectileVolley volley, float damage, int weaponId = 0, int ricochets = 0)
        {
            var shot = SpawnWeaponProjectileCore(origin, null, direction, settings, volley, damage, direction);
            if (shot == null) return;
            shot.RicochetsRemaining = ricochets;
            if (weaponId == 0) return;
            shot.LabSequence = ++labProjectileSequence;
            LabProjectileSpawned?.Invoke(shot.LabSequence, weaponId, shot.Direction);
        }
#endif

        private Projectile SpawnWeaponProjectileCore(Vector2 origin, EnemyHealth target, Vector2 direction,
            OrbitalWeaponSettings settings, ProjectileVolley volley, float damage, Vector2? labDirection)
        {
            Projectile shot = SpawnProjectileCore(origin, target, settings.ProjectileSpeed, damage,
                settings.ProjectileVisual, labDirection);
            if (shot == null) return null;
            shot.Straight = true; shot.Target = null; shot.Position = origin; shot.Direction = direction.normalized;
            shot.RemainingRange = settings.Range; shot.Radius = settings.ProjectileRadius * settings.ProjectileVisualScale;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            shot.MissReturnPending = settings.MissReturnExperiment && settings.Kind == OrbitalModuleKind.Pistol;
            shot.MissReturnRange = shot.MissReturnPending ? settings.Range : 0f;
#endif
            shot.BodiesRemaining = Mathf.Max(1, settings.BodyPierces); shot.Volley = volley;
            shot.Knockback = settings.Knockback;
            shot.Kind = settings.Kind;
            shot.Presentation = settings.Presentation;
            shot.PresentationScale = settings.ProjectileVisualScale;
            shot.BypassShields = settings.BypassShields;
            shot.GameObject.transform.localScale = shot.AuthoredScale * settings.ProjectileVisualScale;
            if (settings.Presentation != null)
            {
                var style = settings.Presentation;
                shot.BodySize = style.BodySize * settings.ProjectileVisualScale;
                shot.Color = style.Color;
                foreach (var sprite in shot.Sprites)
                {
                    Vector2 size = sprite.sprite.bounds.size;
                    sprite.transform.localScale = new Vector3(style.BodySize.x / size.x, style.BodySize.y / size.y, 1f);
                    sprite.color = style.Color;
                }
                foreach (var trail in shot.Trails)
                {
                    trail.time = style.TrailSeconds;
                    trail.widthMultiplier = style.TrailWidth * settings.ProjectileVisualScale;
                    Color head = style.Color; head.a *= style.TrailOpacity;
                    Color tail = head; tail.a = 0f;
                    trail.startColor = head; trail.endColor = tail;
                }
            }
            if (settings.Presentation == null && settings.ProjectileTrailSeconds >= 0f)
                foreach (var trail in shot.Trails) trail.time = settings.ProjectileTrailSeconds;
            shot.GameObject.transform.right = shot.Direction;
            return shot;
        }
        // Pistol targeting reaches 8 m; the legacy 0.2 m body visual is scaled by 1.4 in this lab.
        private const float LabProjectileRange = 8f;
        private const float LabProjectileRadius = .14f;
        private readonly List<RaycastHit2D> projectileHits = new();
        private ContactFilter2D projectileFilter = new() { useTriggers = true };
        public void DebugClearProjectiles()
        {
            foreach (var projectile in projectiles) if (projectile.Active)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                projectile.LabCancelled = true;
#endif
                Release(projectile);
            }
        }
        public int LabActiveProjectileCount
        {
            get { int count = 0; foreach (var projectile in projectiles) if (projectile.Active) count++; return count; }
        }
        public int LabPooledProjectileCount => projectiles.Count;
        public Action<Vector2, Vector2, bool> LabBodyImpact;
        public void PrewarmLabProjectiles(GameObject prefab, int count)
        {
            if (prefab == null) return;
            int existing = 0;
            foreach (var projectile in projectiles) if (projectile.SourcePrefab == prefab) existing++;
            // Profiles can share a visual preset; prewarming must not duplicate that pool.
            for (int i = existing; i < count; i++) CreateProjectile(prefab);
        }
        public void ConfigureLabProjectileVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null) throw new ArgumentNullException(nameof(visualPrefab));
            foreach (var projectile in projectiles)
                UnityEngine.Object.Destroy(projectile.GameObject);
            projectiles.Clear();
            projectilePrefab = visualPrefab;
            for (int i = 0; i < OrbitalPresentationConfig.Active.ProjectilePrewarmCount; i++) CreateProjectile();
        }
        private sealed class Projectile
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public long LabSequence;
            public bool LabCancelled;
            public bool MissReturnPending;
            public float MissReturnRange;
#endif
            public int RicochetsRemaining;
            public GameObject GameObject;
            public EnemyHealth Target;
            public float Speed;
            public float Damage;
            public bool Active;
            public ParticleSystem[] Particles;
            public TrailRenderer[] Trails;
            public SpriteRenderer[] Sprites;
            public Vector3[] AuthoredSpriteScales;
            public Color[] AuthoredSpriteColors;
            public float[] AuthoredTrailWidths;
            public Gradient[] AuthoredTrailColors;
            public Vector2 BodySize;
            public Color Color;
            public OrbitalModuleKind Kind;
            public OrbitalWeaponSettings.ProjectileStyle Presentation;
            public float PresentationScale;
            public float[] AuthoredTrailTimes;
            public bool Straight;
            public bool BypassShields;
            public Vector3 AuthoredScale;
            public Vector2 Position, Direction;
            public float RemainingRange;
            public float Radius, Knockback;
            public int BodiesRemaining;
            public ProjectileVolley Volley;
            public readonly HashSet<EnemyHealth> HitBodies = new();
            public GameObject SourcePrefab;
        }

        private readonly Transform root;
        private GameObject projectilePrefab;
        private readonly List<Projectile> projectiles = new();
        // Borrowed by the synchronous Arc attack until the next query on this adapter.
        private readonly List<EnemyHealth> nearestTargets = new();
        private readonly System.Comparison<EnemyHealth> nearestComparison;
        private Vector2 nearestOrigin;

        public event Action<EnemyHealth, float> Hit;
        public event Action<EnemyHealth> Death;
        public Action<Vector2, Vector2, Color> ProjectileImpact;
        // Typed visual routing keeps independent weapon families; existing impact observers remain intact.
        public Action<Vector2, Vector2, Color, OrbitalModuleKind> ProjectileVisualImpact;

        public ProductionOrbitalCombatAdapter(Transform runtimeRoot, Sprite sharedSprite)
        {
            root = runtimeRoot;
            projectilePrefab = OrbitalPresentationConfig.Active.PistolProjectilePrefab;
            nearestComparison = CompareNearestTargets;
            PrewarmLabProjectiles(OrbitalPresentationConfig.Active.WeaponProjectilePrefab,
                OrbitalPresentationConfig.Active.ProjectilePrewarmCount);
        }

        private Projectile CreateProjectile(GameObject source = null)
        {
            GameObject sourcePrefab = source != null ? source : projectilePrefab;
            GameObject gameObject = UnityEngine.Object.Instantiate(sourcePrefab, root, false);
            Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(gameObject, sourcePrefab);
            // Hits remain owned by this adapter, not the authored prefab's trigger.
            foreach (var collider in gameObject.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;
            var projectile = new Projectile
            {
                GameObject = gameObject,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                MissReturnPending = false, MissReturnRange = 0f,
#endif
            };
            projectile.Particles = gameObject.GetComponentsInChildren<ParticleSystem>(true);
            projectile.Trails = gameObject.GetComponentsInChildren<TrailRenderer>(true);
            projectile.Sprites = gameObject.GetComponentsInChildren<SpriteRenderer>(true);
            projectile.AuthoredSpriteScales = new Vector3[projectile.Sprites.Length];
            projectile.AuthoredSpriteColors = new Color[projectile.Sprites.Length];
            for (int i = 0; i < projectile.Sprites.Length; i++)
            {
                projectile.AuthoredSpriteScales[i] = projectile.Sprites[i].transform.localScale;
                projectile.AuthoredSpriteColors[i] = projectile.Sprites[i].color;
            }
            projectile.AuthoredTrailTimes = new float[projectile.Trails.Length];
            projectile.AuthoredTrailWidths = new float[projectile.Trails.Length];
            projectile.AuthoredTrailColors = new Gradient[projectile.Trails.Length];
            for (int i = 0; i < projectile.Trails.Length; i++)
            {
                projectile.AuthoredTrailTimes[i] = projectile.Trails[i].time;
                projectile.AuthoredTrailWidths[i] = projectile.Trails[i].widthMultiplier;
                projectile.AuthoredTrailColors[i] = projectile.Trails[i].colorGradient;
            }
            projectile.SourcePrefab = source != null ? source : projectilePrefab;
            projectile.AuthoredScale = gameObject.transform.localScale;
            gameObject.SetActive(false);
            projectiles.Add(projectile);
            return projectile;
        }

        public EnemyHealth FindNearest(Vector2 origin, float range)
        {
            EnemyHealth best = null;
            float bestDistance = range * range;
            var enemies = EnemyHealth.GetActiveEnumerator();
            while (enemies.MoveNext())
            {
                EnemyHealth enemy = enemies.Current;
                if (!IsValid(enemy))
                    continue;
                float distance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = enemy;
            }
            return best;
        }

        public List<EnemyHealth> FindNearestMany(Vector2 origin, float range, int count)
        {
            var result = nearestTargets;
            result.Clear();
            nearestOrigin = origin;
            var enemies = EnemyHealth.GetActiveEnumerator();
            while (enemies.MoveNext())
            {
                EnemyHealth enemy = enemies.Current;
                if (IsValid(enemy) && Vector2.Distance(origin, enemy.transform.position) <= range)
                    result.Add(enemy);
            }
            result.Sort(nearestComparison);
            if (result.Count > count)
                result.RemoveRange(count, result.Count - count);
            return result;
        }

        private int CompareNearestTargets(EnemyHealth a, EnemyHealth b) =>
            ((Vector2)a.transform.position - nearestOrigin).sqrMagnitude.CompareTo(
                ((Vector2)b.transform.position - nearestOrigin).sqrMagnitude);

        public bool ApplyDamage(EnemyHealth enemy, float damage, Vector2 hitPoint) => ResolveDamage(enemy, damage, hitPoint, false);

        private bool ResolveDamage(EnemyHealth enemy, float damage, Vector2 hitPoint, bool bodyHit)
        {
            if (!IsValid(enemy))
                return false;
            bool wasAlive = !enemy.IsDead;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (LabInterceptor != null && LabInterceptor.TryResolveDamage(enemy, damage, hitPoint, out bool applied))
            {
                if (applied) { Hit?.Invoke(enemy, damage); if (enemy != null && enemy.IsDead) Death?.Invoke(enemy); }
                return applied;
            }
#endif
            if (!bodyHit && enemy.TryGetComponent<EnemyDirectionalShield>(out var shield) && shield.BlockBeam(hitPoint, enemy.transform.position))
                return false;
            enemy.TakeDamage(Mathf.Max(0f, damage), hitPoint);
            Hit?.Invoke(enemy, damage);
            if (wasAlive && enemy != null && enemy.IsDead)
                Death?.Invoke(enemy);
            return true;
        }

        public void SpawnProjectile(Vector2 origin, EnemyHealth target, float speed,
            float damage, Color color)
            => SpawnProjectileCore(origin, target, speed, damage, null);

        private Projectile SpawnProjectileCore(Vector2 origin, EnemyHealth target, float speed,
            float damage, GameObject source, Vector2? labDirection = null)
        {
            if (!labDirection.HasValue && !IsValid(target))
                return null;
            Vector2 initialDirection = labDirection ?? ((Vector2)target.transform.position - origin);
            Projectile projectile = null;
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (!projectiles[i].Active
                    && projectiles[i].SourcePrefab == (source != null ? source : projectilePrefab)
                    )
                {
                    projectile = projectiles[i];
                    break;
                }
            }
            if (projectile == null)
            {
                projectile = CreateProjectile(source);
            }
            projectile.Active = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            projectile.LabSequence = 0; projectile.LabCancelled = false;
            projectile.MissReturnPending = false; projectile.MissReturnRange = 0f;
#endif
            projectile.RicochetsRemaining = 0;
            projectile.Target = target;
            projectile.Speed = Mathf.Max(1f, speed);
            projectile.Damage = damage;
            projectile.GameObject.transform.position = origin;
            projectile.Straight = LabStraightProjectiles;
            projectile.Radius = LabProjectileRadius; projectile.BodiesRemaining = 1;
            projectile.Knockback = 0f; projectile.Volley = null; projectile.HitBodies.Clear();
            projectile.BypassShields = false;
            projectile.BodySize = Vector2.zero;
            projectile.Color = Color.white;
            projectile.Kind = OrbitalModuleKind.Pistol;
            projectile.Presentation = null;
            projectile.PresentationScale = 1f;
            projectile.GameObject.transform.localScale = projectile.AuthoredScale;
            for (int i = 0; i < projectile.Sprites.Length; i++)
            {
                projectile.Sprites[i].transform.localScale = projectile.AuthoredSpriteScales[i];
                projectile.Sprites[i].color = projectile.AuthoredSpriteColors[i];
            }
            for (int i = 0; i < projectile.Trails.Length; i++)
            {
                projectile.Trails[i].time = projectile.AuthoredTrailTimes[i];
                projectile.Trails[i].widthMultiplier = projectile.AuthoredTrailWidths[i];
                projectile.Trails[i].colorGradient = projectile.AuthoredTrailColors[i];
            }
            if (projectile.Straight)
            {
                projectile.Position = origin;
                projectile.Direction = initialDirection.normalized;
                projectile.RemainingRange = LabProjectileRange;
                projectile.Target = null; // A fired shot no longer owns or follows its aim target.
            }
            if (projectile.Particles.Length > 0 || projectile.Trails.Length > 0)
            {
                projectile.GameObject.transform.right = initialDirection;
                foreach (var trail in projectile.Trails) trail.Clear();
                foreach (var particles in projectile.Particles) particles.Clear(true);
            }
            projectile.GameObject.SetActive(true);
            foreach (var particles in projectile.Particles) particles.Play(true);
            return projectile;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LabInterceptor?.TickContacts(deltaTime);
#endif
            Physics2D.SyncTransforms();
            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];
                if (!projectile.Active)
                    continue;
                RefreshProjectilePresentation(projectile);
                if (projectile.Straight)
                {
                    TickStraightProjectile(projectile, deltaTime);
                    continue;
                }
                if (!IsValid(projectile.Target))
                {
                    Release(projectile);
                    continue;
                }
                Vector2 position = projectile.GameObject.transform.position;
                Vector2 target = projectile.Target.transform.position;
                Vector2 next = Vector2.MoveTowards(position, target,
                    projectile.Speed * deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (LabInterceptor != null && LabInterceptor.InterceptProjectile(projectile.Target, position, next, projectile.Damage, next - position))
                { Release(projectile); continue; }
#endif
                projectile.GameObject.transform.position = next;
                Vector2 direction = target - position;
                if (direction.sqrMagnitude > 0.001f)
                    projectile.GameObject.transform.right = direction;
                if ((next - target).sqrMagnitude > 0.025f)
                    continue;
                ApplyDamage(projectile.Target, projectile.Damage, next);
                Release(projectile);
            }
        }

        public void Teardown()
        {
            Hit = null;
            Death = null;
            ProjectileImpact = null;
            ProjectileVisualImpact = null;
            for (int i = 0; i < projectiles.Count; i++)
                if (projectiles[i].GameObject != null)
                    UnityEngine.Object.Destroy(projectiles[i].GameObject);
            projectiles.Clear();
        }

        private void TickStraightProjectile(Projectile projectile, float deltaTime)
        {
            // Stored world position prevents station/player translation from dragging an airborne shot.
            Vector2 from = projectile.Position;
            float distance = Mathf.Min(projectile.Speed * deltaTime, projectile.RemainingRange);
            projectileHits.Clear();
            if (projectile.BodySize != Vector2.zero)
                Physics2D.CapsuleCast(from, projectile.BodySize, CapsuleDirection2D.Horizontal,
                    Mathf.Atan2(projectile.Direction.y, projectile.Direction.x) * Mathf.Rad2Deg,
                    projectile.Direction, projectileFilter, projectileHits, distance);
            else
                Physics2D.CircleCast(from, projectile.Radius, projectile.Direction,
                    projectileFilter, projectileHits, distance);
            // Process the complete swept segment in travel order; piercing cannot skip a plate.
            projectileHits.Sort((a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in projectileHits)
            {
                var enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (!IsValid(enemy) || enemy.gameObject.scene != root.gameObject.scene) continue;
                Vector2 point = hit.point;
                if (enemy.TryGetComponent<EnemyDirectionalShield>(out var shield) && hit.collider == shield.ShieldCollider)
                {
                    // Explicit sniper bypass ignores plates. A plate is never a body.
                    if (projectile.BypassShields) continue;
                    point = hit.collider.ClosestPoint(point);
                    bool consume = projectile.Volley == null || !projectile.Volley.Shields.Contains(enemy);
                    if (!shield.BlockProjectile(projectile.Direction, point, consume)) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (projectile.LabSequence != 0) LabProjectileContact?.Invoke(projectile.LabSequence, 0f, false, true);
#endif
                    projectile.Volley?.Shields.Add(enemy);
                    Release(projectile); return; // Includes the hit that breaks the shield.
                }
                if (!projectile.HitBodies.Add(enemy)) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // A body contact commits to the ordinary damage/pierce/RIC pipeline, even if damage is rejected.
                projectile.MissReturnPending = false;
                projectile.MissReturnRange = 0f;
                float healthBefore = enemy.CurrentHealth;
                bool aliveBefore = !enemy.IsDead;
#endif
                ResolveDamage(enemy, projectile.Damage, point, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (projectile.LabSequence != 0)
                    LabProjectileContact?.Invoke(projectile.LabSequence, Mathf.Clamp(healthBefore - enemy.CurrentHealth, 0f, Mathf.Max(0f, healthBefore)), aliveBefore && enemy.IsDead, false);
#endif
                ProjectileImpact?.Invoke(point, projectile.Direction, projectile.Color);
                ProjectileVisualImpact?.Invoke(point, projectile.Direction, projectile.Color, projectile.Kind);
                LabBodyImpact?.Invoke(point, projectile.Direction, projectile.BodiesRemaining > 1);
                if (projectile.Knockback > 0f && !enemy.IsDead && enemy.TryGetComponent<EnemyChaseMovement>(out var movement) &&
                    (projectile.Volley == null || projectile.Volley.Pushed.Add(enemy)))
                    movement.ApplyKnockback(projectile.Direction, Mathf.Clamp(projectile.Knockback, 0f, 5f));
                --projectile.BodiesRemaining;
                if (projectile.RicochetsRemaining > 0)
                {
                    EnemyHealth nextTarget = null;
                    float nearest = float.PositiveInfinity;
                    foreach (var candidate in FindNearestMany(point, 5f, int.MaxValue))
                    {
                        if (projectile.HitBodies.Contains(candidate) || candidate.gameObject.scene != root.gameObject.scene) continue;
                        float sqr = ((Vector2)candidate.transform.position - point).sqrMagnitude;
                        if (sqr >= nearest || sqr < .0001f) continue;
                        nearest = sqr; nextTarget = candidate;
                    }
                    if (nextTarget != null)
                    {
                        projectile.RicochetsRemaining--;
                        projectile.BodiesRemaining = Mathf.Max(1, projectile.BodiesRemaining);
                        projectile.Direction = ((Vector2)nextTarget.transform.position - point).normalized;
                        projectile.Position = point + projectile.Direction * .03f;
                        projectile.RemainingRange = Mathf.Max(projectile.RemainingRange - hit.distance, Mathf.Sqrt(nearest) + .5f);
                        projectile.GameObject.transform.position = projectile.Position;
                        projectile.GameObject.transform.right = projectile.Direction;
                        return; // Recast the new direction next frame; never reuse the old sweep hits.
                    }
                }
                if (projectile.BodiesRemaining <= 0) { Release(projectile); return; }
            }
            projectile.Position = from + projectile.Direction * distance;
            projectile.GameObject.transform.position = projectile.Position;
            projectile.GameObject.transform.right = projectile.Direction;
            projectile.RemainingRange -= distance;
            if (projectile.RemainingRange <= 0f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (projectile.MissReturnPending)
                {
                    projectile.MissReturnPending = false;
                    projectile.RemainingRange = projectile.MissReturnRange;
                    projectile.MissReturnRange = 0f;
                    projectile.Direction = -projectile.Direction;
                    projectile.GameObject.transform.right = projectile.Direction;
                    // Keep world Position and the continuous trail. The return leg gets a fresh sweep next Tick;
                    // a large deltaTime cannot reuse outward hits or skip the reverse collision check.
                    return;
                }
#endif
                Release(projectile);
            }
        }

        private static bool IsValid(EnemyHealth enemy) =>
            enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead;

        private static void RefreshProjectilePresentation(Projectile projectile)
        {
            var style = projectile.Presentation;
            if (style == null) return;
            // BodySize, casts and travel data are captured at spawn. Live edits own color and trails only.
            projectile.Color = style.Color;
            foreach (var sprite in projectile.Sprites) sprite.color = style.Color;
            foreach (var trail in projectile.Trails)
            {
                trail.time = style.TrailSeconds;
                trail.widthMultiplier = style.TrailWidth * projectile.PresentationScale;
                Color head = style.Color; head.a *= style.TrailOpacity;
                Color tail = head; tail.a = 0f;
                trail.startColor = head; trail.endColor = tail;
            }
        }

        private void Release(Projectile projectile)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (projectile.LabSequence != 0) LabProjectileEnded?.Invoke(projectile.LabSequence, projectile.LabCancelled);
            projectile.LabSequence = 0; projectile.LabCancelled = false;
            projectile.MissReturnPending = false; projectile.MissReturnRange = 0f;
#endif
            projectile.Active = false;
            projectile.Target = null;
            projectile.Volley = null; projectile.HitBodies.Clear();
            foreach (var trail in projectile.Trails) trail.Clear();
            foreach (var particles in projectile.Particles)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (projectile.GameObject != null)
                projectile.GameObject.SetActive(false);
        }
    }
}
