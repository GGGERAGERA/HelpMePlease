#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

public sealed partial class EnemyOrbitalLabController
{
    public bool AutomaticEnemySpawning { get; private set; } = true;
    readonly Dictionary<EnemyHealth, float> manualEnemies = new();
    readonly List<EnemyHealth> retiredManualEnemies = new();
    readonly List<int> manualEnemyTypes = new();
    readonly List<Collider2D> manualSpawnOverlaps = new();
    readonly ContactFilter2D manualSpawnFilter = new() { useTriggers = true };
    int manualCatalogStamp, manualSpawnSequence;
    Vector2 manualSpawnScroll;
    GUIStyle manualSpawnButton, manualSpawnLabel;

    public int ManualAliveEnemies
    {
        get { int count = 0; foreach (var enemy in manualEnemies.Keys) if (enemy != null && !enemy.IsDead) count++; return count; }
    }
    public IReadOnlyCollection<EnemyHealth> ManualEnemies => manualEnemies.Keys;
    public IReadOnlyList<int> ManualEnemyTypes
    {
        get
        {
            int stamp = 17;
            unchecked
            {
                if (EnemyPrefabs != null) foreach (var prefab in EnemyPrefabs) stamp = stamp * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
                foreach (var prefab in new[] { PursuerPrefab, BreakableShieldBearerPrefab, BreakableShieldShooterPrefab, TestShooterPrefab })
                    stamp = stamp * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
                if (HasArtistComparison && ArtistSpiderPrefabs != null)
                    foreach (var prefab in ArtistSpiderPrefabs) stamp = stamp * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
            }
            if (manualCatalogStamp != stamp || manualEnemyTypes.Count == 0)
            {
                BuildDensityBag(); // The existing mixed-spawn catalog already deduplicates connected combat prefabs.
                manualEnemyTypes.Clear(); manualEnemyTypes.AddRange(densityTypes);
                if (HasArtistComparison && ArtistSpiderPrefabs != null)
                    for (int i = 0; i < ArtistSpiderPrefabs.Length; i++)
                        if (ArtistSpiderPrefabs[i] != null && ArtistSpiderPrefabs[i].GetComponent<EnemyHealth>() != null) manualEnemyTypes.Add(-100-i);
                manualEnemyTypes.Sort((a, b) => string.CompareOrdinal(ManualEnemyName(a), ManualEnemyName(b)));
                manualCatalogStamp = stamp;
            }
            return manualEnemyTypes;
        }
    }
    GameObject LabEnemyPrefab(int type) => type switch
    {
        -2 or -4 => BreakableShieldBearerPrefab, -3 => PursuerPrefab,
        -5 or -6 => BreakableShieldShooterPrefab, -1 => TestShooterPrefab,
        _ => HasArtistComparison && type <= -100 && ArtistSpiderPrefabs != null && -100-type < ArtistSpiderPrefabs.Length
            ? ArtistSpiderPrefabs[-100-type]
            : EnemyPrefabs != null && type >= 0 && type < EnemyPrefabs.Length ? EnemyPrefabs[type] : null
    };
    public string ManualEnemyName(int type)
    {
        var prefab = LabEnemyPrefab(type);
        if (prefab == null) return "Недоступен";
        return prefab.name switch
        {
            "p_Enemy_Flanker" => "Обходчик", "p_Enemy_Breacher" => "Проломщик",
            "p_Enemy_classic" => "Зомби", "p_Enemy_default" => "Преследователь",
            "p_Enemy_Shooter" => "Стрелок", "p_Enemy_Bomber" => "Подрывник",
            "p_EnemyTurret1" => "Турель", "p_EnemyEye1 Variant" => "Глаз",
            "BreakableShieldBearer" => "Щитоносец", "BreakableShieldShooter" => "Стрелок со щитом",
            "DistantTestShooter" => "Тестовый стрелок",
            "LabSpiderWhite" => "Паук 1 · белый эскиз",
            "LabSpiderDark" => "Паук 2 · тёмный",
            "LabSpiderBigGait" => "Паук 2 · крупная походка",
            "LabSpiderBlue" => "Паук 3 · синий",
            "p_Boss2" => "Паук · исходный босс",
            _ => prefab.name.Replace("p_Enemy_", "").Replace("p_Enemy", "").Replace('_', ' ')
        };
    }
    public void SetAutomaticEnemySpawning(bool enabled)
    {
        if (!HasInterceptionExperiment) return;
        AutomaticEnemySpawning = enabled; densityCredit = 0;
    }
    void PruneManualEnemies()
    {
        if (manualEnemies.Count == 0) return;
        retiredManualEnemies.Clear();
        foreach (var pair in manualEnemies) if (pair.Key == null || pair.Key.IsDead) retiredManualEnemies.Add(pair.Key);
        foreach (var enemy in retiredManualEnemies) manualEnemies.Remove(enemy);
    }
    public int SpawnManualEnemies(int type, int count)
    {
        if (!HasInterceptionExperiment || !Ready || count <= 0) return 0;
        var prefab = LabEnemyPrefab(type);
        if (prefab == null || prefab.GetComponent<EnemyHealth>() == null) return 0;
        PruneManualEnemies(); Physics2D.SyncTransforms();
        float radius = ManualSpawnRadius(prefab);
        if (prefab.TryGetComponent<EnemyChaseMovement>(out var tactical) && tactical.IsProductionTacticalEnemy)
            radius = Mathf.Max(radius, 1.6f); // Reserve room for a possible delayed shield, too.
        int created = 0;
        for (; created < count; created++)
        {
            if (!TryManualSpawnPosition(radius, out var position)) break;
            var enemy = SpawnAt(type, position); // Same lab health/AI/weapon-hit/reward-suppression path; no production caps.
            manualEnemies.Add(enemy, radius);
        }
        SandboxMessage = created == count ? $"{ManualEnemyName(type)}: +{created}. Ручные враги сохраняются при любой цели плотности."
            : $"{ManualEnemyName(type)}: +{created}/{count}. Рядом нет свободного места для остальных.";
        return created;
    }
    static float ManualSpawnRadius(GameObject prefab)
    {
        float radius = .35f;
        foreach (var collider in prefab.GetComponentsInChildren<Collider2D>(true))
        {
            if (!collider.enabled) continue;
            Vector2 scale = collider.transform.lossyScale;
            float size = collider switch
            {
                CircleCollider2D circle => circle.radius,
                BoxCollider2D box => box.size.magnitude * .5f,
                CapsuleCollider2D capsule => capsule.size.magnitude * .5f,
                _ => .5f
            };
            float extent = size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            if (collider is PolygonCollider2D polygon)
                foreach (var point in polygon.points) extent = Mathf.Max(extent, point.magnitude * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            if (collider is EdgeCollider2D edge)
                foreach (var point in edge.points) extent = Mathf.Max(extent, (point.magnitude + edge.edgeRadius) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            Vector2 center = collider.transform.TransformPoint(collider.offset);
            radius = Mathf.Max(radius, Vector2.Distance(center, prefab.transform.position) + extent);
        }
        if (prefab.TryGetComponent<EnemyDirectionalShield>(out var shield))
            radius = Mathf.Max(radius, Vector2.Distance(shield.Center, prefab.transform.position) + shield.ShieldDistance + shield.ShieldThickness * .5f);
        return radius;
    }
    bool TryManualSpawnPosition(float radius, out Vector2 position)
    {
        Vector2 player = Player.transform.position;
        float minimum = Mathf.Max(5.5f, radius + 4.8f);
        for (int attempt = 0; attempt < 512; attempt++)
        {
            float angle = manualSpawnSequence++ * 137.508f * Mathf.Deg2Rad;
            float distance = minimum + attempt % 7 * 1.25f;
            Vector2 point = player + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (Mathf.Abs(point.x) + radius > 28 || Mathf.Abs(point.y) + radius > 28) continue;
            bool occupied = false;
            foreach (var pair in manualEnemies)
                if (pair.Key != null && !pair.Key.IsDead && Vector2.Distance(point, pair.Key.transform.position) < radius + pair.Value + .15f)
                { occupied = true; break; }
            if (occupied) continue;
            Physics2D.OverlapCircle(point, radius + .12f, manualSpawnFilter, manualSpawnOverlaps);
            foreach (var collider in manualSpawnOverlaps)
            {
                if (collider == null || collider.gameObject.scene != gameObject.scene) continue;
                if (collider.isTrigger && collider.GetComponentInParent<EnemyHealth>() == null && collider.GetComponentInParent<PlayerHealth>() == null) continue;
                occupied = true; break;
            }
            if (!occupied) { position = point; return true; }
        }
        position = default; return false;
    }
    internal void DrawManualEnemySpawns(GUIStyle heading, GUIStyle small, GUIStyle button)
    {
        if (!HasInterceptionExperiment || !Ready) return;
        if (manualSpawnButton == null)
        {
            manualSpawnButton = new GUIStyle(button) { fixedHeight = 28, fontSize = 13, margin = new RectOffset(2, 2, 1, 1) };
            manualSpawnLabel = new GUIStyle(small) { fixedHeight = 28, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        }
        DrawArtistComparison(heading, small, manualSpawnButton);
        GUILayout.Label("РУЧНОЙ СПАУН ВРАГОВ", heading);
        GUILayout.Label($"Всего живых: {AliveEnemies} · ручных: {ManualAliveEnemies}", small);
        bool auto = GUILayout.Toggle(AutomaticEnemySpawning, "Автоматическое появление врагов");
        if (auto != AutomaticEnemySpawning) SetAutomaticEnemySpawning(auto);
        var types = ManualEnemyTypes;
        manualSpawnScroll = GUILayout.BeginScrollView(manualSpawnScroll, false, false, GUILayout.Height(Mathf.Min(220, types.Count * 30 + 8)));
        foreach (int type in types)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(ManualEnemyName(type), LabEnemyPrefab(type).name), manualSpawnLabel, GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("+1", manualSpawnButton, GUILayout.Width(39))) SpawnManualEnemies(type, 1);
            if (GUILayout.Button("+5", manualSpawnButton, GUILayout.Width(39))) SpawnManualEnemies(type, 5);
            if (GUILayout.Button("+10", manualSpawnButton, GUILayout.Width(42))) SpawnManualEnemies(type, 10);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("20 Обходчиков", manualSpawnButton)) SpawnTacticalGroup(true);
        if (GUILayout.Button("10 Проломщиков", manualSpawnButton)) SpawnTacticalGroup(false);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Очистить врагов", manualSpawnButton)) ClearDensityEnemies();
        GUILayout.Space(4);
    }
}
#endif
