#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;

public sealed class CorridorLab : MonoBehaviour
{
    private WorldSystemsLabController lab;
    private GameplayAreaService area;
    private GameObject rocket, marker;
    private ParticleSystem explosion;
    private CorridorKit kit;
    private CorridorEvent prefab;
    public CorridorSettings Tuning { get; private set; }
    public CorridorEvent Current => current;
    private GameObject enemyPrefab;
    private EnemySpawner enemies;
    private CorridorEvent current;
    private Transform bounds;
    private PlayerHealth playerHealth;
    private SpriteRenderer[] playerSprites;
    private bool[] spriteEnabled;
    private bool movementEnabled;
    private Vector3 areaScale, boundsScale;
    private bool expanded, crowd = true;
    private int turns;
    private string status = "F5: start Corridor V2";

    public void Initialize(WorldSystemsLabController owner, GameplayAreaService gameplayArea,
        GameObject rocketAsset, GameObject markerAsset, ParticleSystem explosionAsset,
        GameObject normalEnemy, CorridorSettings settings, CorridorKit prefabKit, CorridorEvent eventPrefab)
    {
        lab = owner; area = gameplayArea; rocket = rocketAsset; marker = markerAsset;
        explosion = explosionAsset; enemyPrefab = normalEnemy; Tuning = eventPrefab.Config.settings.Snapshot(); kit = prefabKit; prefab = eventPrefab;
        playerHealth = lab.Player.GetComponent<PlayerHealth>();
        playerSprites = lab.Player.GetComponentsInChildren<SpriteRenderer>(true);
        spriteEnabled = playerSprites.Select(sprite => sprite.enabled).ToArray();
        movementEnabled = lab.Player.GetComponent<CharacterMovement2D>().enabled;
        bounds = gameObject.scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Environment Bounds")?.transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6)) Tuning.routePreset = (CorridorRouteMode)(((int)Tuning.routePreset + 1) % 4);
        if (Input.GetKeyDown(KeyCode.F7)) turns = (turns + 1) % 4;
        if (Input.GetKeyDown(KeyCode.F8)) SetCrowd(!crowd);
        if (Input.GetKeyDown(KeyCode.F5)) StartCorridor();
        if (Input.GetKeyDown(KeyCode.F9)) SpawnIndependentTerritory();
    }

    public void StartCorridor()
    {
        if (kit == null || prefab == null || rocket == null || marker == null || explosion == null || enemyPrefab == null || lab.Player == null || area == null || bounds == null)
        {
            status = "Missing lab references. Rebuild WorldSystemsLab from Tools menu.";
            return;
        }
        lab.PrepareCorridorV2();
        areaScale = area.transform.localScale;
        boundsScale = bounds.localScale;
        var route = new CorridorRoute(Tuning, turns);
        float extent = route.Vertices.Max(p => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y))) + route.HalfWidth + 15f;
        float expansion = Mathf.Max(1.6f, extent / 50f);
        area.transform.localScale = areaScale * expansion;
        bounds.localScale = boundsScale * expansion;
        expanded = true;

        var playerBody = lab.Player.GetComponent<Rigidbody2D>();
        lab.Player.position = route.Vertices[0];
        if (playerBody != null)
        {
            playerBody.position = route.Vertices[0];
            playerBody.linearVelocity = Vector2.zero;
        }
        if (playerHealth.IsDead)
        {
            playerHealth.SetRuntimeHealth(playerHealth.MaxHealth, playerHealth.MaxHealth);
            lab.Player.GetComponent<CharacterMovement2D>().enabled = movementEnabled;
            for (int i = 0; i < playerSprites.Length; i++)
                playerSprites[i].enabled = spriteEnabled[i];
        }
        else playerHealth.Heal(playerHealth.MaxHealth);
        Physics2D.SyncTransforms();
        var admissionSettings = Tuning.Snapshot();
        admissionSettings.routeSeed = route.Seed;
        lab.Events.ConfigureDebugEventPrefabs(lab.EventPrefabs.Concat(new WorldEvent[] { prefab }).Distinct().ToArray());
        bool spawned = lab.Events.SpawnConcurrentDebugEventAt(prefab, route.Vertices[0], true, out WorldEvent instance,
            item => ((CorridorEvent)item).SetRouteInput(admissionSettings, turns));
        lab.Events.ConfigureDebugEventPrefabs(lab.EventPrefabs.ToArray());
        if (!spawned) { Stop(); status = "Production Corridor admission rejected"; return; }
        current = (CorridorEvent)instance;
        current.Finished = message =>
        {
            Stop(false); // Keep the playable lab extent and player position after a normal result.
            status = message;
        };
        current.gameObject.SetActive(true);
        current.StartEvent();
        if (!current.IsStarted)
        {
            CancelForRestart();
            status = "Could not start event. Check the active event/tutorial state.";
            return;
        }
        status = $"{Tuning.routePreset} / seed {route.Seed} / {turns * 90} degrees / {route.Length:F0} units";
        SetCrowd(crowd);
    }

    private void SetCrowd(bool value)
    {
        crowd = value;
        if (!crowd)
        {
            enemies?.StopDebugExplorationPressure();
            enemies?.ClearDebugSpawnedEnemies();
            return;
        }
        if (current == null || current.IsCompleted) return;
        if (enemies == null)
        {
            enemies = gameObject.AddComponent<EnemySpawner>();
            enemies.StopSpawning();
        }
        enemies.ResetForNewLevel();
        enemies.ConfigureDebugExplorationPressure(new[] { enemyPrefab }, 1f, 24, 1, 9f, 15f);
    }

    public void Stop(bool restoreArea = true)
    {
        if (current != null) current.Finished = null;
        // Event removal is owned by WorldSystemsLabController.ClearEvents.
        current = null;
        if (enemies != null)
        {
            enemies.StopDebugExplorationPressure();
            enemies.ClearDebugSpawnedEnemies();
        }
        if (expanded && restoreArea)
        {
            if (area != null) area.transform.localScale = areaScale;
            if (bounds != null) bounds.localScale = boundsScale;
            expanded = false;
        }
        status = "Stopped / F5 to restart";
    }
    public void CancelForRestart()
    {
        var owned = current;
        Stop();
        if (owned != null) lab.Events.ClearDebugEvent(owned);
    }
    public void SpawnIndependentTerritory()
    {
        status = lab.SpawnIndependentStasisTerritory()
            ? "Independent Stasis spawned at world origin. F6: Straight, then F5 to cross it."
            : "Could not spawn world territory.";
    }

    private void OnGUI()
    {
        float width = Mathf.Min(310f, Screen.width * .4f);
        bool active = current != null && current.IsStarted && !current.IsCompleted;
        var previousFont = GUI.skin.font;
        var previousColor = GUI.color;
        if (Tuning.font != null && Tuning.font.sourceFontFile != null) GUI.skin.font = Tuning.font.sourceFontFile;
        GUI.color = new Color(.45f, 1f, .9f);
        GUILayout.BeginArea(new Rect(Screen.width - width - 8, 8, width, active ? 76 : 165), GUI.skin.box);
        GUILayout.Label("LAB: F5/F6/F7/F8 В· F9 world Stasis");
        if (!active)
        {
            GUILayout.Label($"{Tuning.routePreset} / {turns * 90} degrees / enemies {(crowd ? "ON" : "OFF")}");
            if (GUILayout.Button("F5: start / restart")) StartCorridor();
            GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }
        if (GUILayout.Button("F9: spawn independent world Stasis")) SpawnIndependentTerritory();
        GUILayout.EndArea();
        GUI.color = previousColor;
        GUI.skin.font = previousFont;
    }
}
#endif
