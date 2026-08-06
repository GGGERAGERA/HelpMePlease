#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public enum TelekinesisDebugMode
{
    Base,
    ExtendedRadius,
    ManualControl,
    DualControl
}

public sealed class TelekinesisDebugPrototype : MonoBehaviour
{
    [SerializeField, Min(1f)] private float extendedRadiusMultiplier = 1.8f;
    [SerializeField, Min(0.1f)] private float manualRadius = 6f;
    [SerializeField, Min(0.1f)] private float manualFollowSpeed = 18f;

    private CharacterSpawner characterSpawner;
    private PlayerHealth playerHealth;
    private BaseWeapon primaryWeapon;
    private BaseWeapon secondaryWeapon;
    private LineRenderer radiusVisual;
    private Material radiusMaterial;
    private bool cleaningUp;

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

    public bool ApplyMode(TelekinesisDebugMode mode)
    {
        if (!IsAvailable)
            return false;

        switch (mode)
        {
            case TelekinesisDebugMode.Base:
                DestroySecondaryWeapon();
                primaryWeapon.SetTelekinesisDebugBase();
                SetRadiusVisualActive(false);
                break;

            case TelekinesisDebugMode.ExtendedRadius:
                DestroySecondaryWeapon();
                primaryWeapon.SetTelekinesisDebugExtended(
                    extendedRadiusMultiplier
                );
                SetRadiusVisualActive(false);
                break;

            case TelekinesisDebugMode.ManualControl:
                DestroySecondaryWeapon();
                ConfigurePrimaryManual();
                SetRadiusVisualActive(true);
                break;

            case TelekinesisDebugMode.DualControl:
                ConfigurePrimaryManual();

                if (!EnsureSecondaryWeapon())
                {
                    CurrentMode = TelekinesisDebugMode.ManualControl;
                    SetRadiusVisualActive(true);
                    return false;
                }

                SetRadiusVisualActive(true);
                break;
        }

        CurrentMode = mode;
        return true;
    }

    public void ResetPrototype()
    {
        if (cleaningUp)
            return;

        cleaningUp = true;

        if (primaryWeapon != null)
            primaryWeapon.SetTelekinesisDebugBase();

        DestroySecondaryWeapon();
        SetRadiusVisualActive(false);
        CurrentMode = TelekinesisDebugMode.Base;
        cleaningUp = false;
    }

    private void ConfigurePrimaryManual()
    {
        primaryWeapon.SetTelekinesisDebugManual(
            manualRadius,
            manualFollowSpeed
        );
    }

    private bool EnsureSecondaryWeapon()
    {
        if (secondaryWeapon != null)
        {
            secondaryWeapon.SetTelekinesisDebugAutomaticClone();
            return true;
        }

        if (characterSpawner == null)
            characterSpawner = FindFirstObjectByType<CharacterSpawner>();

        if (characterSpawner == null)
            return false;

        secondaryWeapon =
            characterSpawner.SpawnTelekinesisDebugWeaponClone(
                gameObject,
                primaryWeapon
            );

        if (secondaryWeapon == null)
            return false;

        secondaryWeapon.name =
            $"{primaryWeapon.name} [TELEKINESIS DEBUG AUTO]";
        secondaryWeapon.SetTelekinesisDebugAutomaticClone();
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

    private void SetRadiusVisualActive(bool active)
    {
        if (!active)
        {
            if (radiusVisual != null)
                radiusVisual.enabled = false;

            return;
        }

        EnsureRadiusVisual();

        if (radiusVisual != null)
            radiusVisual.enabled = true;
    }

    private void EnsureRadiusVisual()
    {
        if (radiusVisual != null)
            return;

        GameObject visualObject = new("Telekinesis Radius (Debug)");
        visualObject.transform.SetParent(transform, false);
        radiusVisual = visualObject.AddComponent<LineRenderer>();
        radiusVisual.useWorldSpace = false;
        radiusVisual.loop = true;
        radiusVisual.positionCount = 64;
        radiusVisual.startWidth = 0.025f;
        radiusVisual.endWidth = 0.025f;
        radiusVisual.startColor = new Color(0.1f, 0.9f, 1f, 0.22f);
        radiusVisual.endColor = new Color(0.1f, 0.9f, 1f, 0.22f);
        radiusVisual.sortingLayerName = "Midground";
        radiusVisual.sortingOrder = 50;

        Shader shader = Shader.Find(
            "Universal Render Pipeline/2D/Sprite-Unlit-Default"
        );
        shader ??= Shader.Find("Sprites/Default");

        if (shader != null)
        {
            radiusMaterial = new Material(shader)
            {
                name = "Telekinesis Radius Material (Debug)"
            };
            radiusVisual.sharedMaterial = radiusMaterial;
        }

        for (int i = 0; i < radiusVisual.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f /
                radiusVisual.positionCount;
            radiusVisual.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * manualRadius,
                    Mathf.Sin(angle) * manualRadius,
                    0f
                )
            );
        }
    }

    private void OnDestroy()
    {
        ResetPrototype();

        if (radiusMaterial != null)
            Destroy(radiusMaterial);
    }
}
#endif
