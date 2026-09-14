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
        if (!IsScreenOverlay)
            transform.position = cameraPosition;
        float height = view.orthographicSize * 2f;
        float width = height * view.aspect;
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
