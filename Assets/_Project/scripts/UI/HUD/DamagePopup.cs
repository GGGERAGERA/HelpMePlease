using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    private const float Lifetime = 1f;
    private const float HealingLifetime = .8f;

    [Header("Damage appearance (healing and rewards keep the authored TMP style)")]
    [SerializeField] private Material damageMaterial;
    [SerializeField] private Color damageColor = new(1f, .95f, .84f, 1f);
    [SerializeField] private Color criticalColor = new(1f, .6f, .16f, 1f);
    [SerializeField] private float damageFontSize = 17f;
    [SerializeField] private float damageCharacterSpacing = -1f;
    [SerializeField] private float criticalScale = 1.3f;

    [Header("Damage motion")]
    [SerializeField, Min(.01f)] private float damageLifetime = .72f;
    [SerializeField, Min(0f)] private float damageFadeDelay = .32f;
    [SerializeField, Min(.01f)] private float damageFadeDuration = .4f;
    [SerializeField] private float damageRiseSpeed = 1.85f;
    [SerializeField, Min(0f)] private float damageGravity = 2.4f;
    [SerializeField, Min(0f)] private float damageDriftRandomness = .38f;
    [SerializeField, Min(0f)] private float damageRotationRandomness = 3f;
    [SerializeField, Min(0f)] private float damageScalePunch = .2f;
    [SerializeField, Min(.01f)] private float damagePunchDuration = .16f;
    [SerializeField, Min(0f)] private float criticalLifetime = 1.1f;
    [SerializeField, Min(0f)] private float criticalRise = 1.08f;
    [SerializeField, Min(0f)] private float criticalPunch = 1.2f;

    private TextMeshPro text;
    private PooledGameObject pooledObject;
    private Color authoredColor;
    private float authoredFontSize;
    private Material authoredMaterial;
    private FontStyles authoredFontStyle;
    private float authoredCharacterSpacing;
    private int authoredSortingLayer;
    private int authoredSortingOrder;
    private Vector3 authoredScale;
    private Quaternion authoredRotation;
    private float timer = Lifetime;
    private bool healing;
    private float healingRise;
    private float feelLifetime;
    private float feelAge;
    private float fadeDelay;
    private float fadeDuration;
    private float spawnDelay;
    private float scalePunch;
    private Vector2 velocity;
    private Vector3 feelOrigin;
    private Vector3 feelBaseScale;
    private Color feelColor;
    private bool feelActive;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugDamageNumbersVisible { get; set; } = true;
    private bool isDamageNumber;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDebugVisibility() => DebugDamageNumbersVisible = true;
    private void LateUpdate()
    {
        if (text != null) text.renderer.enabled = !isDamageNumber || DebugDamageNumbersVisible;
    }
#endif

    private void Awake()
    {
        text = GetComponent<TextMeshPro>();
        if (text == null)
            Debug.LogError("DamagePopup: TextMeshPro component not found!");
        else
        {
            authoredColor = text.color;
            authoredFontSize = text.fontSize;
            authoredMaterial = text.fontSharedMaterial;
            authoredFontStyle = text.fontStyle;
            authoredCharacterSpacing = text.characterSpacing;
            authoredSortingLayer = text.renderer.sortingLayerID;
            authoredSortingOrder = text.renderer.sortingOrder;
        }
        authoredScale = transform.localScale;
        authoredRotation = transform.localRotation;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        isDamageNumber = false;
        if (text != null) text.renderer.enabled = true;
#endif
        timer = Lifetime;
        healing = false;
        feelActive = false;
        transform.localScale = authoredScale;
        transform.localRotation = authoredRotation;
        if (text != null)
        {
            text.color = authoredColor;
            text.fontSize = authoredFontSize;
            text.fontSharedMaterial = authoredMaterial;
            text.fontStyle = authoredFontStyle;
            text.characterSpacing = authoredCharacterSpacing;
            text.renderer.sortingLayerID = authoredSortingLayer;
            text.renderer.sortingOrder = authoredSortingOrder;
        }
    }

    internal void ConfigurePoolHandle(PooledGameObject item) => pooledObject = item;

    public void SetDamage(int damage, bool isCritical = false)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        isDamageNumber = true;
        if (text != null) text.renderer.enabled = DebugDamageNumbersVisible;
#endif
        if (text == null) return;
        text.text = damage.ToString();
        text.color = isCritical ? criticalColor : damageColor;
        text.fontSize = damageFontSize;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = damageCharacterSpacing;
        text.fontSharedMaterial = damageMaterial;
        ConfigureFeel(isCritical);
    }

    public void SetRewardMultiplier(float multiplier, Color color)
    {
        if (text == null) return;
        text.text = string.Format(LocalizationService.EnsureExists().Get("hud.goldMultiplier"), multiplier);
        text.color = color;
        text.fontSize = authoredFontSize * 1.15f;
    }

    public void SetHealing(float amount)
    {
        if (text == null) return;
        healing = true;
        timer = HealingLifetime;
        text.text = "+" + amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        text.color = new Color(.25f, 1f, .35f, 1f);
        text.renderer.sortingLayerID = SortingLayer.NameToID("UI");
        text.renderer.sortingOrder = 100;
        // World-space TMP, with a rise matching 32 screen pixels at pickup time.
        Camera camera = Camera.main;
        Vector3 origin = camera != null ? camera.WorldToScreenPoint(transform.position) : Vector3.zero;
        healingRise = camera != null
            ? Vector3.Distance(camera.ScreenToWorldPoint(origin),
                camera.ScreenToWorldPoint(origin + Vector3.up * 32f))
            : .5f;
    }

    private void Update()
    {
        if (healing)
        {
            transform.position += Vector3.up * (healingRise / HealingLifetime * Time.deltaTime);
            timer -= Time.deltaTime;
            Color color = text.color;
            color.a = Mathf.Clamp01(timer / HealingLifetime);
            text.color = color;
            if (timer <= 0f) Release();
            return;
        }
        if (feelActive)
        {
            UpdateFeel();
            return;
        }
        transform.position += Vector3.up * (1.5f * Time.deltaTime);
        timer -= Time.deltaTime;
        if (timer <= 0f) Release();
    }

    private void Release()
    {
        if (pooledObject == null || !pooledObject.Release())
            Destroy(gameObject);
    }

    private void ConfigureFeel(bool critical)
    {
        feelActive = true;
        feelAge = 0f;
        feelLifetime = damageLifetime;
        fadeDelay = damageFadeDelay;
        fadeDuration = damageFadeDuration;
        spawnDelay = 0f;
        scalePunch = damageScalePunch;
        float initialScale = 1f;
        float riseSpeed = damageRiseSpeed;
        float drift = 0f;
        float driftRandomness = damageDriftRandomness;
        float rotation = 0f;
        float rotationRandomness = damageRotationRandomness;
        float critScale = criticalScale;
        float critLifetime = criticalLifetime;
        float critRise = criticalRise;
        float critPunch = criticalPunch;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // F1 is an optional relative experiment over the authored production look.
        // Neutral/Reset must leave the exact same result in Editor and Release.
        initialScale = LabAdjustment(initialScale, CombatFeelParameter.PopupInitialScale);
        scalePunch = LabAdjustment(scalePunch, CombatFeelParameter.PopupScalePunch);
        riseSpeed = LabAdjustment(riseSpeed, CombatFeelParameter.PopupRiseSpeed);
        feelLifetime = LabAdjustment(feelLifetime, CombatFeelParameter.PopupLifetime);
        fadeDelay = LabAdjustment(fadeDelay, CombatFeelParameter.PopupFadeDelay);
        fadeDuration = LabAdjustment(fadeDuration, CombatFeelParameter.PopupFadeDuration);
        spawnDelay = LabAdjustment(spawnDelay, CombatFeelParameter.PopupDelay);
        drift = LabAdjustment(drift, CombatFeelParameter.PopupHorizontalDrift);
        driftRandomness = LabAdjustment(driftRandomness, CombatFeelParameter.PopupDriftRandomness);
        rotation = LabAdjustment(rotation, CombatFeelParameter.PopupRotation);
        rotationRandomness = LabAdjustment(rotationRandomness, CombatFeelParameter.PopupRotationRandomness);
        critScale = LabAdjustment(critScale, CombatFeelParameter.CritPopupScale);
        critLifetime = LabAdjustment(critLifetime, CombatFeelParameter.CritPopupLifetime);
        critRise = LabAdjustment(critRise, CombatFeelParameter.CritPopupRise);
        critPunch = LabAdjustment(critPunch, CombatFeelParameter.CritPopupPunch);
#endif
        if (critical)
        {
            feelLifetime *= critLifetime;
            fadeDelay *= critLifetime;
            fadeDuration *= critLifetime;
            initialScale *= critScale;
            riseSpeed *= critRise;
            scalePunch *= critPunch;
        }
        // Lab's expanded experiment ranges include zero lifetime/fade duration.
        feelLifetime = Mathf.Max(.01f, feelLifetime);
        fadeDelay = Mathf.Clamp(fadeDelay, 0f, feelLifetime);
        fadeDuration = Mathf.Max(.01f, fadeDuration);
        velocity = new Vector2(drift + Random.Range(-driftRandomness, driftRandomness), riseSpeed);
        feelOrigin = transform.position;
        feelBaseScale = authoredScale * initialScale;
        transform.localScale = feelBaseScale * (1f + scalePunch);
        transform.localRotation = authoredRotation * Quaternion.Euler(0f, 0f,
            rotation + Random.Range(-rotationRandomness, rotationRandomness));
        feelColor = text.color;
        if (spawnDelay > 0f)
        {
            Color hidden = feelColor;
            hidden.a = 0f;
            text.color = hidden;
        }
    }

    private void UpdateFeel()
    {
        feelAge += Time.deltaTime;
        float age = feelAge - spawnDelay;
        if (age < 0f) return;
        transform.position = feelOrigin + (Vector3)velocity * age -
            Vector3.up * (.5f * damageGravity * age * age);
        float envelope = 1f - Mathf.Clamp01(age / damagePunchDuration);
        transform.localScale = feelBaseScale * (1f + scalePunch * envelope * envelope);
        Color color = feelColor;
        color.a *= 1f - Mathf.Clamp01((age - fadeDelay) / fadeDuration);
        text.color = color;
        if (age >= feelLifetime)
        {
            feelActive = false;
            Release();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static float LabAdjustment(float authored, CombatFeelParameter parameter)
    {
        float neutral = CombatFeelParameterDefaults.Get(parameter);
        float value = PhysicalCombatFeedbackRuntime.GetLabValue(parameter);
        return neutral == 0f ? authored + value : authored * value / neutral;
    }
#endif
}
