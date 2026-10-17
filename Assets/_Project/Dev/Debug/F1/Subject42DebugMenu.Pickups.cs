#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Subject42DebugMenu
{
    private readonly List<ExperiencePickup> testPickups = new();
    private readonly RaycastHit2D[] pickupPathHits = new RaycastHit2D[16];

    private bool CanUsePickupDebugTools => IsMvpDebugMenu && MvpCanEdit &&
        ExperienceManager.Instance != null &&
        ExperienceManager.Instance.CanUseRarePickups(gameObject.scene) &&
        GameplayAreaService.Instance != null;

    private void BuildMvpPickupTools()
    {
        PruneTestPickups();
        var experience = ExperienceManager.Instance;
        bool available = CanUsePickupDebugTools;
        AddSectionTitle("Предметы / Pickups", "Создайте предметы и закройте F1: игра продолжится, начнётся обычное притяжение и подбор.");
        MvpRow("Аптечка", "Рабочий p_medKit1 · рядом с игроком", "Spawn Medkit",
            available && experience.DebugHasRarePickup(RarePickup.PickupKind.MedKit),
            () => SpawnTestRarePickup(RarePickup.PickupKind.MedKit));
        MvpRow("Магнит", "Рабочий p_magnet1 · повторный подбор продлевает эффект", "Spawn Magnet",
            available && experience.DebugHasRarePickup(RarePickup.PickupKind.Magnet),
            () => SpawnTestRarePickup(RarePickup.PickupKind.Magnet));
        MvpRow("Проверка лечения", "Установить ровно половину максимального здоровья", "Set HP to 50%", available, SetTestHealthToHalf);
        MvpRow("Поле опыта", "50 обычных кристаллов за текущим радиусом подбора", "Spawn XP × 50",
            available && ResolveExperienceLootSource() != null, () => SpawnDebugExperience(50, trackForCleanup: true));
        MvpRow("Очистить тестовые предметы", $"Осталось созданных этим разделом объектов: {testPickups.Count}",
            "Clear Test Pickups", testPickups.Count > 0, () =>
            {
                ReleaseTestPickups();
                mvpStatus = "Тестовые предметы и опыт удалены. Игровой дроп сохранён.";
                RefreshCurrentTab();
            });
    }

    private void SetTestHealthToHalf()
    {
        if (!CanUsePickupDebugTools) return;
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        RunStateManager.Instance?.DebugMarkDevelopmentRun();
        health.SetRuntimeHealth(health.MaxHealth, health.MaxHealth * .5f);
        RefreshCurrentTab();
    }

    private void SpawnTestRarePickup(RarePickup.PickupKind kind)
    {
        if (!CanUsePickupDebugTools) return;
        var player = PlayerRuntimeReference.CachedPlayer.transform;
        if (!TryGetTestPickupPosition(player, 1.5f, 2f, out var position))
        {
            mvpStatus = "Рядом нет свободной точки: отойдите от препятствий и повторите.";
            RefreshCurrentTab();
            return;
        }
        PruneTestPickups();
        var pickup = ExperienceManager.Instance.DebugSpawnRarePickup(kind, position);
        if (pickup != null)
        {
            testPickups.Add(pickup);
            RunStateManager.Instance?.DebugMarkDevelopmentRun();
            mvpStatus = "Предмет создан в 1,5–2 м от игрока. Закройте F1 для подбора.";
        }
        RefreshCurrentTab();
    }

    private bool TryGetTestPickupPosition(Transform player, float minimum, float maximum, out Vector3 position)
    {
        position = default;
        var area = GameplayAreaService.Instance;
        if (area == null) return false;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(player.gameObject.layer));
        float startAngle = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < 48; i++)
        {
            float angle = startAngle + i * (Mathf.PI * 2f / 48f);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float distance = Mathf.Lerp(minimum, maximum, (i % 3) * .5f);
            Vector2 candidate = (Vector2)player.position + direction * distance;
            if (!area.IsInsidePlayableArea(candidate, .2f)) continue;
            int hitCount = Physics2D.CircleCast(player.position, .2f, direction, filter, pickupPathHits, distance);
            if (hitCount == pickupPathHits.Length) continue;
            bool blocked = false;
            for (int hit = 0; hit < hitCount; hit++)
            {
                var collider = pickupPathHits[hit].collider;
                if (collider != null && !collider.transform.IsChildOf(player)) { blocked = true; break; }
            }
            if (blocked) continue;
            position = new Vector3(candidate.x, candidate.y, player.position.z);
            return true;
        }
        return false;
    }

    private void PruneTestPickups() => testPickups.RemoveAll(pickup => pickup == null || !pickup.gameObject.activeSelf);

    private void ReleaseTestPickups()
    {
        foreach (var pickup in testPickups)
            if (pickup != null) pickup.Despawn();
        testPickups.Clear();
    }
}
#endif
