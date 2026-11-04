using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = GameplayRandom.WorldRandom;

public sealed partial class ProductionExplorationSectorController
{
    private readonly List<Vector2> routePositions = new();
    private readonly List<RouteRiskOffer> routeOffers = new();
    private readonly List<RouteWave> routeWaves = new();
    private readonly List<WorldLootChest> routeChests = new();
    private bool routeContentActive;
    internal bool HasOpeningRouteChest => routeChests.Exists(chest => chest != null &&
        chest.State == WorldLootChest.ChestState.Opening);

    private void InitializeRouteContent()
    {
        var sector = RunStateManager.Instance?.CurrentSector;
        if (sector?.RouteNode == null) return;
        var incident = sector.RouteNode.SurpriseProfile;
        if (incident == SurpriseProfile.None) return;
        routeContentActive = true;
        foreach (var item in spawnedBreakables)
            if (item != null) routePositions.Add(item.transform.position);
        foreach (var chest in ownedRewardChests)
            if (chest != null) routePositions.Add(chest.transform.position);

        StartRouteIncident(incident);
    }

    private bool CanRunRouteContent(float seconds = 0f) => routeContentActive &&
        runFlow != null && runFlow.Phase == RunPhase.NormalSector &&
        runFlow.ExitMinimumTimeRemaining > seconds && !SceneTransitionOverlay.IsTransitioning &&
        PlayerRuntimeReference.PlayerHealth is { IsDead: false };

    private bool TryRoutePosition(out Vector2 position, float edgeClearance = .65f)
    {
        position = default;
        var player = PlayerRuntimeReference.PlayerTransform;
        if (!hasBreakableLayout || player == null) return false;
        Vector2 origin = player.position;
        Physics2D.SyncTransforms();
        for (int i = 0; i < 140; i++)
        {
            Vector2 candidate = origin + Random.insideUnitCircle.normalized * Random.Range(3.8f, 10f);
            if (!gameplayArea.IsInsidePlayableArea(candidate, edgeClearance) ||
                !IsBreakablePositionValid(candidate, origin, breakableNormalSitePositions,
                    breakableSpecialSitePosition, breakableExitPosition, routePositions, 3.5f, 1f))
                continue;
            routePositions.Add(candidate);
            position = candidate;
            return true;
        }
        return false;
    }

    private RouteRiskOffer AddRouteOffer(string name, Vector2 position, string prompt, Func<bool> available, Func<bool> accept)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform);
        root.transform.position = position;
        var collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = .45f;
        var source = config.BreakablePrefab != null ? config.BreakablePrefab.GetComponentInChildren<SpriteRenderer>() : null;
        if (source != null)
        {
            var marker = root.AddComponent<SpriteRenderer>();
            marker.sprite = source.sprite;
            marker.sortingLayerID = source.sortingLayerID;
            marker.sortingOrder = source.sortingOrder + 1;
            marker.color = new Color(1f, .64f, .22f);
        }
        var offer = root.AddComponent<RouteRiskOffer>();
        offer.Initialize(prompt, available, accept);
        routeOffers.Add(offer);
        return offer;
    }

    private RouteWave StartRouteWave(Vector2 position, int count, bool attackers, Action onCleared = null)
    {
        if (!CanRunRouteContent() || enemySpawner == null) return null;
        var wave = new RouteWave();
        enemySpawner.SpawnRouteWave(position, count, attackers, wave.Enemies);
        if (wave.Enemies.Count == 0) return null;
        routeWaves.Add(wave);
        wave.Track(onCleared);
        return wave;
    }

    private WorldLootChest SpawnRouteChest(Vector2 position, string name)
    {
        if (!CanRunRouteContent() || config.WorldLootChestPrefab == null) return null;
        var chest = WorldLootChestSpawner.SpawnChest(config.WorldLootChestPrefab, position);
        if (chest == null) return null;
        chest.name = name;
        chest.transform.SetParent(transform, true);
        ownedRewardChests.Add(chest);
        routeChests.Add(chest);
        return chest;
    }

    private static void RouteNotice(string title, string message)
    {
        var text = LocalizationService.EnsureExists();
        RunMessageService.Instance?.ShowCustom(text.Get(title), text.Get(message), 2.5f);
    }

    internal void EndRouteContent()
    {
        if (!routeContentActive) return;
        routeContentActive = false;
        EndRouteIncident();
        foreach (var offer in routeOffers)
            if (offer != null) { offer.gameObject.SetActive(false); Destroy(offer.gameObject); }
        routeOffers.Clear();
        foreach (var wave in routeWaves) wave.Dispose();
        routeWaves.Clear();
        for (int i = routeChests.Count - 1; i >= 0; i--)
        {
            var chest = routeChests[i];
            if (chest != null && chest.State is WorldLootChest.ChestState.Opening or WorldLootChest.ChestState.RewardReel)
                continue;
            routeChests.RemoveAt(i);
            if (chest == null) continue;
            chest.gameObject.SetActive(false);
            Destroy(chest.gameObject);
            ownedRewardChests.Remove(chest);
        }
        routePositions.Clear();
    }

    private Coroutine routeIncident;
    private LocalAnomalyZone incidentZone;
    private RarePickup incidentMedkit;
    private WorldEvent protocolEvent;
    private RouteWave protocolWave;
    private Vector2 protocolPosition;
    private bool protocolEventCleared, protocolWaveCleared, protocolRewardIssued;

    private void StartRouteIncident(SurpriseProfile profile)
    {
        routeIncident = StartCoroutine(PlayRouteIncident(profile));
    }

    private IEnumerator PlayRouteIncident(SurpriseProfile profile)
    {
        yield return new WaitForSeconds(7f);
        if (!CanRunRouteContent(12f)) yield break;

        switch (profile)
        {
            case SurpriseProfile.ForgottenCache:
                if (TryRoutePosition(out var cachePosition) &&
                    SpawnRouteChest(cachePosition, "Incident forgotten cache") != null)
                    RouteNotice("route.incident.cache", "route.incident.cache.info");
                break;
            case SurpriseProfile.Ambush:
                var player = PlayerRuntimeReference.PlayerTransform;
                if (player == null) break;
                Vector2 origin = player.position;
                RouteNotice("route.incident.ambush", "route.incident.ambush.info");
                yield return new WaitForSeconds(2f);
                for (int attempt = 0; attempt < 4 && CanRunRouteContent(2f); attempt++)
                {
                    if (StartRouteWave(origin, 3, false) != null) break;
                    yield return new WaitForSeconds(1f);
                }
                break;
            case SurpriseProfile.EmergencyProtocol:
                yield return OfferEmergencyProtocol();
                break;
            case SurpriseProfile.FieldFault:
                var anomaly = System.Array.Find(config.NormalAnomalies ?? System.Array.Empty<LocalAnomalyData>(),
                    candidate => candidate != null && candidate.ZonePrefab != null);
                if (anomalyController == null || anomaly == null || !TryRoutePosition(out var zonePosition, 3f))
                    break;
                incidentZone = anomalyController.SpawnSiteZone(anomaly, zonePosition, Vector2.one * 4f, transform);
                if (incidentZone == null) break;
                incidentZone.name = "Incident field fault";
                RouteNotice("route.incident.field", "route.incident.field.info");
                yield return new WaitForSeconds(10f);
                ClearIncidentZone();
                break;
            case SurpriseProfile.MedStation:
                if (!TryRoutePosition(out var medkitPosition) || ExperienceManager.Instance == null) break;
                incidentMedkit = ExperienceManager.Instance.SpawnSectorMedKit(medkitPosition);
                if (incidentMedkit == null) break;
                incidentMedkit.name = "Incident med station";
                incidentMedkit.transform.SetParent(transform, true);
                RouteNotice("route.incident.med", "route.incident.med.info");
                break;
        }
        routeIncident = null;
    }

    private IEnumerator OfferEmergencyProtocol()
    {
        if (eventSpawner == null || config.WorldLootChestPrefab == null) yield break;
        RouteNotice("route.incident.protocol", "route.incident.protocol.wait");
        while (CanRunRouteContent(12f) && eventSpawner.ActiveEvent == null)
            yield return null;
        if (!CanRunRouteContent(12f) || eventSpawner.ActiveEvent == null) yield break;

        var active = eventSpawner.ActiveEvent;
        var player = PlayerRuntimeReference.PlayerTransform;
        if (player == null) yield break;
        Vector2 position = (Vector2)player.position + Vector2.right * .85f;
        if (!gameplayArea.IsInsidePlayableArea(position, .55f)) position = player.position;
        var offer = AddRouteOffer("Emergency protocol offer", position, "route.incident.protocol.offer",
            () => CanRunRouteContent(12f) && eventSpawner.ActiveEvent == active && active != null && !active.IsCompleted,
            () => AcceptProtocol(active, position));
        while (offer.CanInteract) yield return null;
        if (offer != null) offer.gameObject.SetActive(false);
    }

    private bool AcceptProtocol(WorldEvent active, Vector2 position)
    {
        protocolWave = StartRouteWave(position, 3, true, () =>
        {
            protocolWaveCleared = true;
            TryIssueProtocolReward();
        });
        if (protocolWave == null)
        {
            RouteNotice("route.incident.protocol", "route.wave.unavailable");
            return false;
        }

        protocolEvent = active;
        protocolPosition = position;
        eventSpawner.EventCompleted += OnProtocolEventCompleted;
        eventSpawner.EventFailed += OnProtocolEventFailed;
        RouteNotice("route.incident.protocol", "route.incident.protocol.started");
        return true;
    }

    private void OnProtocolEventCompleted(WorldEvent completed)
    {
        if (completed != protocolEvent) return;
        protocolEventCleared = true;
        UnsubscribeProtocol();
        TryIssueProtocolReward();
    }

    private void OnProtocolEventFailed(WorldEvent failed)
    {
        if (failed != protocolEvent) return;
        UnsubscribeProtocol();
        protocolWave?.Dispose();
        protocolWave = null;
    }

    private void TryIssueProtocolReward()
    {
        if (!protocolEventCleared || !protocolWaveCleared || protocolRewardIssued) return;
        protocolRewardIssued = true;
        SpawnRouteChest(protocolPosition, "Incident protocol bonus chest");
    }

    private void UnsubscribeProtocol()
    {
        if (eventSpawner != null)
        {
            eventSpawner.EventCompleted -= OnProtocolEventCompleted;
            eventSpawner.EventFailed -= OnProtocolEventFailed;
        }
        protocolEvent = null;
    }

    private void ClearIncidentZone()
    {
        if (incidentZone != null && anomalyController != null)
            anomalyController.CollapseSiteZone(incidentZone);
        incidentZone = null;
    }

    private void EndRouteIncident()
    {
        if (routeIncident != null) StopCoroutine(routeIncident);
        routeIncident = null;
        UnsubscribeProtocol();
        protocolWave = null;
        protocolEventCleared = protocolWaveCleared = protocolRewardIssued = false;
        ClearIncidentZone();
        if (incidentMedkit != null)
        {
            incidentMedkit.gameObject.SetActive(false);
            Destroy(incidentMedkit.gameObject);
        }
        incidentMedkit = null;
    }

    private sealed class RouteWave
    {
        public readonly List<EnemyHealth> Enemies = new();
        private int remaining;
        private Action cleared;

        public void Track(Action onCleared)
        {
            remaining = Enemies.Count;
            cleared = onCleared;
            foreach (var enemy in Enemies) enemy.OnDied += OnEnemyDied;
        }

        private void OnEnemyDied(EnemyHealth enemy)
        {
            enemy.OnDied -= OnEnemyDied;
            if (--remaining != 0) return;
            var reward = cleared;
            cleared = null;
            reward?.Invoke();
        }

        public void Dispose()
        {
            cleared = null;
            foreach (var enemy in Enemies)
            {
                if (enemy == null) continue;
                enemy.OnDied -= OnEnemyDied;
                if (enemy.IsDead) continue;
                enemy.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(enemy.gameObject);
            }
            Enemies.Clear();
        }
    }
}
