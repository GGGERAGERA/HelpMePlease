using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// The lab's one-use timer, shared with production. Damage and durability stay in EnemyDirectionalShield.
public class EnemyTacticalTimedShield : MonoBehaviour
{
    public const float WarningSeconds = .4f;
    public EnemyDirectionalShield ShieldTemplate;
    [Range(0f, 1f)] public float SpawnChance;
    public float ActivationDelay { get; private set; }
    public float Age { get; private set; }
    public bool Activated { get; private set; }
    public bool Warning { get; private set; }
    [field: SerializeField] public EnemyDirectionalShield Shield { get; private set; }
    EnemyHealth health;
    bool configured;
    protected virtual bool ShouldTick => true;

    protected void Start()
    {
        if (configured) return;
        health = GetComponent<EnemyHealth>();
        if (Shield == null || Random.value >= SpawnChance) { enabled = false; return; }
        ActivationDelay = Random.Range(3f, 6f);
    }
    public void Initialize(EnemyDirectionalShield source, float delay)
    {
        Shield = GetComponent<EnemyDirectionalShield>();
        if (Shield != null)
        {
            health = GetComponent<EnemyHealth>(); ActivationDelay = Mathf.Clamp(delay, 3f, 6f); configured = true;
            return;
        }
        if (source == null) return;
        // Assemble before the shield's first OnEnable: no hidden protection.
        // Only the shield starts at activation; the AI and health are never restarted then.
        gameObject.SetActive(false);
        var shield = gameObject.AddComponent<EnemyDirectionalShield>();
        shield.enabled = false;
        shield.Kind = EnemyDirectionalShield.ShieldKind.Breakable;
        shield.MaximumBlockedHits = 2;
        shield.ProtectedSector = source.ProtectedSector; shield.TurnSpeed = source.TurnSpeed;
        shield.ShieldDistance = source.ShieldDistance; shield.LocalShieldCenter = source.LocalShieldCenter;
        shield.ShieldThickness = source.ShieldThickness;
        shield.ShieldPivot = Instantiate(source.ShieldPivot, transform, false);
        var lines = shield.ShieldPivot.GetComponentsInChildren<LineRenderer>(true);
        shield.ShieldArc = lines.First(line => line.name == source.ShieldArc.name);
        shield.ShieldCollider = shield.ShieldPivot.GetComponentInChildren<EdgeCollider2D>(true);
        shield.DamageCracks = source.DamageCracks.Select(crack => lines.First(line => line.name == crack.name)).ToArray();
        shield.BodySorting = GetComponentInChildren<SortingGroup>(true);
        shield.BodySorting.enabled = true;
        shield.BlockSparks = Instantiate(source.BlockSparks, transform, false);
        shield.BreakShards = Instantiate(source.BreakShards, transform, false);
        shield.FeedbackSource = Instantiate(source.FeedbackSource, transform, false);
        shield.BreakSound = source.BreakSound;
        foreach (var line in lines) line.enabled = false;
        shield.ShieldCollider.enabled = false;
        Shield = shield; health = GetComponent<EnemyHealth>();
        ActivationDelay = Mathf.Clamp(delay, 3f, 6f);
        configured = true;
        gameObject.SetActive(true);
    }
    protected void Update()
    {
        if (Activated || health == null || health.IsDead || !ShouldTick) return;
        Age += Time.deltaTime;
        if (Age >= ActivationDelay)
        {
            Warning = false; Activated = true;
            Shield.enabled = true;
            enabled = false;
        }
        else if (Age >= ActivationDelay - WarningSeconds)
        {
            Warning = true;
            Shield.Bind(PlayerRuntimeReference.CachedPlayer != null ? PlayerRuntimeReference.CachedPlayer.transform : null);
            Shield.ShieldCollider.enabled = false;
            Shield.ShieldArc.enabled = true;
            Shield.ShieldArc.widthMultiplier = Shield.ShieldThickness * .65f;
            float pulse = .55f + .4f * Mathf.Sin((Age - ActivationDelay + WarningSeconds) * 35f);
            Shield.ShieldArc.startColor = Shield.ShieldArc.endColor = new Color(1f, .8f, .22f, pulse);
        }
    }
}
