#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public sealed partial class EnemyOrbitalLabController
{
    void ConfigureLabTacticalEnemy(GameObject instance, EnemyHealth health)
    {
        if (!HasInterceptionExperiment || !instance.TryGetComponent<EnemyChaseMovement>(out var movement) || !movement.IsProductionTacticalEnemy) return;
        bool flanker = movement.IsFlankingExperiment;
        // Preserve the lab's seeded chance and pause policy instead of rolling the prefab timer too.
        var productionTimer = instance.GetComponent<EnemyTacticalTimedShield>();
        if (productionTimer != null) productionTimer.enabled = false;
        if (incidentRandom.NextDouble() < (flanker ? .20 : .25) && BreakableShieldBearerPrefab != null)
            LabTacticalTimedShield.Attach(instance, BreakableShieldBearerPrefab.GetComponent<EnemyDirectionalShield>(), this,
                Mathf.Lerp(3f, 6f, (float)incidentRandom.NextDouble()));
        var graphic = movement.ConfigureLabTacticalMovement();
        health.SetRuntimeMaxHealth(flanker ? 16f : 32f);
        // Keep animated sprite parts and real colliders; tint/scale only the safe graphic parent.
        if (graphic != null && !movement.UsesProductionTacticalTuning)
        {
            graphic.localScale = Vector3.Scale(graphic.localScale, flanker ? new Vector3(.85f, 1.15f, 1f) : new Vector3(1.35f, 1.25f, 1f));
            foreach (var sprite in graphic.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.color = flanker ? new Color(.88f, .42f, 1f, sprite.color.a) : new Color(1f, .4f, .24f, sprite.color.a);
        }
    }

    public void SpawnTacticalGroup(bool flankers)
    {
        var types = ManualEnemyTypes;
        foreach (int type in types)
            if (LabEnemyPrefab(type).TryGetComponent<EnemyChaseMovement>(out var movement) && movement.IsProductionTacticalEnemy &&
                (LabEnemyPrefab(type).name.Contains("Flanker") == flankers))
            { SpawnManualEnemies(type, flankers ? 20 : 10); return; }
    }
}
#endif
