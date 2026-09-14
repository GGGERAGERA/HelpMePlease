using UnityEngine;

/// <summary>Camera coverage and rule state for authored FX. Particle appearance lives in the prefab.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)] // Follow the camera after CameraFollow.LateUpdate.
public sealed class WorldRuleAuthoredVisual : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] layers = System.Array.Empty<ParticleSystem>();
    [Tooltip("Rotate the authored +X particle flow to the authoritative Wind direction.")]
    [SerializeField] private bool directional;
    [Tooltip("Extra coverage beyond each camera edge, in world units.")]
    [SerializeField] private float coveragePadding = 2f;
    [SerializeField] private CanvasGroup vignette;
    [SerializeField] private MeshRenderer wetGround;
    [Header("Golden enemy presentation (no gameplay modifiers)")]
    [SerializeField] private bool tintEnemies;
    [SerializeField] private Color enemyTint = new Color(1f, 0.78f, 0.32f, 1f);

    [Header("Snow authored resources")]
    public MeshRenderer snowGround;
    public UnityEngine.Rendering.Volume snowVolume;
    public GameObject snowParticles;
    [Header("Snow presentation tuning")]
    [Range(0f, 1f)] public float snowVisualIntensity = 0.58f;
    [Range(0f, 1f)] public float snowVolumeWeight = 1f;
    [Range(0f, 0.25f)] public float snowScreenOpacity = 0.04f;
    [Range(0f, 4f)] public float blizzardIntensity = 0.32f;
    [Range(0f, 16f)] public float blizzardLineDensity = 8f;
    [Range(0f, 4f)] public float blizzardLineSpeed = 1.4f;
    [Range(0f, 4f)] public float blizzardVeil = 0.14f;
    [Range(0f, 4f)] public float snowParticleEmissionMultiplier = 1.8f;
    [Min(0f)] public float snowFallSpeedMultiplier = 1.35f;
    [Header("Darkness world veil (below gameplay layers)")]
    [SerializeField] private SpriteRenderer darknessVeil;
    public UnityEngine.Rendering.Universal.Light2D darknessShotReveal;
    [Range(0f, 1f)] public float darknessGlobalIntensity = 0.05f;
    [SerializeField, Range(0f, 1f)] private float darknessOpacity = 0.985f;

    private Camera view;
    private float[] emissionMultipliers;
    private MaterialPropertyBlock wetProperties;
    private bool enemiesBound;

    // A screen-space Canvas must remain a scene root, outside moving world transforms.
    public bool IsScreenOverlay => vignette != null;

    public void Bind(Camera camera)
    {
        view = camera;
        emissionMultipliers = new float[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            emissionMultipliers[i] = layers[i].emission.rateOverTimeMultiplier;
        if (wetGround != null) wetProperties = new MaterialPropertyBlock();
    }

    public void SetRuleActive(bool active, Vector2 direction)
    {
        if (directional && direction.sqrMagnitude > 0f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        if (active == gameObject.activeSelf) return;
        gameObject.SetActive(active);
        if (!active) return;
        FollowCamera();
        foreach (var layer in layers) layer.Play(false);
        if (tintEnemies)
        {
            enemiesBound = true;
            EnemyHealth.Spawned += TintEnemy;
            foreach (var enemy in EnemyHealth.ActiveInstances) TintEnemy(enemy);
        }
    }

    public void SetAmount(float amount)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            var emission = layers[i].emission;
            emission.rateOverTimeMultiplier = emissionMultipliers[i] * amount;
        }
    }

    public void SetIntensity(float intensity)
    {
        if (darknessVeil != null)
            darknessVeil.color = new Color(0f, 0f, 0f, darknessOpacity * intensity);
        if (vignette != null) vignette.alpha = intensity;
        if (wetGround == null) return;
        wetGround.GetPropertyBlock(wetProperties);
        wetProperties.SetFloat("_WetGroundIntensity", wetGround.sharedMaterial.GetFloat("_WetGroundIntensity") * intensity);
        wetProperties.SetFloat("_VisualTime", Time.unscaledTime);
        wetGround.SetPropertyBlock(wetProperties);
    }

    private void TintEnemy(EnemyHealth enemy) =>
        EnemyAnomalyEffects.GetOrCreate(enemy).SetWorldRuleTint(enemyTint);

    private void LateUpdate() => FollowCamera();

    private void FollowCamera()
    {
        if (view == null) return;
        Vector3 cameraPosition = new Vector3(view.transform.position.x, view.transform.position.y, 0f);
        Vector3 revealPosition = darknessShotReveal != null ? darknessShotReveal.transform.position : Vector3.zero;
        if (!IsScreenOverlay && snowParticles == null)
            transform.position = cameraPosition;
        if (darknessShotReveal != null && darknessShotReveal.enabled)
            darknessShotReveal.transform.position = revealPosition;
        float height = view.orthographicSize * 2f;
        float width = height * view.aspect;
        if (darknessVeil != null)
            darknessVeil.transform.localScale = new Vector3(
                (width + 2f) / darknessVeil.sprite.bounds.size.x,
                (height + 2f) / darknessVeil.sprite.bounds.size.y, 1f);
        // Project the viewport onto the emitter's local axes, including diagonal wind.
        Vector3 right = transform.right, up = transform.up;
        float localWidth = Mathf.Abs(right.x) * width + Mathf.Abs(right.y) * height;
        float localHeight = Mathf.Abs(up.x) * width + Mathf.Abs(up.y) * height;
        foreach (var layer in layers)
        {
            // The Golden prefab owns both spaces: keep its Canvas fixed and move
            // only native particles (authored with Local scaling, World simulation).
            if (IsScreenOverlay)
                layer.transform.position = cameraPosition;
            var shape = layer.shape;
            shape.scale = new Vector3(localWidth + coveragePadding * 2f, localHeight + coveragePadding * 2f, 0f);
        }
        if (wetGround != null)
            wetGround.transform.localScale = new Vector3(width + 2f, height + 2f, 1f);
    }

    private void OnDisable()
    {
        foreach (var layer in layers)
            layer.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (!enemiesBound) return;
        EnemyHealth.Spawned -= TintEnemy;
        enemiesBound = false;
        foreach (var enemy in EnemyHealth.ActiveInstances)
        {
            var effects = enemy.GetComponent<EnemyAnomalyEffects>();
            if (effects != null) effects.SetWorldRuleTint(Color.white);
            var golden = enemy.GetComponent<GoldenEnemyModifier>();
            if (golden != null) golden.ClearRulePresentation();
        }
    }
}
