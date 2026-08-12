#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.EventSystems;

public enum TelekinesisDebugMode
{
    Base,
    Remote,
    ManualPosition,
    ManualFire,
    DualControl,
    DualSwitch,
    CommandPoint,
    FocusTarget,
    WeaponThrow,
    FullAutoCommand
}

public sealed class TelekinesisDebugPrototype : MonoBehaviour
{
    private enum WeaponThrowState
    {
        Idle,
        FlyingOut,
        Holding,
        Returning
    }

    private const float ManualRadius = 6f;
    private const float CommandRadius = 8f;
    private const float ManualFollowSpeed = 18f;
    private const float CommandFollowSpeed = 18f;
    private const float ThrowFollowSpeed = 28f;
    private const float ThrowHoldDuration = 2f;
    private const float FormationOffset = 0.8f;
    private const float ThrowArrivalDistance = 0.08f;
    private const float ThrowReturnDistance = 1.25f;

    private CharacterSpawner characterSpawner;
    private PlayerHealth playerHealth;
    private BaseWeapon primaryWeapon;
    private BaseWeapon secondaryWeapon;
    private WeaponData secondaryDebugWeaponData;
    private EnemyHealth focusTarget;
    private LineRenderer radiusVisual;
    private LineRenderer commandPointMarker;
    private LineRenderer focusTargetMarker;
    private Material debugLineMaterial;
    private Vector2 commandPoint;
    private Vector2 remoteCommandPoint;
    private Vector2 throwTarget;
    private float throwHoldRemaining;
    private float radiusVisualRadius;
    private int manualWeaponIndex;
    private WeaponThrowState throwState;
    private bool cleaningUp;
    private bool hasRemoteCommandPoint;

    public TelekinesisDebugMode CurrentMode { get; private set; } =
        TelekinesisDebugMode.Base;

    public bool IsAvailable =>
        ResolvePrimaryWeapon() &&
        playerHealth != null &&
        !playerHealth.IsDead;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        characterSpawner = FindFirstObjectByType<CharacterSpawner>();
        ResolvePrimaryWeapon();
    }

    private void Update()
    {
        if ((playerHealth != null && playerHealth.IsDead) ||
            characterSpawner == null)
        {
            ResetPrototype();
            enabled = false;
            return;
        }

        if (Time.timeScale <= 0f)
            return;

        ValidateFocusTarget();

        if (CanProcessGameplayInput())
            ProcessModeInput();

        switch (CurrentMode)
        {
            case TelekinesisDebugMode.Remote:
                UpdateRemotePosition();
                break;

            case TelekinesisDebugMode.CommandPoint:
            case TelekinesisDebugMode.FullAutoCommand:
                UpdateCommandFormation();
                break;

            case TelekinesisDebugMode.WeaponThrow:
                UpdateWeaponThrow();
                break;
        }
    }

    public void Configure(CharacterSpawner spawner)
    {
        if (spawner != null)
            characterSpawner = spawner;

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        enabled = true;
    }

    public WeaponData SecondaryDebugWeaponData => secondaryDebugWeaponData;

    public void SetPrimaryWeapon(BaseWeapon weapon)
    {
        primaryWeapon = weapon;
        enabled = weapon != null;
    }

    public void SetSecondaryDebugWeapon(WeaponData weaponData)
    {
        if (secondaryDebugWeaponData == weaponData)
            return;

        secondaryDebugWeaponData = weaponData;

        if (secondaryWeapon == null)
            return;

        TelekinesisDebugMode mode = CurrentMode;
        ClearTransientState();
        ApplyMode(mode);
    }

    public bool ApplyMode(TelekinesisDebugMode mode)
    {
        if (!IsAvailable)
            return false;

        ClearTransientState();
        CurrentMode = mode;

        switch (mode)
        {
            case TelekinesisDebugMode.Base:
                break;

            case TelekinesisDebugMode.Remote:
                ConfigureRemote();
                break;

            case TelekinesisDebugMode.ManualPosition:
                ConfigureManualPosition(primaryWeapon, false);
                SetRadiusVisualActive(true, ManualRadius);
                break;

            case TelekinesisDebugMode.ManualFire:
                primaryWeapon.SetTelekinesisDebugManualFire(
                    ManualRadius,
                    ManualFollowSpeed
                );
                SetRadiusVisualActive(true, ManualRadius);
                break;

            case TelekinesisDebugMode.DualControl:
                ConfigureDualControl(false);
                break;

            case TelekinesisDebugMode.DualSwitch:
                ConfigureDualControl(true);
                break;

            case TelekinesisDebugMode.CommandPoint:
                ConfigureCommandMode(false);
                break;

            case TelekinesisDebugMode.FocusTarget:
                primaryWeapon.SetTelekinesisDebugAutomatic(false);
                break;

            case TelekinesisDebugMode.WeaponThrow:
                primaryWeapon.SetTelekinesisDebugAutomatic(false);
                SetRadiusVisualActive(true, CommandRadius);
                break;

            case TelekinesisDebugMode.FullAutoCommand:
                ConfigureCommandMode(true);
                break;
        }

        return true;
    }

    private void ConfigureRemote()
    {
        if (!hasRemoteCommandPoint)
        {
            remoteCommandPoint = ClampToPlayerRadius(
                primaryWeapon.transform.position,
                CommandRadius
            );
            hasRemoteCommandPoint = true;
        }

        primaryWeapon.SetTelekinesisDebugExternalPosition(
            remoteCommandPoint,
            CommandFollowSpeed,
            true
        );
        SetRadiusVisualActive(true, CommandRadius);
    }

    public void ResetPrototype()
    {
        if (cleaningUp)
            return;

        cleaningUp = true;
        ClearTransientState();
        CurrentMode = TelekinesisDebugMode.Base;
        cleaningUp = false;
    }

    private void ConfigureDualControl(bool switchable)
    {
        ConfigureManualPosition(primaryWeapon, false);

        if (EnsureSecondaryWeapon())
            secondaryWeapon.SetTelekinesisDebugAutomatic(true);

        manualWeaponIndex = 0;
        SetRadiusVisualActive(true, ManualRadius);

        if (!switchable)
            manualWeaponIndex = 0;
    }

    private void ConfigureCommandMode(bool allowFocus)
    {
        EnsureSecondaryWeapon();
        commandPoint = transform.position;
        EnsureCommandPointMarker();
        SetRadiusVisualActive(true, CommandRadius);
        ConfigureCommandFormationWeapons();

        if (!allowFocus)
            ClearFocusTarget();
    }

    private void ConfigureCommandFormationWeapons()
    {
        GetCommandFormationTargets(
            out Vector2 primaryTarget,
            out Vector2 secondaryTarget
        );
        primaryWeapon.SetTelekinesisDebugExternalAutoPosition(
            primaryTarget,
            CommandFollowSpeed,
            false,
            true
        );

        if (secondaryWeapon != null)
        {
            secondaryWeapon.SetTelekinesisDebugExternalAutoPosition(
                secondaryTarget,
                CommandFollowSpeed,
                true,
                true
            );
        }
    }

    private void ConfigureManualPosition(
        BaseWeapon weapon,
        bool secondary)
    {
        weapon?.SetTelekinesisDebugManual(
            ManualRadius,
            ManualFollowSpeed,
            secondary
        );
    }

    private void ProcessModeInput()
    {
        switch (CurrentMode)
        {
            case TelekinesisDebugMode.Remote:
                if (Input.GetMouseButtonDown(1))
                    SetRemotePointFromMouse();
                break;

            case TelekinesisDebugMode.DualSwitch:
                if (Input.GetKeyDown(KeyCode.Tab))
                    SwitchManualWeapon();
                break;

            case TelekinesisDebugMode.CommandPoint:
                if (Input.GetMouseButtonDown(1))
                    SetCommandPointFromMouse();
                break;

            case TelekinesisDebugMode.FocusTarget:
                if (Input.GetMouseButtonDown(0))
                    SetFocusTargetFromMouse();
                break;

            case TelekinesisDebugMode.WeaponThrow:
                if (Input.GetMouseButtonDown(1))
                    StartOrRetargetWeaponThrow();
                break;

            case TelekinesisDebugMode.FullAutoCommand:
                if (Input.GetMouseButtonDown(1))
                    SetCommandPointFromMouse();
                if (Input.GetMouseButtonDown(0))
                    SetFocusTargetFromMouse();
                break;
        }
    }

    private void SetRemotePointFromMouse()
    {
        remoteCommandPoint = ClampToPlayerRadius(
            GetMouseWorldPosition(),
            CommandRadius
        );
        hasRemoteCommandPoint = true;
        primaryWeapon?.UpdateTelekinesisDebugPositionTarget(
            remoteCommandPoint
        );
    }

    private void UpdateRemotePosition()
    {
        if (!hasRemoteCommandPoint || primaryWeapon == null)
            return;

        remoteCommandPoint = ClampToPlayerRadius(
            remoteCommandPoint,
            CommandRadius
        );
        primaryWeapon.UpdateTelekinesisDebugPositionTarget(
            remoteCommandPoint
        );
    }

    private void SwitchManualWeapon()
    {
        if (secondaryWeapon == null)
            return;

        manualWeaponIndex = 1 - manualWeaponIndex;

        if (manualWeaponIndex == 0)
        {
            ConfigureManualPosition(primaryWeapon, false);
            secondaryWeapon.SetTelekinesisDebugAutomatic(true);
        }
        else
        {
            primaryWeapon.SetTelekinesisDebugAutomatic(false);
            ConfigureManualPosition(secondaryWeapon, true);
        }
    }

    private void SetCommandPointFromMouse()
    {
        commandPoint = ClampToPlayerRadius(
            GetMouseWorldPosition(),
            CommandRadius
        );
        EnsureCommandPointMarker();
    }

    private void UpdateCommandFormation()
    {
        commandPoint = ClampToPlayerRadius(
            commandPoint,
            CommandRadius
        );

        if (commandPointMarker != null)
            commandPointMarker.transform.position = commandPoint;

        GetCommandFormationTargets(
            out Vector2 primaryTarget,
            out Vector2 secondaryTarget
        );
        primaryWeapon?.UpdateTelekinesisDebugPositionTarget(primaryTarget);
        secondaryWeapon?.UpdateTelekinesisDebugPositionTarget(secondaryTarget);
    }

    private void GetCommandFormationTargets(
        out Vector2 primaryTarget,
        out Vector2 secondaryTarget)
    {
        Vector2 left = commandPoint + Vector2.left * FormationOffset;
        Vector2 right = commandPoint + Vector2.right * FormationOffset;
        primaryTarget = ClampToPlayerRadius(left, CommandRadius);
        secondaryTarget = ClampToPlayerRadius(right, CommandRadius);
    }

    private void SetFocusTargetFromMouse()
    {
        SetFocusTarget(FindEnemyAtWorldPosition(GetMouseWorldPosition()));
    }

    private void SetFocusTarget(EnemyHealth target)
    {
        focusTarget = target != null && !target.IsDead ? target : null;
        ApplyFocusTargetToWeapons();
        UpdateFocusTargetMarker();
    }

    private void ApplyFocusTargetToWeapons()
    {
        primaryWeapon?.SetTelekinesisDebugPriorityTarget(focusTarget);
        secondaryWeapon?.SetTelekinesisDebugPriorityTarget(focusTarget);
    }

    private void ValidateFocusTarget()
    {
        if (focusTarget == null)
            return;

        if (!focusTarget.IsDead && focusTarget.gameObject.activeInHierarchy)
            return;

        ClearFocusTarget();
    }

    private void ClearFocusTarget()
    {
        focusTarget = null;
        ApplyFocusTargetToWeapons();

        if (focusTargetMarker != null)
            Destroy(focusTargetMarker.gameObject);

        focusTargetMarker = null;
    }

    private void StartOrRetargetWeaponThrow()
    {
        throwTarget = ClampToPlayerRadius(
            GetMouseWorldPosition(),
            CommandRadius
        );
        throwHoldRemaining = ThrowHoldDuration;
        throwState = WeaponThrowState.FlyingOut;
        primaryWeapon.SetTelekinesisDebugExternalAutoPosition(
            throwTarget,
            ThrowFollowSpeed,
            false,
            false
        );
    }

    private void UpdateWeaponThrow()
    {
        switch (throwState)
        {
            case WeaponThrowState.FlyingOut:
                throwTarget = ClampToPlayerRadius(
                    throwTarget,
                    CommandRadius
                );
                primaryWeapon.UpdateTelekinesisDebugPositionTarget(
                    throwTarget
                );

                if (Vector2.Distance(
                        primaryWeapon.transform.position,
                        throwTarget) <= ThrowArrivalDistance)
                {
                    throwState = WeaponThrowState.Holding;
                    throwHoldRemaining = ThrowHoldDuration;
                }
                break;

            case WeaponThrowState.Holding:
                throwHoldRemaining -= Time.deltaTime;

                if (throwHoldRemaining <= 0f)
                {
                    throwState = WeaponThrowState.Returning;
                    primaryWeapon.SetTelekinesisDebugAutomatic(false);
                }
                break;

            case WeaponThrowState.Returning:
                if (Vector2.Distance(
                        primaryWeapon.transform.position,
                        transform.position) <= ThrowReturnDistance)
                {
                    throwState = WeaponThrowState.Idle;
                }
                break;
        }
    }

    private void ClearTransientState()
    {
        ClearFocusTarget();

        if (primaryWeapon != null)
            primaryWeapon.SetTelekinesisDebugBase();

        DestroySecondaryWeapon();
        DestroyCommandPointMarker();
        SetRadiusVisualActive(false, 0f);
        commandPoint = transform.position;
        throwTarget = transform.position;
        throwHoldRemaining = 0f;
        throwState = WeaponThrowState.Idle;
        manualWeaponIndex = 0;
    }

    private bool EnsureSecondaryWeapon()
    {
        if (secondaryWeapon != null)
            return true;

        if (characterSpawner == null)
            characterSpawner = FindFirstObjectByType<CharacterSpawner>();

        if (characterSpawner == null)
            return false;

        WeaponData weaponData = secondaryDebugWeaponData != null
            ? secondaryDebugWeaponData
            : primaryWeapon.weaponData;
        secondaryWeapon = characterSpawner.SpawnTelekinesisDebugWeapon(
            gameObject,
            weaponData,
            primaryWeapon
        );

        if (secondaryWeapon == null)
            return false;

        secondaryWeapon.name =
            $"{primaryWeapon.name} [TELEKINESIS DEBUG AUTO]";
        return true;
    }

    private void DestroySecondaryWeapon()
    {
        if (secondaryWeapon == null)
            return;

        GameObject secondaryObject = secondaryWeapon.gameObject;
        secondaryWeapon = null;
        secondaryObject.SetActive(false);
        Destroy(secondaryObject);
    }

    private bool ResolvePrimaryWeapon()
    {
        if (primaryWeapon != null &&
            !primaryWeapon.IsTelekinesisDebugSecondary)
        {
            return true;
        }

        primaryWeapon = null;
        BaseWeapon[] weapons = GetComponentsInChildren<BaseWeapon>(true);

        for (int i = 0; i < weapons.Length; i++)
        {
            BaseWeapon candidate = weapons[i];

            if (candidate == null || candidate.IsTelekinesisDebugSecondary)
                continue;

            primaryWeapon = candidate;
            return true;
        }

        return false;
    }

    private bool CanProcessGameplayInput()
    {
        if (Time.timeScale <= 0f)
            return false;

        EventSystem eventSystem = EventSystem.current;
        return eventSystem == null || !eventSystem.IsPointerOverGameObject();
    }

    private Vector2 GetMouseWorldPosition()
    {
        Camera targetCamera = Camera.main;

        if (targetCamera == null)
            return transform.position;

        Vector3 mouseWorld = targetCamera.ScreenToWorldPoint(
            Input.mousePosition
        );
        return new Vector2(mouseWorld.x, mouseWorld.y);
    }

    private Vector2 ClampToPlayerRadius(Vector2 target, float radius)
    {
        Vector2 playerPosition = transform.position;
        return playerPosition + Vector2.ClampMagnitude(
            target - playerPosition,
            radius
        );
    }

    private static EnemyHealth FindEnemyAtWorldPosition(Vector2 position)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(position);
        EnemyHealth selected = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth enemy = hits[i] != null
                ? hits[i].GetComponentInParent<EnemyHealth>()
                : null;

            if (enemy == null || enemy.IsDead ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distanceSquared =
                ((Vector2)enemy.transform.position - position).sqrMagnitude;

            if (distanceSquared >= nearestDistanceSquared)
                continue;

            selected = enemy;
            nearestDistanceSquared = distanceSquared;
        }

        return selected;
    }

    private void SetRadiusVisualActive(bool active, float radius)
    {
        if (!active)
        {
            if (radiusVisual != null)
                radiusVisual.enabled = false;

            return;
        }

        EnsureRadiusVisual();

        if (radiusVisual == null)
            return;

        if (!Mathf.Approximately(radiusVisualRadius, radius))
        {
            radiusVisualRadius = radius;
            SetRingPositions(radiusVisual, radius);
        }

        radiusVisual.enabled = true;
    }

    private void EnsureRadiusVisual()
    {
        if (radiusVisual != null)
            return;

        radiusVisual = CreateRing(
            "Telekinesis Radius (Debug)",
            transform,
            64,
            0.025f,
            new Color(0.1f, 0.9f, 1f, 0.22f),
            50
        );
    }

    private void EnsureCommandPointMarker()
    {
        if (commandPointMarker == null)
        {
            commandPointMarker = CreateRing(
                "Telekinesis Command Point (Debug)",
                transform,
                20,
                0.045f,
                new Color(0.1f, 0.95f, 1f, 0.8f),
                55
            );
            SetRingPositions(commandPointMarker, 0.28f);
        }

        if (commandPointMarker != null)
        {
            commandPointMarker.transform.position = commandPoint;
            commandPointMarker.enabled = true;
        }
    }

    private void DestroyCommandPointMarker()
    {
        if (commandPointMarker != null)
            Destroy(commandPointMarker.gameObject);

        commandPointMarker = null;
    }

    private void UpdateFocusTargetMarker()
    {
        if (focusTargetMarker != null)
            Destroy(focusTargetMarker.gameObject);

        focusTargetMarker = null;

        if (focusTarget == null)
            return;

        focusTargetMarker = CreateRing(
            "Telekinesis Focus Target (Debug)",
            focusTarget.transform,
            20,
            0.055f,
            new Color(1f, 0.2f, 0.35f, 0.9f),
            60
        );

        if (focusTargetMarker != null)
        {
            focusTargetMarker.transform.localPosition =
                new Vector3(0f, 1.2f, 0f);
            SetRingPositions(focusTargetMarker, 0.32f);
        }
    }

    private LineRenderer CreateRing(
        string objectName,
        Transform parent,
        int segments,
        float width,
        Color color,
        int sortingOrder)
    {
        EnsureDebugLineMaterial();

        if (debugLineMaterial == null)
            return null;

        GameObject visualObject = new(objectName);
        visualObject.transform.SetParent(parent, false);
        LineRenderer line = visualObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Mathf.Max(8, segments);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerName = "Midground";
        line.sortingOrder = sortingOrder;
        line.sharedMaterial = debugLineMaterial;
        return line;
    }

    private void EnsureDebugLineMaterial()
    {
        if (debugLineMaterial != null)
            return;

        Shader shader = Shader.Find(
            "Universal Render Pipeline/2D/Sprite-Unlit-Default"
        );
        shader ??= Shader.Find("Sprites/Default");

        if (shader == null)
            return;

        debugLineMaterial = new Material(shader)
        {
            name = "Telekinesis Debug Line Material"
        };
    }

    private static void SetRingPositions(LineRenderer line, float radius)
    {
        if (line == null)
            return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                )
            );
        }
    }

    private void OnDestroy()
    {
        ResetPrototype();

        if (debugLineMaterial != null)
            Destroy(debugLineMaterial);
    }
}
#endif
