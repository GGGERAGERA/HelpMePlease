using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public enum CombatFeelDummyMode
{
    Static,
    Follow,
    Orbit,
    NormalAi
}

public enum CombatFeelDummyArchetype
{
    Basic,
    Elite,
    Shooter
}

[DisallowMultipleComponent]
public sealed class CombatFeelTestDummy : MonoBehaviour
{
    private readonly List<Behaviour> controlledBehaviours = new();
    private readonly List<bool> originalEnabledStates = new();
    private CombatFeelTestDummyController owner;
    private Rigidbody2D body;
    private RigidbodyType2D originalBodyType;
    private Transform visualRoot;
    private Vector3 visualBaseScale;

    public bool IsDebugOwned => true;
    public bool SuppressesProductionRewards => true;
    public bool Invulnerable { get; set; } = true;
    public EnemyHealth Health { get; private set; }

    public void Initialize(CombatFeelTestDummyController controller)
    {
        owner = controller;
        Health = GetComponent<EnemyHealth>();
        body = GetComponent<Rigidbody2D>();
        if (body != null)
            originalBodyType = body.bodyType;

        CaptureBehaviour(GetComponent<EnemyMovement>());
        CaptureBehaviour(GetComponent<EnemyCollisionHandler>());
        CaptureBehaviour(GetComponent<TurretEnemyBehaviour>());
        CaptureBehaviour(GetComponent<EyesEnemyBehaviour>());

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null)
        {
            visualRoot = renderer.transform;
            while (visualRoot.parent != null && visualRoot.parent != transform)
                visualRoot = visualRoot.parent;
            visualBaseScale = visualRoot.localScale;
        }
    }

    public void ApplyMode(CombatFeelDummyMode mode)
    {
        bool normalAi = mode == CombatFeelDummyMode.NormalAi;
        for (int i = 0; i < controlledBehaviours.Count; i++)
        {
            Behaviour behaviour = controlledBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = normalAi && originalEnabledStates[i];
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = normalAi ? originalBodyType : RigidbodyType2D.Kinematic;
        }
    }

    public void MoveRoot(Vector2 position)
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
        }
        else
        {
            transform.position = position;
        }
    }

    public void Face(Vector2 direction)
    {
        if (visualRoot == null || Mathf.Abs(direction.x) < 0.01f)
            return;

        Vector3 scale = visualRoot.localScale;
        float magnitude = Mathf.Abs(scale.x);
        if (magnitude < .0001f)
            magnitude = Mathf.Abs(visualBaseScale.x);
        scale.x = direction.x > 0f ? -magnitude : magnitude;
        visualRoot.localScale = scale;
    }

    private void CaptureBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || controlledBehaviours.Contains(behaviour))
            return;
        controlledBehaviours.Add(behaviour);
        originalEnabledStates.Add(behaviour.enabled);
    }

    private void OnDestroy()
    {
        owner?.NotifyDummyDestroyed(this);
    }
}

[DisallowMultipleComponent]
public sealed class CombatFeelTestDummyController : MonoBehaviour
{
    private EnemySpawner spawner;
    private CombatFeelTestDummy dummy;
    private Vector2 anchor;
    private float orbitAngle;
    private string status = "NOT SPAWNED";

    public CombatFeelDummyMode Mode { get; private set; } = CombatFeelDummyMode.Static;
    public CombatFeelDummyArchetype Archetype { get; private set; } = CombatFeelDummyArchetype.Basic;
    public bool Invulnerable { get; private set; } = true;
    public bool FacePlayer { get; private set; } = true;
    public bool Clockwise { get; private set; }
    public float Distance { get; private set; } = 4f;
    public float FollowSpeed { get; private set; } = 3f;
    public float OrbitSpeed { get; private set; } = 35f;
    public float OrbitRadius { get; private set; } = 4f;
    public bool HasDummy => dummy != null;
    public string Status => status;

    public static string GetModeLabel(CombatFeelDummyMode mode) =>
        mode == CombatFeelDummyMode.NormalAi
            ? "NORMAL AI"
            : mode.ToString().ToUpperInvariant();

    public void Configure(EnemySpawner enemySpawner)
    {
        spawner = enemySpawner;
    }

    public bool CanSpawn(CombatFeelDummyArchetype archetype)
    {
        return ResolvePrefab(archetype) != null;
    }

    public bool Spawn()
    {
        Despawn();
        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null)
            return Fail("SPAWN FAILED — NO SPAWNER",
                "EnemySpawner not found");

        Transform player = ResolvePlayer();
        if (player == null)
            return Fail("SPAWN FAILED — NO PLAYER", "player not found");

        GameObject prefab = ResolvePrefab(Archetype);
        if (prefab == null)
            return Fail($"SPAWN FAILED — NO {Archetype.ToString().ToUpperInvariant()}",
                $"{Archetype} production prefab not resolved");

        anchor = GetPositionBeforePlayer(player, Distance);
        if (!IsFinite(anchor))
            return Fail("SPAWN FAILED — BAD POSITION",
                $"spawn position is not finite: {anchor}");

        GameObject instance = spawner.SpawnDebugEnemyAt(prefab, anchor);
        if (instance == null)
            return Fail("SPAWN FAILED — CREATE ERROR",
                "production spawn API returned null");

        EnemyHealth health = instance.GetComponent<EnemyHealth>();
        if (health == null)
        {
            Destroy(instance);
            return Fail("SPAWN FAILED — INVALID PREFAB",
                $"production prefab '{prefab.name}' has no EnemyHealth");
        }

        dummy = instance.GetComponent<CombatFeelTestDummy>();
        dummy ??= instance.AddComponent<CombatFeelTestDummy>();
        dummy.Initialize(this);
        dummy.Invulnerable = Invulnerable;
        orbitAngle = Mathf.Atan2(anchor.y - player.position.y,
            anchor.x - player.position.x) * Mathf.Rad2Deg;
        dummy.ApplyMode(Mode);
        status = $"{Archetype.ToString().ToUpperInvariant()} / {GetModeLabel(Mode)}";
        Debug.Log(
            $"[FEEL DUMMY] Spawned {Archetype} from production prefab " +
            $"'{prefab.name}' at {anchor}; wave registration=false.",
            instance);
        return true;
    }

    public void Despawn()
    {
        if (dummy == null)
            return;
        CombatFeelTestDummy owned = dummy;
        dummy = null;
        status = "NOT SPAWNED";
        Destroy(owned.gameObject);
    }

    public void ResetPosition()
    {
        Transform player = ResolvePlayer();
        if (player == null)
            return;
        anchor = GetPositionBeforePlayer(player, Distance);
        dummy?.MoveRoot(anchor);
    }

    public void SetMode(CombatFeelDummyMode value)
    {
        Mode = value;
        dummy?.ApplyMode(value);
        if (dummy != null)
            status = $"{Archetype.ToString().ToUpperInvariant()} / " +
                GetModeLabel(Mode);
        if (value == CombatFeelDummyMode.Orbit)
        {
            Transform player = ResolvePlayer();
            if (player != null && dummy != null)
            {
                Vector2 offset = (Vector2)dummy.transform.position -
                    (Vector2)player.position;
                orbitAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            }
        }
    }

    public void SetArchetype(CombatFeelDummyArchetype value)
    {
        if (Archetype == value)
            return;
        Archetype = value;
        if (HasDummy)
            Spawn();
    }

    public void SetInvulnerable(bool value)
    {
        Invulnerable = value;
        if (dummy != null)
            dummy.Invulnerable = value;
    }

    public void SetFacePlayer(bool value) => FacePlayer = value;
    public void SetClockwise(bool value) => Clockwise = value;
    public void SetDistance(float value) => Distance = Mathf.Clamp(value, 1f, 12f);
    public void SetFollowSpeed(float value) => FollowSpeed = Mathf.Clamp(value, .25f, 12f);
    public void SetOrbitSpeed(float value) => OrbitSpeed = Mathf.Clamp(value, 1f, 180f);
    public void SetOrbitRadius(float value) => OrbitRadius = Mathf.Clamp(value, 1f, 12f);

    public void SimulateHit(float healthFraction, bool critical, bool lethal)
    {
        if (dummy == null || dummy.Health == null)
            return;
        float damage = Mathf.Max(1f, dummy.Health.MaxHealth * healthFraction);
        Vector2 direction = GetHitDirection();
        Vector2 hitPoint = dummy.transform.position;
        dummy.Health.DebugSimulateFeedback(damage, hitPoint, critical, lethal);
        PhysicalCombatFeedbackRuntime.NotifySimulatedHit(new WeaponHitContext(
            null, null, dummy.Health, hitPoint, direction, damage, critical), lethal);
    }

    public void NotifyDummyDestroyed(CombatFeelTestDummy destroyed)
    {
        if (dummy == destroyed)
        {
            dummy = null;
            status = "NOT SPAWNED";
        }
    }

    private void FixedUpdate()
    {
        if (dummy == null || Mode == CombatFeelDummyMode.NormalAi)
            return;
        Transform player = ResolvePlayer();
        if (player == null)
            return;

        Vector2 target;
        switch (Mode)
        {
            case CombatFeelDummyMode.Follow:
                Vector2 offset = (Vector2)dummy.transform.position -
                    (Vector2)player.position;
                if (offset.sqrMagnitude <= Distance * Distance)
                    target = dummy.transform.position;
                else
                    target = Vector2.MoveTowards(dummy.transform.position,
                        player.position, FollowSpeed * Time.fixedDeltaTime);
                break;
            case CombatFeelDummyMode.Orbit:
                orbitAngle += (Clockwise ? -1f : 1f) * OrbitSpeed *
                    Time.fixedDeltaTime;
                float radians = orbitAngle * Mathf.Deg2Rad;
                target = (Vector2)player.position + new Vector2(
                    Mathf.Cos(radians), Mathf.Sin(radians)) * OrbitRadius;
                anchor = target;
                break;
            default:
                target = anchor;
                break;
        }

        dummy.MoveRoot(target);
        if (FacePlayer)
            dummy.Face((Vector2)player.position - target);
    }

    private GameObject ResolvePrefab(CombatFeelDummyArchetype archetype)
    {
        if (spawner == null)
            return null;
        EnemySpawner.DebugEnemyArchetype mapped = archetype switch
        {
            CombatFeelDummyArchetype.Elite => EnemySpawner.DebugEnemyArchetype.Elite,
            CombatFeelDummyArchetype.Shooter => EnemySpawner.DebugEnemyArchetype.Shooter,
            _ => EnemySpawner.DebugEnemyArchetype.Basic
        };
        return spawner.FindDebugEnemyPrefab(mapped);
    }

    private static Transform ResolvePlayer() =>
        GameObject.FindGameObjectWithTag("Player")?.transform;

    private static Vector2 GetPositionBeforePlayer(Transform player, float distance)
    {
        Vector2 direction = player.right;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null && body.linearVelocity.sqrMagnitude > .04f)
            direction = body.linearVelocity.normalized;
        if (direction.sqrMagnitude < .001f)
            direction = Vector2.right;
        return (Vector2)player.position + direction.normalized * distance;
    }

    private bool Fail(string uiStatus, string reason)
    {
        status = uiStatus;
        Debug.LogWarning($"[FEEL DUMMY] Spawn failed: {reason}.", this);
        return false;
    }

    private static bool IsFinite(Vector2 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y);

    private Vector2 GetHitDirection()
    {
        Transform player = ResolvePlayer();
        return player != null
            ? (Vector2)dummy.transform.position - (Vector2)player.position
            : Vector2.right;
    }

    private void OnDisable() => Despawn();
    private void OnDestroy() => Despawn();
}
#endif
