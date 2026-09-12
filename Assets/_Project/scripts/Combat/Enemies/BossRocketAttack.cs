using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyChaseMovement), typeof(EnemyHealth))]
public sealed class BossRocketAttack : MonoBehaviour
{
    public enum AttackState { Chasing, PreparingAttack, WaitingForImpact, Recovering }

    [Header("Cycle (seconds)")]
    [Min(0.1f)] public float AttackCooldown = 6f;
    [Min(0f)] public float PreAttackDuration = 0.75f;
    [Tooltip("Stationary pause before PAttack.")]
    [Min(0f)] public float StopDuration = 0.15f;
    [Min(0.01f)] public float RocketFallDelay = 1.2f;
    [Min(0.05f)] public float RocketFallDuration = 0.4f;
    [Min(0f)] public float RecoveryDuration = 0.55f;
    [Header("Rocket")]
    [Min(1f)] public float RocketSpawnHeight = 14f;
    [Min(0.1f)] public float ExplosionRadius = 2f;
    [Min(0)] public int ExplosionDamage = 25;
    [Min(0f)] public float TargetPrediction = 0f;
    [Min(0f)] public float MinAttackDistance = 0f;
    [Min(0f)] public float MaxAttackDistance = 30f;
    [Min(1)] public int RocketCount = 1;
    [Header("Existing assets")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform leftMuzzle;
    [SerializeField] private Transform rightMuzzle;
    [SerializeField] private GameObject rocketPrefab;
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private ParticleSystem explosionPrefab;

    public AttackState State { get; private set; }
    public int PendingRocketCount => shots.Count;
    private EnemyChaseMovement chase;
    private EnemyHealth health;
    private Rigidbody2D body;
    private PlayerHealth player;
    private Rigidbody2D playerBody;
    private float timer;
    private float shotTime;
    private bool preparing;
    private bool attackTriggered;
    private bool movementHeld;
    private bool resumeChase;
    private int handsLayer;
    private ParticleSystem[] muzzles;
    private readonly List<Shot> shots = new();
    private readonly List<GameObject> impactEffects = new();

    private sealed class Shot
    {
        public Vector3 target;
        public GameObject marker;
        public GameObject launch;
        public GameObject falling;
        public Vector3 launchStart;
        public Vector3 fallStart;
    }

    private void Awake()
    {
        chase = GetComponent<EnemyChaseMovement>();
        health = GetComponent<EnemyHealth>();
        body = GetComponent<Rigidbody2D>();
        handsLayer = animator != null ? animator.GetLayerIndex("Hands1") : -1;
        var particles = new List<ParticleSystem>();
        if (leftMuzzle != null) particles.AddRange(leftMuzzle.GetComponentsInChildren<ParticleSystem>(true));
        if (rightMuzzle != null) particles.AddRange(rightMuzzle.GetComponentsInChildren<ParticleSystem>(true));
        muzzles = particles.ToArray();
        StopMuzzles();
    }

    private void OnEnable()
    {
        health.OnDied += OnBossDied;
        State = AttackState.Chasing;
        timer = AttackCooldown;
    }

    private bool CombatAllowed()
    {
        if (health == null || health.IsDead || player == null ||
            !player.isActiveAndEnabled || player.IsDead) return false;
        if (RunStateManager.Instance != null && RunStateManager.Instance.IsRunEnded) return false;
        var flow = RunFlowController.Instance;
        return flow == null || (flow.isActiveAndEnabled &&
            flow.Phase != RunPhase.Stopped && flow.Phase != RunPhase.Victory &&
            flow.Phase != RunPhase.FinalBossIntro);
    }

    private void Update()
    {
        if (player == null && State == AttackState.Chasing)
        {
            var target = GameObject.FindGameObjectWithTag("Player");
            if (target != null)
            {
                player = target.GetComponent<PlayerHealth>();
                playerBody = target.GetComponent<Rigidbody2D>();
            }
        }
        if (!CombatAllowed())
        {
            CancelAttack();
            return;
        }
        if (Time.timeScale == 0f || EnemyDebugAiFreeze.IsFrozen || animator == null) return;
        impactEffects.RemoveAll(fx => fx == null);
        timer -= Time.deltaTime;
        switch (State)
        {
            case AttackState.Chasing:
                animator.SetFloat("Speed", chase.enabled ? body.linearVelocity.magnitude : 0f);
                float distance = Vector2.Distance(body.position, player.transform.position);
                if (timer <= 0f && chase.enabled && distance >= MinAttackDistance && distance <= MaxAttackDistance)
                    BeginAttack();
                break;
            case AttackState.PreparingAttack:
                if (!preparing && timer <= 0f)
                {
                    preparing = true;
                    animator.SetTrigger("PAttack");
                    timer = Mathf.Max(0f, PreAttackDuration);
                }
                else if (preparing && !attackTriggered && timer <= 0f &&
                    animator.GetCurrentAnimatorStateInfo(handsLayer).IsName("animBossPrepareToShootIDle"))
                {
                    attackTriggered = true;
                    animator.SetTrigger("Attack");
                }
                // A missing/interrupted event must never leave a stationary boss forever.
                if (timer < -5f) CancelAttack();
                break;
            case AttackState.WaitingForImpact:
                TickRockets();
                break;
            case AttackState.Recovering:
                if (timer <= 0f)
                {
                    RestoreMovement();
                    State = AttackState.Chasing;
                    timer = Mathf.Max(0.1f, AttackCooldown);
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (!movementHeld) return;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void BeginAttack()
    {
        if (animator == null || !animator.isActiveAndEnabled || handsLayer < 0 ||
            rocketPrefab == null || targetPrefab == null || explosionPrefab == null) return;
        resumeChase = chase.enabled;
        movementHeld = true;
        chase.enabled = false;
        body.linearVelocity = Vector2.zero;
        // Chase already faces the player; lock that facing through the attack.
        animator.SetFloat("Speed", 0f);
        animator.ResetTrigger("PAttack");
        animator.ResetTrigger("Attack");
        preparing = attackTriggered = false;
        State = AttackState.PreparingAttack;
        timer = Mathf.Max(0f, StopDuration);
    }

    // Called only by the authored Attack clip via BossRocketAnimationEvents.
    public void FireRocket()
    {
        if (!isActiveAndEnabled || State != AttackState.PreparingAttack ||
            !attackTriggered || !CombatAllowed()) return;
        State = AttackState.WaitingForImpact; // Consume the event before spawning anything.
        shotTime = 0f;
        Vector3 target = player.transform.position;
        if (playerBody != null) target += (Vector3)(playerBody.linearVelocity * Mathf.Max(0f, TargetPrediction));
        target.z = 0f;
        foreach (var muzzle in muzzles)
        {
            if (muzzle == null) continue;
            muzzle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzle.Play(false);
        }
        for (int i = 0; i < Mathf.Max(1, RocketCount); i++)
        {
            // For a salvo, spread fixed targets around the predicted center.
            Vector3 point = target;
            if (RocketCount > 1)
            {
                float angle = i * Mathf.PI * 2f / RocketCount;
                point += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * ExplosionRadius;
            }
            Transform muzzle = i % 2 == 0 ? leftMuzzle : rightMuzzle;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position;
            var shot = new Shot { target = point, launchStart = origin };
            shot.marker = ExplosionWarningVisual.Spawn(targetPrefab, point, ExplosionRadius,
                Mathf.Max(0.01f, RocketFallDelay) + Mathf.Max(0.05f, RocketFallDuration));
            foreach (var ps in shot.marker.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.startLifetime = main.duration + 0.1f;
                main.startSpeed = 0f;
                ps.Play(false);
            }
            shot.launch = SpawnRocket(origin, false);
            shots.Add(shot);
        }
    }

    private GameObject SpawnRocket(Vector3 position, bool falling)
    {
        var rocket = Instantiate(rocketPrefab, position, Quaternion.identity);
        var ps = rocket.GetComponent<ParticleSystem>();
        // The prefab is a particle sprite with world simulation. Attach its body to
        // our scripted transform, emit exactly one body, and keep the authored smoke.
        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0f;
        main.startLifetime = 30f;
        main.startRotation = falling ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f;
        var emission = ps.emission;
        emission.enabled = false;
        ps.Play(false);
        ps.Emit(1);
        return rocket;
    }

    private void TickRockets()
    {
        shotTime += Time.deltaTime;
        float delay = Mathf.Max(0.01f, RocketFallDelay);
        float fallDuration = Mathf.Max(0.05f, RocketFallDuration);
        float launchDuration = Mathf.Min(0.35f, delay);
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            Shot shot = shots[i];
            if (shot.launch != null)
            {
                shot.launch.transform.position = shot.launchStart + Vector3.up *
                    Mathf.Max(1f, RocketSpawnHeight) * Mathf.Clamp01(shotTime / launchDuration);
                if (shotTime >= launchDuration) Destroy(shot.launch);
            }
            if (shotTime < delay) continue;
            if (shot.falling == null)
            {
                float spawnY = shot.target.y + Mathf.Max(1f, RocketSpawnHeight);
                var camera = Camera.main;
                if (camera != null && camera.orthographic)
                    spawnY = Mathf.Max(spawnY, camera.transform.position.y + camera.orthographicSize + 2f);
                shot.fallStart = new Vector3(shot.target.x, spawnY, shot.target.z);
                shot.falling = SpawnRocket(shot.fallStart, true);
            }
            float progress = Mathf.Clamp01((shotTime - delay) / fallDuration);
            shot.falling.transform.position = Vector3.Lerp(shot.fallStart, shot.target, progress);
            if (progress < 1f) continue;
            shots.RemoveAt(i); // Remove before damage callbacks can cancel the ability.
            DestroyShot(shot);
            if (!CombatAllowed()) { CancelAttack(); return; }
            var fx = EnemyExplosion.Detonate(shot.target, ExplosionRadius, ExplosionDamage, explosionPrefab);
            if (fx != null) impactEffects.Add(fx.gameObject);
            if (!CombatAllowed()) { CancelAttack(); return; }
        }
        if (shots.Count != 0) return;
        State = AttackState.Recovering;
        timer = Mathf.Max(0f, RecoveryDuration);
    }

    private static void DestroyShot(Shot shot)
    {
        if (shot.marker != null) Destroy(shot.marker);
        if (shot.launch != null) Destroy(shot.launch);
        if (shot.falling != null) Destroy(shot.falling);
    }

    private void RestoreMovement()
    {
        if (movementHeld && chase != null && health != null && !health.IsDead)
            chase.enabled = resumeChase;
        movementHeld = false;
    }

    private void StopMuzzles()
    {
        if (muzzles == null) return;
        foreach (var ps in muzzles)
            if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void CancelAttack()
    {
        foreach (var shot in shots) DestroyShot(shot);
        shots.Clear();
        foreach (var fx in impactEffects) if (fx != null) Destroy(fx);
        impactEffects.Clear();
        if (State != AttackState.Chasing && animator != null)
        {
            animator.ResetTrigger("PAttack");
            animator.ResetTrigger("Attack");
            if (animator.isActiveAndEnabled && handsLayer >= 0)
                animator.Play("animHandsEmpty", handsLayer, 0f);
        }
        StopMuzzles();
        RestoreMovement();
        State = AttackState.Chasing;
        timer = Mathf.Max(0.1f, AttackCooldown);
    }

    private void OnBossDied(EnemyHealth _) => CancelAttack();
    private void OnDisable()
    {
        if (health != null) health.OnDied -= OnBossDied;
        CancelAttack();
    }
}
