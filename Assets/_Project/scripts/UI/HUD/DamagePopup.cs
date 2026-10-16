using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    private const float Lifetime = 1f;
    private TextMeshPro text;
    private PooledGameObject pooledObject;
    private Color authoredColor;
    private float authoredFontSize;
    private int authoredSortingLayer;
    private int authoredSortingOrder;
    private float timer = Lifetime;
    private float floatSpeed = 1.5f;
    private bool healing;
    private float healingRise;
    private const float HealingLifetime = 0.8f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugDamageNumbersVisible { get; set; } = true;
    private bool isDamageNumber;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDebugVisibility() => DebugDamageNumbersVisible = true;
    private void LateUpdate()
    {
        if (text != null) text.renderer.enabled = !isDamageNumber || DebugDamageNumbersVisible;
    }
    private Vector3 authoredScale;
    private Quaternion authoredRotation;
    private float feelLifetime = Lifetime;
    private float feelAge;
    private float fadeDelay;
    private float fadeDuration;
    private float spawnDelay;
    private Vector2 velocity;
    private Vector3 feelBaseScale;
    private bool critical;
    private bool feelActive;
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
            authoredSortingLayer = text.renderer.sortingLayerID;
            authoredSortingOrder = text.renderer.sortingOrder;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        authoredScale = transform.localScale;
        authoredRotation = transform.localRotation;
#endif
    }

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        isDamageNumber = false;
        if (text != null) text.renderer.enabled = true;
#endif
        timer = Lifetime;
        healing = false;
        if (text != null)
        {
            text.color = authoredColor;
            text.fontSize = authoredFontSize;
            text.renderer.sortingLayerID = authoredSortingLayer;
            text.renderer.sortingOrder = authoredSortingOrder;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transform.localScale = authoredScale;
        transform.localRotation = authoredRotation;
        feelActive = false;
#endif
    }

    internal void ConfigurePoolHandle(PooledGameObject item) => pooledObject = item;

    public void SetDamage(int damage, bool isCritical = false)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        isDamageNumber = true;
        if (text != null) text.renderer.enabled = DebugDamageNumbersVisible;
#endif
        if (text != null)
        {
            text.text = damage.ToString();
            if (isCritical)
            {
                text.color = Color.red;
                text.fontSize *= 1.2f;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ConfigureFeel(isCritical);
#endif
    }

    public void SetRewardMultiplier(float multiplier, Color color)
    {
        if (text == null) return;
        text.text = string.Format(LocalizationService.EnsureExists().Get("hud.goldMultiplier"), multiplier);
        text.color = color;
        text.fontSize *= 1.15f;
    }

    public void SetHealing(float amount)
    {
        if (text == null) return;
        healing = true;
        timer = HealingLifetime;
        text.text = "+" + amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        text.color = new Color(0.25f, 1f, 0.35f, 1f);
        text.renderer.sortingLayerID = SortingLayer.NameToID("UI");
        text.renderer.sortingOrder = 100;
        // World-space TMP, with a rise matching 32 screen pixels at pickup time.
        Camera camera = Camera.main;
        Vector3 origin = camera != null ? camera.WorldToScreenPoint(transform.position) : Vector3.zero;
        healingRise = camera != null
            ? Vector3.Distance(camera.ScreenToWorldPoint(origin),
                camera.ScreenToWorldPoint(origin + Vector3.up * 32f))
            : 0.5f;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (feelActive)
        {
            UpdateFeel();
            return;
        }
#endif
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        timer -= Time.deltaTime;
        if (timer <= 0f) Release();
    }

    private void Release()
    {
        if (pooledObject == null || !pooledObject.Release())
            Destroy(gameObject);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static float V(CombatFeelParameter parameter) =>
        PhysicalCombatFeedbackRuntime.GetLabValue(parameter);

    private void ConfigureFeel(bool isCritical)
    {
        if (!PhysicalCombatFeedbackRuntime.LabAvailable || text == null) return;
        feelActive = true;
        critical = isCritical;
        feelAge = 0f;
        feelLifetime = V(CombatFeelParameter.PopupLifetime) *
            (critical ? V(CombatFeelParameter.CritPopupLifetime) : 1f);
        fadeDelay = Mathf.Min(feelLifetime, V(CombatFeelParameter.PopupFadeDelay));
        fadeDuration = Mathf.Max(.01f, V(CombatFeelParameter.PopupFadeDuration));
        spawnDelay = V(CombatFeelParameter.PopupDelay);
        velocity = new Vector2(
            V(CombatFeelParameter.PopupHorizontalDrift) + Random.Range(-1f, 1f) *
                V(CombatFeelParameter.PopupDriftRandomness),
            V(CombatFeelParameter.PopupRiseSpeed) *
                (critical ? V(CombatFeelParameter.CritPopupRise) : 1f));
        transform.localRotation = authoredRotation * Quaternion.Euler(0f, 0f,
            V(CombatFeelParameter.PopupRotation) + Random.Range(-1f, 1f) *
                V(CombatFeelParameter.PopupRotationRandomness));
        feelBaseScale = authoredScale * V(CombatFeelParameter.PopupInitialScale) *
            (critical ? V(CombatFeelParameter.CritPopupScale) / 1.2f : 1f);
        transform.localScale = feelBaseScale;
        if (spawnDelay > 0f)
        {
            Color hidden = text.color;
            hidden.a = 0f;
            text.color = hidden;
        }
    }

    private void UpdateFeel()
    {
        feelAge += Time.deltaTime;
        if (feelAge < spawnDelay) return;
        float age = feelAge - spawnDelay;
        Color color = text.color;
        if (color.a <= 0f) color.a = critical ? 1f : authoredColor.a;
        transform.position += (Vector3)velocity * Time.deltaTime;
        float punch = V(CombatFeelParameter.PopupScalePunch) *
            (critical ? V(CombatFeelParameter.CritPopupPunch) : 1f);
        float envelope = 1f - Mathf.Clamp01(age / .18f);
        transform.localScale = feelBaseScale * (1f + punch * envelope);
        if (age >= fadeDelay)
            color.a *= 1f - Mathf.Clamp01((age - fadeDelay) / fadeDuration);
        text.color = color;
        if (age >= feelLifetime)
        {
            feelActive = false;
            Release();
        }
    }
#endif
}
