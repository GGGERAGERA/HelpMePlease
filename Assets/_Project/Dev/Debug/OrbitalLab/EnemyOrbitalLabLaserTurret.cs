#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// No production enemy archetype: this temporary incident actor exists only in EnemyOrbitalLab.
public sealed class EnemyOrbitalLabLaserTurret : MonoBehaviour
{
    public const float ChargeSeconds = 2f, FireSeconds = .65f, BeamWidth = .46f;
    public EnemyHealth Health { get; private set; }
    public bool Firing { get; private set; }
    public Vector2 Direction { get; private set; }
    public Vector2 Origin => (Vector2)transform.position + Direction * .72f;
    public Vector2 End => Origin + Direction * 70f;
    EnemyOrbitalLabController lab;
    LineRenderer warning, beam, core, charge;
    Transform healthBar;
    float elapsed;
    bool finished, contacted;

    public void Initialize(EnemyOrbitalLabController owner, Vector2 direction, Sprite sprite)
    {
        lab = owner; Direction = direction;
        Health = GetComponent<EnemyHealth>();
        Health.DeathFeedback += Destroyed;
        var view = new GameObject("Pixel turret"); view.transform.SetParent(transform, false);
        view.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        var renderer = view.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sharedMaterial = owner.Station.VisualMaterial; renderer.sortingLayerName = "Player"; renderer.sortingOrder = 124;
        warning = Line("Red warning", new Color(1f, .12f, .15f, .7f), .05f, 126);
        beam = Line("Laser edge", new Color(1f, .06f, .1f), BeamWidth, 127);
        core = Line("Laser core", new Color(1f, .92f, .82f), .13f, 128);
        charge = Line("Charge gauge", new Color(1f, .22f, .15f), .11f, 129);
        beam.enabled = core.enabled = false;
        var bg = Line("HP background", new Color(.07f, .09f, .13f), .11f, 128);
        bg.SetPosition(0, transform.position + new Vector3(-.6f, .9f)); bg.SetPosition(1, transform.position + new Vector3(.6f, .9f));
        var hp = Line("HP", new Color(.3f, 1f, .66f), .07f, 129); healthBar = hp.transform;
        Refresh(0);
    }
    LineRenderer Line(string name, Color color, float width, int order)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 2;
        line.sharedMaterial = lab.Station.VisualMaterial; line.startColor = line.endColor = color;
        line.widthMultiplier = width; line.sortingLayerName = "Player"; line.sortingOrder = order;
        line.numCapVertices = 0;
        line.SetPosition(0, Origin); line.SetPosition(1, End); return line;
    }
    public float DistanceToBeam(Vector2 point)
    {
        Vector2 segment = End - Origin;
        return Vector2.Distance(point, Origin + segment * Mathf.Clamp01(Vector2.Dot(point - Origin, segment) / segment.sqrMagnitude));
    }
    public void Tick(float dt)
    {
        if (finished || Health == null || Health.IsDead) { Cancel(); return; }
        elapsed += dt;
        if (!Firing && elapsed >= ChargeSeconds)
        { Firing = true; lab.RecordLaserShot(); warning.enabled = charge.enabled = false; beam.enabled = core.enabled = true; }
        if (Firing)
        {
            // One contact per burst, plus a shared cooldown across simultaneous turrets.
            var collider = lab.Player.GetComponent<Collider2D>();
            Vector2 point = collider != null ? collider.bounds.center : lab.Player.transform.position;
            float radius = collider != null ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : .4f;
            if (!contacted && DistanceToBeam(point) <= BeamWidth * .5f + radius)
            { contacted = true; lab.ApplyLaserContact(Direction); }
            if (elapsed >= ChargeSeconds + FireSeconds) { Cancel(); Destroy(gameObject); }
        }
        else Refresh(Mathf.Clamp01(elapsed / ChargeSeconds));
    }
    void Refresh(float progress)
    {
        warning.widthMultiplier = Mathf.Lerp(.05f, .13f, progress);
        warning.startColor = warning.endColor = new Color(1f, .08f, .12f, Mathf.Lerp(.55f, 1f, progress));
        charge.SetPosition(0, transform.position + new Vector3(-.6f, -.85f));
        charge.SetPosition(1, transform.position + new Vector3(-.6f + 1.2f * Mathf.Max(.02f, progress), -.85f));
        var hp = healthBar.GetComponent<LineRenderer>();
        hp.SetPosition(0, transform.position + new Vector3(-.6f, .9f));
        hp.SetPosition(1, transform.position + new Vector3(-.6f + 1.2f * Health.CurrentHealth / Health.MaxHealth, .9f));
    }
    void Destroyed() { lab.RecordTurretDestroyed(); Cancel(); }
    public void Cancel()
    {
        finished = true; Firing = false;
        if (warning != null) warning.enabled = false;
        if (beam != null) beam.enabled = false;
        if (core != null) core.enabled = false;
        if (charge != null) charge.enabled = false;
    }
    void OnDestroy() { if (Health != null) Health.DeathFeedback -= Destroyed; }
}
#endif
