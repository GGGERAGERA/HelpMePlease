using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyChaseMovement), typeof(EnemyHealth))]
public sealed class BossRocketAttack : MonoBehaviour
{
    public enum AttackState { Chasing, PreparingAttack, Firing, BetweenShots, Recovering }

    [Header("Cycle (seconds)")]
    [Min(0.1f)] public float AttackCooldown = 6f;
    [Range(1, 5)] public int ShotsPerBurst = 1;
    [Min(0f)] public float DelayBetweenShots = 0.15f;
    [Min(0f)] public float PreAttackDuration = 0.75f;
    [Tooltip("Stationary pause before PAttack.")]
    [Min(0f)] public float StopDuration = 0.15f;
    [Header("Independent rocket flight (seconds)")]
    [Min(0.01f)] public float RocketFallDelayMin = 0.4f;
    [Min(0.01f)] public float RocketFallDelayMax = 2f;
    [Min(0.05f)] public float RocketFallDuration = 0.4f;
    [Min(0f)] public float RecoveryDuration = 0.55f;
    [Header("Rocket")]
    [Min(1f)] public float RocketSpawnHeight = 14f;
    [Min(0.1f)] public float ExplosionRadius = 2f;
    [Min(0)] public int ExplosionDamage = 25;
    [Min(0f)] public float TargetPrediction = 0f;
    [Min(0f)] public float MinAttackDistance = 0f;
    [Min(0f)] public float MaxAttackDistance = 30f;
    [Min(0.01f)] public float TargetSpreadRadius = 1.5f;
    [Header("Existing assets")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform leftMuzzle;
    [SerializeField] private Transform rightMuzzle;
    [SerializeField] private GameObject rocketPrefab;
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private ParticleSystem explosionPrefab;

    public AttackState State { get; private set; }
    public int PendingRocketCount => rocketRunner?.PendingCount ?? 0;
    private EnemyChaseMovement chase;
    private EnemyHealth health;
    private PlayerHealth player;
    private Rigidbody2D playerBody;
    private float timer;
    private bool preparing;
    private bool attackTriggered;
    private int remainingShots;
    public bool IsBurstActive => State == AttackState.PreparingAttack ||
        State == AttackState.Firing || State == AttackState.BetweenShots;
    private Transform cachedTarget;
    private int handsLayer;
    private ParticleSystem[] muzzles;
    private RocketAttackRunner rocketRunner;

    private void Awake()
    {
        chase = GetComponent<EnemyChaseMovement>();
        health = GetComponent<EnemyHealth>();
        handsLayer = animator != null ? animator.GetLayerIndex("Hands1") : -1;
        var particles = new List<ParticleSystem>();
        if (leftMuzzle != null) particles.AddRange(leftMuzzle.GetComponentsInChildren<ParticleSystem>(true));
        if (rightMuzzle != null) particles.AddRange(rightMuzzle.GetComponentsInChildren<ParticleSystem>(true));
        muzzles = particles.ToArray();
        StopMuzzles();
        rocketRunner = new RocketAttackRunner(this, rocketPrefab, targetPrefab, explosionPrefab);
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
        if (chase.Target != cachedTarget)
        {
            cachedTarget = chase.Target;
            player = cachedTarget != null ? cachedTarget.GetComponent<PlayerHealth>() : null;
            playerBody = cachedTarget != null ? cachedTarget.GetComponent<Rigidbody2D>() : null;
        }
        if (!CombatAllowed())
        {
            CancelAttack();
            return;
        }
        if (animator == null || !animator.isActiveAndEnabled || !chase.isActiveAndEnabled)
        {
            CancelAttack();
            return;
        }
        if (Time.timeScale == 0f || EnemyDebugAiFreeze.IsFrozen) return;
        if (IsBurstActive && !chase.IsAttackPaused) { CancelAttack(); return; }
        // Already-launched rockets advance independently of the animation/burst state.
        rocketRunner.Tick(Time.deltaTime, RocketSpawnHeight, ExplosionRadius, ExplosionDamage, CombatAllowed);
        if (!CombatAllowed()) { CancelAttack(); return; }
        timer -= Time.deltaTime;
        switch (State)
        {
            case AttackState.Chasing:
                float distance = Vector2.Distance(transform.position, player.transform.position);
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
                break;
            case AttackState.Firing:
                // FireRocket consumes one event; wait for the authored nonlooping shot to exit.
                if (!animator.IsInTransition(handsLayer) &&
                    animator.GetCurrentAnimatorStateInfo(handsLayer).IsName("animHandsEmpty"))
                {
                    if (remainingShots > 0)
                    {
                        State = AttackState.BetweenShots;
                        timer = Mathf.Max(0f, DelayBetweenShots);
                        chase.PauseForAttack(timer + 2f);
                    }
                    else
                    {
                        RestoreMovement();
                        State = AttackState.Recovering;
                        timer = Mathf.Max(0f, RecoveryDuration);
                    }
                }
                break;
            case AttackState.BetweenShots:
                if (timer <= 0f) BeginShot(false);
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

    private void BeginAttack()
    {
        if (animator == null || !animator.isActiveAndEnabled || handsLayer < 0 ||
            rocketPrefab == null || targetPrefab == null || explosionPrefab == null ||
            leftMuzzle == null || rightMuzzle == null) return;
        remainingShots = Mathf.Clamp(ShotsPerBurst, 1, 5);
        BeginShot(true);
    }

    private void BeginShot(bool first)
    {
        timer = first ? Mathf.Max(0f, StopDuration) : 0f;
        chase.PauseForAttack(timer + Mathf.Max(0f, PreAttackDuration) + 3f);
        animator.ResetTrigger("PAttack");
        animator.ResetTrigger("Attack");
        preparing = attackTriggered = false;
        State = AttackState.PreparingAttack;
    }

    // Called only by the authored Attack clip via BossRocketAnimationEvents.
    public void FireRocket()
    {
        if (!isActiveAndEnabled || State != AttackState.PreparingAttack ||
            !attackTriggered || !CombatAllowed() || animator == null || !animator.isActiveAndEnabled) return;
        if (!animator.GetCurrentAnimatorStateInfo(handsLayer).IsName("animBossShoot1") &&
            !(animator.IsInTransition(handsLayer) && animator.GetNextAnimatorStateInfo(handsLayer).IsName("animBossShoot1"))) return;
        State = AttackState.Firing; // Consume once, then launch BOTH hands.
        remainingShots--;
        chase.PauseForAttack(3f);
        Vector3 target = player.transform.position;
        if (playerBody != null) target += (Vector3)(playerBody.linearVelocity * Mathf.Max(0f, TargetPrediction));
        target.z = 0f;
        foreach (var muzzle in muzzles)
        {
            if (muzzle == null) continue;
            muzzle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzle.Play(false);
        }
        Vector2 firstOffset = Vector2.zero;
        float spread = Mathf.Max(0.01f, TargetSpreadRadius);
        for (int i = 0; i < 2; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spread;
            if (i == 0) firstOffset = offset;
            else if (Vector2.Distance(offset, firstOffset) < spread * .25f)
            {
                // Keep independently sampled targets visibly separated, even for near-identical samples.
                offset = firstOffset.sqrMagnitude > spread * spread * .01f
                    ? -firstOffset.normalized * spread : Vector2.right * spread;
            }
            Vector3 point = target + (Vector3)offset;
            Transform muzzle = i == 0 ? leftMuzzle : rightMuzzle;
            Vector3 origin = muzzle.position;
            float minDelay = Mathf.Max(.01f, Mathf.Min(RocketFallDelayMin, RocketFallDelayMax));
            float maxDelay = Mathf.Max(minDelay, Mathf.Max(RocketFallDelayMin, RocketFallDelayMax));
            rocketRunner.Launch(point, origin, Random.Range(minDelay, maxDelay),
                Mathf.Max(.05f, RocketFallDuration), ExplosionRadius);
        }
    }

    private void RestoreMovement()
    {
        if (chase != null) chase.ResumeAfterAttack();
    }

    private void StopMuzzles()
    {
        if (muzzles == null) return;
        foreach (var ps in muzzles)
            if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void CancelAttack()
    {
        rocketRunner?.Cancel();
        if (State != AttackState.Chasing && animator != null)
        {
            animator.ResetTrigger("PAttack");
            animator.ResetTrigger("Attack");
            if (animator.isActiveAndEnabled && handsLayer >= 0)
                animator.Play("animHandsEmpty", handsLayer, 0f);
        }
        StopMuzzles();
        RestoreMovement();
        remainingShots = 0;
        State = AttackState.Chasing;
        timer = Mathf.Max(0.1f, AttackCooldown);
    }

    private void OnDestroy() => rocketRunner?.Dispose();

    private void OnBossDied(EnemyHealth _) => CancelAttack();
    private void OnDisable()
    {
        if (health != null) health.OnDied -= OnBossDied;
        CancelAttack();
    }
}
