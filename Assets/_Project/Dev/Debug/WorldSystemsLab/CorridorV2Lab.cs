#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;

public sealed class CorridorV2Lab : MonoBehaviour
{
    private WorldSystemsLabController lab;
    private GameplayAreaService area;
    private RocketHazardDefinition rocket;
    private GameObject enemyPrefab;
    private EnemySpawner enemies;
    private CorridorV2Event current;
    private Transform bounds;
    private PlayerHealth playerHealth;
    private SpriteRenderer[] playerSprites;
    private bool[] spriteEnabled;
    private bool movementEnabled;
    private Vector3 areaScale, boundsScale;
    private bool expanded, bent, crowd = true;
    private int turns;
    private string status = "F5: start Corridor V2";

    public void Initialize(WorldSystemsLabController owner, GameplayAreaService gameplayArea,
        RocketHazardDefinition rocketAsset, GameObject normalEnemy)
    {
        lab = owner; area = gameplayArea; rocket = rocketAsset; enemyPrefab = normalEnemy;
        playerHealth = lab.Player.GetComponent<PlayerHealth>();
        playerSprites = lab.Player.GetComponentsInChildren<SpriteRenderer>(true);
        spriteEnabled = playerSprites.Select(sprite => sprite.enabled).ToArray();
        movementEnabled = lab.Player.GetComponent<CharacterMovement2D>().enabled;
        bounds = gameObject.scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Environment Bounds")?.transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6)) bent = !bent;
        if (Input.GetKeyDown(KeyCode.F7)) turns = (turns + 1) % 4;
        if (Input.GetKeyDown(KeyCode.F8)) SetCrowd(!crowd);
        if (Input.GetKeyDown(KeyCode.F5)) StartCorridor();
    }

    public void StartCorridor()
    {
        if (rocket == null || !rocket.IsConfigured || enemyPrefab == null || lab.Player == null || area == null || bounds == null)
        {
            status = "Missing lab references. Rebuild WorldSystemsLab from Tools menu.";
            return;
        }
        lab.PrepareCorridorV2();
        areaScale = area.transform.localScale;
        boundsScale = bounds.localScale;
        area.transform.localScale = areaScale * 1.6f;
        bounds.localScale = boundsScale * 1.6f;
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
        var route = new CorridorV2Route(bent, turns);
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
        current.Configure(lab.Player, bent, turns, rocket);
        current.Finished = message =>
        {
            status = message;
            enemies?.StopDebugExplorationPressure();
        };
        current.gameObject.SetActive(true);
        current.StartEvent();
        if (!current.IsStarted)
        {
            lab.ClearEvents();
            status = "Could not start event. Check the active event/tutorial state.";
            return;
        }
        status = $"{(bent ? "L" : "Straight")} / {turns * 90} degrees / 120 units / 25s";
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
        enemies.ConfigureDebugExplorationPressure(new[] { enemyPrefab }, .7f, 36, 1, 9f, 15f);
    }

    public void Stop()
    {
        if (current != null) current.Finished = null;
        // Event removal is owned by WorldSystemsLabController.ClearEvents.
        current = null;
        enemies?.StopDebugExplorationPressure();
        enemies?.ClearDebugSpawnedEnemies();
        if (expanded)
        {
            area.transform.localScale = areaScale;
            bounds.localScale = boundsScale;
            expanded = false;
            lab.TeleportPlayerCenter();
        }
        status = "Stopped / F5 to restart";
    }

    private void OnGUI()
    {
        float width = Mathf.Min(355f, Screen.width * .45f);
        GUILayout.BeginArea(new Rect(Screen.width - width - 8, 8, width, 285), GUI.skin.box);
        GUILayout.Label("CORRIDOR V2 - GAMEPLAY PROTOTYPE");
        GUILayout.Label($"Next run: {(bent ? "L-shaped" : "Straight")} / {turns * 90} degrees");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("F6: layout")) bent = !bent;
        if (GUILayout.Button("F7: rotate")) turns = (turns + 1) % 4;
        GUILayout.EndHorizontal();
        if (GUILayout.Button("F5: start / restart (heal)")) StartCorridor();
        if (GUILayout.Button($"F8: normal enemies {(crowd ? "ON" : "OFF")}")) SetCrowd(!crowd);
        if (GUILayout.Button("Stop / clear")) lab.ClearEvents();
        if (current != null && current.IsStarted && !current.IsCompleted)
        {
            GUILayout.Label($"TIME {current.Remaining:F1}s   CP {current.Route.Completed}/3   EXIT {(current.Route.ExitOpen ? "OPEN" : "LOCKED")}");
            GUILayout.Label($"HP {playerHealth.CurrentHealth:F0} | boundary hits {current.BoundaryHits} | enemies {enemies?.DebugTrackedEnemyCount ?? 0}");
            GUILayout.Label(current.Outside ? "OUTSIDE: 12 damage/sec - return to route!" : current.Pattern);
        }
        GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
        GUILayout.Label("WASD/arrows: move. Space: dash. F1: hide left panel.");
        GUILayout.EndArea();
    }
}
#endif
