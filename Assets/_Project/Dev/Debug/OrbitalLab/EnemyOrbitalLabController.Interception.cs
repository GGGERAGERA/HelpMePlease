#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

// Explicitly scene-local hypothesis. WorldSystemsLab also borrows this controller.
public sealed partial class EnemyOrbitalLabController
{
    public bool HasInterceptionExperiment => gameObject.scene.path == ScenePath && AnomalyExperiment == null;
    public bool DensityControl { get; private set; }
    public int TargetEnemyDensity { get; private set; }
    public bool RandomEnemyTypes = true;
    public float DensitySpawnRate = 25f;
    public bool AutomaticInterceptions = true;
    public float InterceptionMinimumInterval = 9f, InterceptionMaximumInterval = 16f;
    public enum InterceptionVariant { Single, Sequential, Dual }
    public InterceptionVariant ActiveInterceptionVariant { get; private set; }
    public bool InterceptionActive { get; private set; }
    public int LaserShots { get; private set; }
    public int LaserTurretsDestroyed { get; private set; }
    public int LaserDamageContacts { get; private set; }
    public IReadOnlyList<EnemyOrbitalLabLaserTurret> LaserTurrets => laserTurrets;
    public string InterceptionLabel => InterceptionActive ? ActiveInterceptionVariant switch
    { InterceptionVariant.Single => "Одиночный", InterceptionVariant.Sequential => "Последовательный", _ => "Перекрёстный" } : "Обычный бой";
    readonly System.Random incidentRandom = new();
    readonly List<int> densityTypes = new(), densityBag = new();
    readonly List<EnemyOrbitalLabLaserTurret> laserTurrets = new();
    readonly Dictionary<GameObject, SimplePrefabPool> labEnemyProjectilePools = new();
    float densityCredit, nextIncident = 10f, sequentialDelay, laserDamageReadyAt;
    bool sequentialPending, firstFromLeft;
    Texture2D turretTexture;
    Sprite turretSprite;
    GUIStyle incidentHudStyle, incidentToggleStyle;
    static readonly int[] DensityPresets = { 25, 50, 100, 150 };
    Vector2 safeEscape;
    public Vector2 InterceptionSafePoint => safeEscape;

    public void SetTargetEnemyDensity(int count)
    {
        if (!HasInterceptionExperiment || !Ready) return;
        DensityControl = true; SandboxSpawning = false;
        TargetEnemyDensity = Mathf.Clamp(count, 0, 150);
        densityCredit = 0f;
        SandboxMessage = $"Цель: {TargetEnemyDensity} живых. Пополнение: {DensitySpawnRate:0}/с.";
    }

    public void ClearDensityEnemies()
    {
        SetTargetEnemyDensity(0);
        ClearSandboxArena();
    }

    void BuildDensityBag()
    {
        densityTypes.Clear(); densityBag.Clear();
        var prefabs = new HashSet<GameObject>();
        for (int i = 0; EnemyPrefabs != null && i < EnemyPrefabs.Length; i++)
            if (EnemyPrefabs[i] != null && EnemyPrefabs[i].GetComponent<EnemyHealth>() != null && prefabs.Add(EnemyPrefabs[i])) densityTypes.Add(i);
        Add(-3, PursuerPrefab); Add(-4, BreakableShieldBearerPrefab);
        Add(-6, BreakableShieldShooterPrefab); Add(-1, TestShooterPrefab);
        densityBag.AddRange(densityTypes);
        // Shuffle bags guarantee variety, including rare lab actors, rather than relying on luck.
        for (int i = densityBag.Count - 1; i > 0; i--)
        { int j = incidentRandom.Next(i + 1); (densityBag[i], densityBag[j]) = (densityBag[j], densityBag[i]); }
        void Add(int type, GameObject prefab)
        { if (prefab != null && prefab.GetComponent<EnemyHealth>() != null && prefabs.Add(prefab)) densityTypes.Add(type); }
    }

    void SpawnDensityEnemy()
    {
        int type;
        if (RandomEnemyTypes)
        {
            if (densityBag.Count == 0) BuildDensityBag();
            if (densityBag.Count == 0) return;
            int last = densityBag.Count - 1; type = densityBag[last]; densityBag.RemoveAt(last);
        }
        else
        {
            if (EnemyPrefabs == null || EnemyPrefabs.Length == 0) return;
            type = Mathf.Clamp(SelectedEnemy, 0, EnemyPrefabs.Length - 1);
            if (EnemyPrefabs[type] == null) return;
        }
        float angle = (float)incidentRandom.NextDouble() * Mathf.PI * 2f;
        float radius = 7f + (float)incidentRandom.NextDouble() * 4f;
        Vector2 position = (Vector2)Player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        // Reflect at arena boundaries instead of piling new actors onto one clamped point.
        if (Mathf.Abs(position.x) > 28) position.x = Mathf.Sign(position.x) * (56f - Mathf.Abs(position.x));
        if (Mathf.Abs(position.y) > 28) position.y = Mathf.Sign(position.y) * (56f - Mathf.Abs(position.y));
        SpawnAt(type, position);
    }

    void ConfigureLabEnemyProjectilePool(GameObject instance)
    {
        if (!HasInterceptionExperiment) return;
        if (instance.TryGetComponent<EnemyShooterMovement>(out var shooter)) shooter.SetProjectilePool(Pool(shooter.ProjectilePrefab));
        if (instance.TryGetComponent<TurretEnemyBehaviour>(out var turret)) turret.SetProjectilePool(Pool(turret.ProjectilePrefab));
        SimplePrefabPool Pool(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!labEnemyProjectilePools.TryGetValue(prefab, out var pool))
            { pool = new SimplePrefabPool(this, prefab, 0, 256); labEnemyProjectilePools.Add(prefab, pool); }
            return pool;
        }
    }

    void TickInterceptionExperiment()
    {
        if (!HasInterceptionExperiment || !Ready || Paused || Player.IsDead) return;
        float dt = Time.deltaTime;
        if (DensityControl)
        {
            int alive = AliveEnemies;
            // Retire excess actors gradually when lowering the target; this is not a kill/reward.
            for (int i = enemies.Count - 1, retired = 0; i >= 0 && alive > TargetEnemyDensity && retired < 2; i--)
                if (enemies[i] != null && !enemies[i].IsDead)
                { var enemy = enemies[i]; enemies.RemoveAt(i); enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); alive--; retired++; }
            densityCredit = alive < TargetEnemyDensity ? Mathf.Min(2f, densityCredit + dt * Mathf.Clamp(DensitySpawnRate, 1f, 60f)) : 0f;
            while (densityCredit >= 1f && alive < TargetEnemyDensity)
            { SpawnDensityEnemy(); densityCredit -= 1f; alive++; }
        }
        for (int i = laserTurrets.Count - 1; i >= 0; i--)
        {
            var turret = laserTurrets[i];
            if (turret == null) { laserTurrets.RemoveAt(i); continue; }
            turret.Tick(dt);
        }
        if (sequentialPending)
        {
            sequentialDelay -= dt;
            if (sequentialDelay <= 0f)
            { sequentialPending = false; SpawnLaserTurret(!firstFromLeft, 0f, .8f); }
        }
        if (InterceptionActive)
        {
            if (laserTurrets.Count == 0 && !sequentialPending)
            { InterceptionActive = false; ScheduleNextIncident(); }
        }
        else if (AutomaticInterceptions && AliveEnemies > 0)
        {
            nextIncident -= dt;
            if (nextIncident <= 0f) LaunchInterception();
        }
    }

    public void SetInterceptionIntervals(float minimum, float maximum)
    {
        InterceptionMinimumInterval = Mathf.Clamp(minimum, 4f, 40f);
        InterceptionMaximumInterval = Mathf.Clamp(maximum, InterceptionMinimumInterval, 60f);
        if (!InterceptionActive) ScheduleNextIncident();
    }
    void ScheduleNextIncident() => nextIncident = Mathf.Lerp(InterceptionMinimumInterval,
        Mathf.Max(InterceptionMinimumInterval, InterceptionMaximumInterval), (float)incidentRandom.NextDouble());

    public bool LaunchInterception() => LaunchInterception((InterceptionVariant)incidentRandom.Next(3));
    public bool LaunchInterception(InterceptionVariant variant)
    {
        if (!HasInterceptionExperiment || !Ready || Player.IsDead || InterceptionActive) return false;
        ActiveInterceptionVariant = variant; InterceptionActive = true;
        firstFromLeft = incidentRandom.Next(2) == 0;
        float slope = (float)incidentRandom.NextDouble() * 2f - 1f;
        SpawnLaserTurret(firstFromLeft, slope, -.35f);
        if (variant == InterceptionVariant.Sequential)
        { sequentialPending = true; sequentialDelay = 3.2f; }
        else if (variant == InterceptionVariant.Dual)
            SpawnLaserTurret(!firstFromLeft, -slope + (slope >= 0 ? -1.5f : 1.5f), 2.1f * (incidentRandom.Next(2) == 0 ? -1 : 1));
        if (!FindSafeEscape())
        { CancelInterception(); SandboxMessage = "Перехват отложен: нет безопасного выхода."; return false; }
        return true;
    }

    void SpawnLaserTurret(bool left, float verticalOffset, float aimOffset)
    {
        var camera = CameraRig.ControlledCamera;
        Vector2 edge = camera.ViewportToWorldPoint(new Vector3(left ? .035f : .965f, .5f, -camera.transform.position.z));
        Vector2 player = Player.transform.position;
        edge.y = Mathf.Clamp(player.y + verticalOffset * 2f, camera.ViewportToWorldPoint(new Vector3(0, .12f, -camera.transform.position.z)).y,
            camera.ViewportToWorldPoint(new Vector3(0, .88f, -camera.transform.position.z)).y);
        edge.x = Mathf.Clamp(edge.x, -27.5f, 27.5f); edge.y = Mathf.Clamp(edge.y, -27.5f, 27.5f);
        // A player hugging an edge must not get a turret directly on top of them.
        if (Vector2.Distance(edge, player) < 2f) edge.y = Mathf.Clamp(edge.y + (edge.y < player.y ? -3f : 3f), -27.5f, 27.5f);
        var go = new GameObject("Протокол Перехват · " + (left ? "L" : "R"));
        go.transform.SetParent(transform); go.transform.position = edge;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        go.layer = enemyLayer >= 0 ? enemyLayer : 0;
        var body = go.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
        var collider = go.AddComponent<CircleCollider2D>(); collider.radius = .55f; collider.isTrigger = true;
        var health = go.AddComponent<EnemyHealth>(); health.SetRuntimeMaxHealth(32f);
        var dummy = go.AddComponent<LabEnemyDummy>(); dummy.Initialize(); dummy.Invulnerable = false;
        var turret = go.AddComponent<EnemyOrbitalLabLaserTurret>();
        Vector2 direction = (player + Vector2.up * aimOffset - edge).normalized;
        if (ActiveInterceptionVariant == InterceptionVariant.Dual && laserTurrets.Count == 1)
        {
            Vector2 first = laserTurrets[0].Direction;
            // Opposite travel alone is not a different firing lane: keep at least a 12° crossing angle.
            if (Mathf.Abs(Vector2.Dot(first, direction)) > Mathf.Cos(12f * Mathf.Deg2Rad))
                direction = Quaternion.Euler(0, 0, incidentRandom.Next(2) == 0 ? 18 : -18) * -first;
        }
        turret.Initialize(this, direction, LaserTurretSprite());
        laserTurrets.Add(turret); health.NotifySpawnConfigured();
    }

    bool FindSafeEscape()
    {
        Vector2 player = Player.transform.position;
        var camera = CameraRig.ControlledCamera;
        for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                Vector2 point = player + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (1.5f + ring);
                if (Mathf.Abs(point.x) > 27 || Mathf.Abs(point.y) > 27) continue;
                Vector3 view = camera.WorldToViewportPoint(point);
                if (view.x < .06f || view.x > .94f || view.y < .08f || view.y > .92f) continue;
                bool clear = true;
                foreach (var turret in laserTurrets) if (turret != null && turret.DistanceToBeam(point) < 1.1f) { clear = false; break; }
                if (clear) { safeEscape = point; return true; }
            }
        return false;
    }

    public void CancelInterception()
    {
        foreach (var turret in laserTurrets) if (turret != null) { turret.Cancel(); Destroy(turret.gameObject); }
        laserTurrets.Clear(); sequentialPending = false; InterceptionActive = false; ScheduleNextIncident();
    }
    internal void RecordLaserShot() => LaserShots++;
    internal void RecordTurretDestroyed() => LaserTurretsDestroyed++;
    internal void ApplyLaserContact(Vector2 direction)
    {
        if (Time.time < laserDamageReadyAt) return;
        laserDamageReadyAt = Time.time + .75f; LaserDamageContacts++;
        Player.TakeDamage(18f, direction);
    }
    void DisposeInterceptionExperiment()
    {
        CancelInterception();
        foreach (var pool in labEnemyProjectilePools.Values) pool.Dispose();
        labEnemyProjectilePools.Clear();
        if (turretSprite != null) Destroy(turretSprite);
        if (turretTexture != null) Destroy(turretTexture);
    }

    Sprite LaserTurretSprite()
    {
        if (turretSprite != null) return turretSprite;
        const int w = 32, h = 24;
        turretTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            Color c = Color.clear;
            if (x >= 4 && x <= 21 && y >= 2 && y <= 21) c = new Color(.1f, .14f, .19f);
            if (x >= 6 && x <= 19 && y >= 4 && y <= 19) c = new Color(.34f, .42f, .48f);
            if (x >= 8 && x <= 18 && y >= 6 && y <= 17) c = new Color(.2f, .27f, .33f);
            if (x >= 17 && x <= 29 && y >= 8 && y <= 15) c = new Color(.08f, .11f, .16f);
            if (x >= 19 && x <= 28 && y >= 10 && y <= 13) c = new Color(.55f, .62f, .65f);
            if ((x == 7 || x == 18) && (y == 5 || y == 18)) c = new Color(.8f, .85f, .85f);
            if (x >= 10 && x <= 15 && y >= 9 && y <= 14 || x >= 28 && x <= 30 && y >= 9 && y <= 14) c = new Color(1f, .16f, .16f);
            if (x >= 1 && x <= 4 && (y >= 3 && y <= 7 || y >= 16 && y <= 20)) c = new Color(.55f, .6f, .62f);
            pixels[y * w + x] = c;
        }
        turretTexture.SetPixels(pixels); turretTexture.Apply();
        turretSprite = Sprite.Create(turretTexture, new Rect(0, 0, w, h), new Vector2(.42f, .5f), 24f);
        turretSprite.hideFlags = HideFlags.HideAndDontSave;
        return turretSprite;
    }

    internal void DrawInterceptionControls(GUIStyle label, GUIStyle small, GUIStyle button)
    {
        if (!HasInterceptionExperiment) return;
        if (incidentToggleStyle == null) incidentToggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 13, wordWrap = true };
        GUILayout.Space(8); GUILayout.Label("ПЛОТНОСТЬ ВРАГОВ", label);
        GUILayout.Label($"Живые: {AliveEnemies}/{TargetEnemyDensity}" + (DensityControl ? "" : " · сценарий"), small);
        int density = Mathf.RoundToInt(GUILayout.HorizontalSlider(TargetEnemyDensity, 0, 150));
        if (density != TargetEnemyDensity) SetTargetEnemyDensity(density);
        GUILayout.BeginHorizontal();
        foreach (int preset in DensityPresets)
            if (GUILayout.Button(preset.ToString(), button, GUILayout.Height(30))) SetTargetEnemyDensity(preset);
        GUILayout.EndHorizontal();
        RandomEnemyTypes = GUILayout.Toggle(RandomEnemyTypes, "Случайные типы", incidentToggleStyle);
        if (!RandomEnemyTypes && EnemyPrefabs != null && EnemyPrefabs.Length > 0)
        {
            SelectedEnemy = Mathf.Clamp(SelectedEnemy, 0, EnemyPrefabs.Length - 1);
            if (GUILayout.Button(EnemyPrefabs[SelectedEnemy].name + " →", button, GUILayout.Height(28))) SelectedEnemy = (SelectedEnemy + 1) % EnemyPrefabs.Length;
        }
        GUILayout.Label($"Появление: {DensitySpawnRate:0} врагов/с", small);
        DensitySpawnRate = Mathf.Round(GUILayout.HorizontalSlider(DensitySpawnRate, 1, 60));
        if (GUILayout.Button("Очистить врагов", button, GUILayout.Height(30))) ClearDensityEnemies();
        GUILayout.Space(6); GUILayout.Label("ПРОТОКОЛ: ПЕРЕХВАТ · ЭКСПЕРИМЕНТ", label);
        AutomaticInterceptions = GUILayout.Toggle(AutomaticInterceptions, "Автоматические лазерные инциденты", incidentToggleStyle);
        GUILayout.Label($"Интервал: {InterceptionMinimumInterval:0}–{InterceptionMaximumInterval:0} с после инцидента", small);
        float min = Mathf.Round(GUILayout.HorizontalSlider(InterceptionMinimumInterval, 4, 40));
        float max = Mathf.Round(GUILayout.HorizontalSlider(InterceptionMaximumInterval, min, 60));
        if (min != InterceptionMinimumInterval || max != InterceptionMaximumInterval) SetInterceptionIntervals(min, max);
        bool enabled = GUI.enabled; GUI.enabled = enabled && !InterceptionActive;
        if (GUILayout.Button("Запустить инцидент", button, GUILayout.Height(32))) LaunchInterception();
        GUI.enabled = enabled;
        GUILayout.Label("Вариант: " + InterceptionLabel, small);
        bool vulnerable = !PlayerInvincible;
        if (GUILayout.Toggle(vulnerable, "Получать урон (тренировка выключена)", incidentToggleStyle) != vulnerable) SetPlayerInvincible(vulnerable);
    }

    void DrawInterceptionHud()
    {
        if (!HasInterceptionExperiment || !Ready) return;
        float left = PanelVisible ? PanelRect.xMax + 28 : 18;
        var style = incidentHudStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 8, 3, 3) };
        style.normal.textColor = InterceptionActive ? new Color(1f, .4f, .35f) : Color.white;
        GUI.Box(new Rect(left, Screen.height - 58, 480, 42), $"{AliveEnemies}/{TargetEnemyDensity}  ·  {InterceptionLabel}" + (InterceptionActive ? "  ·  Уничтожь или уклонись" : ""), style);
    }
}
#endif
