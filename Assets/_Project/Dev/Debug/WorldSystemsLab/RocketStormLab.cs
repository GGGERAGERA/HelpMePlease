#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;

// Only lab bootstrap/controls. The production prefab owns every strike and the attempt lifecycle.
public sealed class RocketStormLab : MonoBehaviour
{
    private WorldSystemsLabController lab;
    private RocketStormEvent.Settings settings;
    private RocketStormEvent prefab, current;
    private bool pressure;
    private string status = "30 секунд внутри зоны";
    public bool IsRunning => current != null && current.IsRunning;
    public int PendingRockets => current != null ? current.PendingRockets : 0;
    public int WavesLaunched => current != null ? current.WavesLaunched : 0;
    public string Status => current != null ? current.Status : status;

    public void Initialize(WorldSystemsLabController owner, RocketStormEvent.Settings tuning, RocketStormEvent asset)
    { lab = owner; settings = tuning; prefab = asset; }

    public void StartStorm()
    {
        if (prefab == null) { status = "Missing Rocket Storm prefab"; return; }
        if (!lab.PrepareRocketStorm()) { status = "Lab combat bootstrap failed; see Console"; return; }
        var health = lab.Player.GetComponent<PlayerHealth>();
        health.SetRuntimeHealth(health.MaxHealth, health.MaxHealth);
        lab.Player.GetComponent<CharacterMovement2D>().enabled = true;
        lab.TeleportPlayerCenter();
        Physics2D.SyncTransforms();
        lab.Events.ConfigureDebugEventPrefabs(lab.EventPrefabs.Concat(new WorldEvent[] { prefab }).Distinct().ToArray());
        bool spawned = lab.Events.SpawnConcurrentDebugEventAt(prefab, lab.Player.position, true, out var instance,
            item => ((RocketStormEvent)item).SetLabSettings(settings));
        lab.Events.ConfigureDebugEventPrefabs(lab.EventPrefabs.ToArray());
        if (!spawned) { status = "Rocket Storm placement rejected; see Console"; return; }
        current = (RocketStormEvent)instance;
        current.Finished = message => { Stop(false); status = message; };
        current.StartEvent();
        lab.SetRocketStormPressure(pressure);
    }
    public void SetEnemyPressure(bool value)
    {
        if (pressure == value) return;
        pressure = value;
        if (IsRunning) lab.SetRocketStormPressure(value);
    }
    public void Stop(bool cancelAttempt = true)
    {
        var owned = current;
        if (owned != null) owned.Finished = null;
        current = null;
        if (cancelAttempt && owned != null) lab.Events.ClearDebugEvent(owned);
        if (lab != null) lab.SetRocketStormPressure(false);
        status = "Остановлено";
    }
    private void OnDisable() => Stop();
}
#endif
