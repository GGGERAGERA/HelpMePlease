#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;

public sealed class CorridorV2Lab : MonoBehaviour
{
    private WorldSystemsLabController lab;
    private GameplayAreaService area;
    private GameObject rocket, marker;
    private ParticleSystem explosion;
    public CorridorV2Settings Tuning { get; private set; }
    public CorridorV2Event Current => current;
    private GameObject enemyPrefab;
    private EnemySpawner enemies;
    private CorridorV2Event current;
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
        GameObject normalEnemy, CorridorV2Settings settings)
    {
        lab = owner; area = gameplayArea; rocket = rocketAsset; marker = markerAsset;
        explosion = explosionAsset; enemyPrefab = normalEnemy; Tuning = settings;
        playerHealth = lab.Player.GetComponent<PlayerHealth>();
        playerSprites = lab.Player.GetComponentsInChildren<SpriteRenderer>(true);
        spriteEnabled = playerSprites.Select(sprite => sprite.enabled).ToArray();
        movementEnabled = lab.Player.GetComponent<CharacterMovement2D>().enabled;
        bounds = gameObject.scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Environment Bounds")?.transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6)) Tuning.routePreset = (CorridorV2Preset)(((int)Tuning.routePreset + 1) % 3);
        if (Input.GetKeyDown(KeyCode.F7)) turns = (turns + 1) % 4;
        if (Input.GetKeyDown(KeyCode.F8)) SetCrowd(!crowd);
        if (Input.GetKeyDown(KeyCode.F5)) StartCorridor();
    }

    public void StartCorridor()
    {
        if (rocket == null || marker == null || explosion == null || enemyPrefab == null || lab.Player == null || area == null || bounds == null)
        {
            status = "Missing lab references. Rebuild WorldSystemsLab from Tools menu.";
            return;
        }
        lab.PrepareCorridorV2();
        areaScale = area.transform.localScale;
        boundsScale = bounds.localScale;
        var route = new CorridorV2Route(Tuning, turns);
        float extent = route.Vertices.Max(p => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y))) + route.HalfWidth + 15f;
        float expansion = Mathf.Max(1.6f, extent / 50f);
        area.transform.localScale = areaScale * expansion;
        bounds.localScale = boundsScale * expansion;
        expanded = true;

        // A transient template lets the existing spawner own registration,
        // active-event state and cleanup without adding a production prefab.
        var templateObject = new GameObject("Corridor V2 lab template");
        templateObject.SetActive(false);
        var template = templateObject.AddComponent<CorridorV2Event>();
        lab.Events.ConfigureDebugEventPrefabs(lab.EventPrefabs.Concat(new WorldEvent[] { template }).ToArray());
        bool spawned = lab.Events.SpawnDebugEventAt(template, Vector3.zero, true, out WorldEvent instance);
        lab.Events.ConfigureDebugEventPrefabs(lab.EventPrefabs.ToArray());
        Destroy(templateObject);
        if (!spawned) { Stop(); status = "Event spawner rejected Corridor V2"; return; }

        current = (CorridorV2Event)instance;
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
        current.Configure(lab.Player, Tuning, turns, rocket, marker, explosion);
        current.Finished = message =>
        {
            Stop(); // Completion/death releases the same lab leases as an explicit reset.
            status = message;
        };
        current.gameObject.SetActive(true);
        current.StartEvent();
        if (!current.IsStarted)
        {
            lab.ClearEvents();
            status = "Could not start event. Check the active event/tutorial state.";
            return;
        }
        status = $"{Tuning.routePreset} / {turns * 90} degrees / {route.Length:F0} units";
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

    public void Stop()
    {
        if (current != null) current.Finished = null;
        // Event removal is owned by WorldSystemsLabController.ClearEvents.
        current = null;
        if (enemies != null)
        {
            enemies.StopDebugExplorationPressure();
            enemies.ClearDebugSpawnedEnemies();
        }
        if (expanded)
        {
            if (area != null) area.transform.localScale = areaScale;
            if (bounds != null) bounds.localScale = boundsScale;
            expanded = false;
            if (lab != null && lab.Player != null && area != null) lab.TeleportPlayerCenter();
        }
        status = "Stopped / F5 to restart";
    }

    private void OnGUI()
    {
        float width = Mathf.Min(310f, Screen.width * .4f);
        bool active = current != null && current.IsStarted && !current.IsCompleted;
        var previousFont = GUI.skin.font;
        var previousColor = GUI.color;
        if (Tuning.font != null && Tuning.font.sourceFontFile != null) GUI.skin.font = Tuning.font.sourceFontFile;
        GUI.color = new Color(.45f, 1f, .9f);
        GUILayout.BeginArea(new Rect(Screen.width - width - 8, 8, width, active ? 115 : 160), GUI.skin.box);
        GUILayout.Label("CORRIDOR");
        if (current != null && current.IsStarted && !current.IsCompleted)
        {
            GUILayout.Label(current.IsFinalPush ? (current.ExitReady ? "FINAL PUSH / REACH EXIT" : "FINAL PUSH") :
                $"CHECKPOINT {current.Route.Completed} / {current.Route.CheckpointCount}");
            GUILayout.Label(current.UnderPressure ? "COLLAPSE: MOVE FORWARD" :
                current.IsFinalPush ? "KEEP MOVING / REACH EXIT" : "Follow the cyan gate");
        }
        else
        {
            GUILayout.Label($"{Tuning.routePreset} / {turns * 90} degrees / enemies {(crowd ? "ON" : "OFF")}");
            if (GUILayout.Button("F5: start / restart")) StartCorridor();
            GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }
        GUILayout.Label("F5 restart  F6 route  F7 rotate  F8 enemies");
        GUILayout.EndArea();
        GUI.color = previousColor;
        GUI.skin.font = previousFont;
    }
}
#endif
