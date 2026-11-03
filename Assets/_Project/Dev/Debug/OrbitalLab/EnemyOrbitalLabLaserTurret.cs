#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// No production enemy archetype: this temporary incident actor exists only in EnemyOrbitalLab.
public sealed class EnemyOrbitalLabLaserTurret : MonoBehaviour
{
    public const float EnterSeconds = .55f, ChargeSeconds = 2f, FireSeconds = .65f, RetractSeconds = .55f, BeamWidth = .46f;
    const float SlideDistance = 2.6f;
    public enum AttackPhase { Entering, Charging, Firing, Retracting }
    public AttackPhase Phase { get; private set; }
    public EnemyHealth Health { get; private set; }
    public bool Firing => !finished && Phase == AttackPhase.Firing;
    public Vector2 Direction { get; private set; }
    public float HorizontalOffset { get; private set; }
    public float FixedY { get; private set; }
    public bool LeftSide { get; private set; }
    public Vector2 AttackPosition => new(lab.Player.transform.position.x + HorizontalOffset, FixedY);
    public Vector2 Origin => (Vector2)transform.position + Direction * .72f;
    public Vector2 End => Origin + Direction * 70f;
    EnemyOrbitalLabController lab;
    LineRenderer warning, beam, core, charge, healthBar, healthBackground;
    Rigidbody2D body;
    float phaseTime;
    bool finished, contacted;

    public void Initialize(EnemyOrbitalLabController owner, Vector2 direction, Sprite sprite, bool leftSide, Vector2 attackPosition)
    {
        lab = owner; Direction = direction;
        LeftSide = leftSide; FixedY = attackPosition.y;
        HorizontalOffset = attackPosition.x - owner.Player.transform.position.x;
        body = GetComponent<Rigidbody2D>(); Phase = AttackPhase.Entering;
        MoveWithPlayer(SlideDistance);
        Health = GetComponent<EnemyHealth>();
        Health.DeathFeedback += Destroyed;
        var view = new GameObject("Pixel turret"); view.transform.SetParent(transform, false);
        view.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        var renderer = view.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        // Keep SpriteRenderer's own sprite material; the station's line material can
        // carry a different texture through the live visual comparison tools.
        renderer.sortingLayerName = "Player"; renderer.sortingOrder = 124;
        if(owner.CreateArtistLaserTurretVisual(view.transform)) renderer.enabled=false;
        warning = Line("Red warning", new Color(1f, .12f, .15f, .5f), BeamWidth, 126);
        beam = Line("Laser edge", new Color(1f, .06f, .1f), BeamWidth, 127);
        core = Line("Laser core", new Color(1f, .92f, .82f), .13f, 128);
        charge = Line("Charge gauge", new Color(1f, .22f, .15f), .11f, 129);
        warning.enabled = beam.enabled = core.enabled = charge.enabled = false;
        healthBackground = Line("HP background", new Color(.07f, .09f, .13f), .11f, 128);
        healthBar = Line("HP", new Color(.3f, 1f, .66f), .07f, 129);
        RefreshGeometry(); RefreshCharge(0);
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
        return DistanceToRay(point, Origin);
    }
    public float DistanceToPlannedBeam(Vector2 point) => DistanceToRay(point, AttackPosition + Direction * .72f);
    float DistanceToRay(Vector2 point, Vector2 origin)
    {
        Vector2 segment = Direction * 70f;
        return Vector2.Distance(point, origin + segment * Mathf.Clamp01(Vector2.Dot(point - origin, segment) / segment.sqrMagnitude));
    }
    void MoveWithPlayer(float slide)
    {
        Vector2 position = AttackPosition + Vector2.right * (LeftSide ? -slide : slide);
        // Lab owns the actor. No camera visibility check and no following on Y.
        body.position = position; transform.position = position;
    }
    public void Tick(float dt)
    {
        if (finished || Health == null || Health.IsDead) { Cancel(); return; }
        phaseTime += dt;
        switch (Phase)
        {
            case AttackPhase.Entering:
                MoveWithPlayer(SlideDistance * (1f - Mathf.SmoothStep(0, 1, phaseTime / EnterSeconds)));
                if (phaseTime >= EnterSeconds)
                { Phase = AttackPhase.Charging; phaseTime = 0; warning.enabled = charge.enabled = true; }
                break;
            case AttackPhase.Charging:
                MoveWithPlayer(0);
                if (phaseTime >= ChargeSeconds)
                {
                    Phase = AttackPhase.Firing; phaseTime = 0; lab.RecordLaserShot();
                    warning.enabled = charge.enabled = false; beam.enabled = core.enabled = true;
                }
                break;
            case AttackPhase.Firing:
                MoveWithPlayer(0);
                if (phaseTime >= FireSeconds)
                { Phase = AttackPhase.Retracting; phaseTime = 0; beam.enabled = core.enabled = false; }
                break;
            case AttackPhase.Retracting:
                MoveWithPlayer(SlideDistance * Mathf.SmoothStep(0, 1, phaseTime / RetractSeconds));
                if (phaseTime >= RetractSeconds) { Cancel(); Destroy(gameObject); }
                break;
        }
        // Rendered lines and damage read the same moved world segment in LateUpdate.
        RefreshGeometry();
        if (Phase == AttackPhase.Charging) RefreshCharge(Mathf.Clamp01(phaseTime / ChargeSeconds));
        if (Firing)
        {
            // One contact per burst, plus a shared cooldown across simultaneous turrets.
            var collider = lab.Player.GetComponent<Collider2D>();
            Vector2 point = collider != null ? collider.bounds.center : lab.Player.transform.position;
            float radius = collider != null ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : .4f;
            if (!contacted && DistanceToBeam(point) <= BeamWidth * .5f + radius)
            { contacted = true; lab.ApplyLaserContact(Direction); }
        }
    }
    void RefreshGeometry()
    {
        foreach (var line in new[] { warning, beam, core })
        { line.SetPosition(0, Origin); line.SetPosition(1, End); }
        healthBackground.SetPosition(0, transform.position + new Vector3(-.6f, .9f));
        healthBackground.SetPosition(1, transform.position + new Vector3(.6f, .9f));
        healthBar.SetPosition(0, transform.position + new Vector3(-.6f, .9f));
        healthBar.SetPosition(1, transform.position + new Vector3(-.6f + 1.2f * Health.CurrentHealth / Health.MaxHealth, .9f));
    }
    void RefreshCharge(float progress)
    {
        // The complete future damage width is visible throughout charging.
        warning.startColor = warning.endColor = new Color(1f, .08f, .12f, Mathf.Lerp(.35f, .85f, progress));
        charge.SetPosition(0, transform.position + new Vector3(-.6f, -.85f));
        charge.SetPosition(1, transform.position + new Vector3(-.6f + 1.2f * Mathf.Max(.02f, progress), -.85f));
    }
    void Destroyed()
    {
        lab.RecordTurretDestroyed();
        var effect = new GameObject("Interception · pixel destruction");
        effect.transform.SetParent(lab.transform, true); effect.transform.position = transform.position;
        effect.AddComponent<EnemyOrbitalLabTurretDestruction>().Initialize();
        Cancel();
    }
    public void Cancel()
    {
        finished = true;
        if (warning != null) warning.enabled = false;
        if (beam != null) beam.enabled = false;
        if (core != null) core.enabled = false;
        if (charge != null) charge.enabled = false;
    }
    void OnDestroy() { if (Health != null) Health.DeathFeedback -= Destroyed; }
}

// A short scene-owned effect survives EnemyHealth's ordinary destruction of its actor.
public sealed class EnemyOrbitalLabTurretDestruction : MonoBehaviour
{
    readonly SpriteRenderer[] pieces = new SpriteRenderer[7];
    readonly Vector2[] velocities = new Vector2[7];
    Sprite pixel;
    float elapsed;
    public void Initialize()
    {
        pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        for (int i = 0; i < pieces.Length; i++)
        {
            var shard = new GameObject("Pixel debris"); shard.transform.SetParent(transform, false);
            pieces[i] = shard.AddComponent<SpriteRenderer>(); pieces[i].sprite = pixel;
            pieces[i].sortingLayerName = "Player"; pieces[i].sortingOrder = 130;
            shard.transform.localScale = i == 0 ? new Vector3(.8f, .55f, 1) : new Vector3(.15f, .1f, 1);
            float angle = i * Mathf.PI * 2f / (pieces.Length - 1);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (1.5f + i * .17f);
        }
    }
    void Update()
    {
        elapsed += Time.deltaTime;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null) continue;
            pieces[i].transform.localPosition = velocities[i] * elapsed;
            pieces[i].transform.localRotation = Quaternion.Euler(0, 0, i * 41 + elapsed * 180);
            float alpha = Mathf.Clamp01(1f - elapsed / (i == 0 ? .13f : .45f));
            pieces[i].color = i % 2 == 0 ? new Color(1f, .48f, .16f, alpha) : new Color(.65f, .75f, .8f, alpha);
        }
        if (elapsed >= .45f) Destroy(gameObject);
    }
    void OnDestroy() { if (pixel != null) Destroy(pixel); }
}
#endif
