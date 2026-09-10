using System.Collections;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

/// <summary>Scene-local comparison controls. All environment content is authored, never generated in Play Mode.</summary>
public sealed class SurfaceVisualLab : MonoBehaviour
{
    public GameObject[] Presets;
    public GameObject Player;
    public CharacterData Character;
    public Camera GameplayCamera;
    public GameObject[] Enemies;
    public GameObject[] EnemyPrefabs;
    public Transform EnemyRoot;
    public GameObject XPPrefab;
    public GameObject CratePrefab;
    public GameObject PickupGroup;
    public GameObject[] Fixtures;
    public ParticleSystem[] ImpactSamples;
    public int Preset = 3;
    public int Scenario = 2;
    public bool ShowHelp = true;
    public bool FollowPlayer = true;
    public OrbitalStationRuntime Station { get; private set; }
    public int Hits { get; private set; }
    public bool Ready { get; private set; }
    Vector3[] enemyAnchors;
    Vector3[] pickupAnchors;
    Vector3[] fixtureAnchors;
    float fxTimer;
    RunStateManager ownedRun;
    static readonly string[] Names = { "QUIET WASTELAND", "BIOPUNK WASTELAND", "RUINED FACILITY OUTSKIRTS", "ART DIRECTED / COLD ASH" };

    IEnumerator Start()
    {
        // This scene is entered directly from Edit Mode, with its own transient run.
        if (RunStateManager.Instance != null)
        {
            Debug.LogError("SurfaceVisualLab must start directly from Edit Mode; refusing to replace an existing run.", this);
            enabled = false; yield break;
        }
        ownedRun = RunStateManager.EnsureExists();
        ownedRun.BeginNewRun(Character, null);
        Player.SetActive(true);
        Player.GetComponent<PlayerHealth>().SetIncomingDamageMultiplier(0);
        Station = OrbitalStationRuntime.Ensure(Player, Character);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Station.ApplyPresetMid();
#endif
        Station.Combat.Hit += OnHit;
        enemyAnchors = new Vector3[Enemies.Length];
        for (int i = 0; i < Enemies.Length; i++) enemyAnchors[i] = Enemies[i].transform.position;
        pickupAnchors = new Vector3[PickupGroup.transform.childCount];
        for (int i = 0; i < pickupAnchors.Length; i++) pickupAnchors[i] = PickupGroup.transform.GetChild(i).position;
        fixtureAnchors = new Vector3[Fixtures.Length];
        for (int i = 0; i < Fixtures.Length; i++) fixtureAnchors[i] = Fixtures[i].transform.position;
        yield return null;
        SetScenario(Scenario);
        SelectPreset(Preset);
        Ready = true;
    }
    void OnHit(EnemyHealth enemy, float damage) { Hits++; }
    public void SelectPreset(int index)
    {
        Preset = Mathf.Clamp(index, 0, 3);
        for (int i = 0; i < Presets.Length; i++) Presets[i].SetActive(i == Preset);
    }
    public void SetScenario(int index)
    {
        Scenario = Mathf.Clamp(index, 0, 3);
        Player.transform.position = Vector3.zero;
        var body = Player.GetComponent<Rigidbody2D>(); body.position = Vector2.zero; body.linearVelocity = Vector2.zero;
        GameplayCamera.transform.position = new Vector3(0, 0, -10);
        Station.gameObject.SetActive(Scenario >= 2);
        PickupGroup.SetActive(Scenario >= 2);
        foreach (Transform pickup in PickupGroup.transform)
        { pickup.gameObject.SetActive(false); Destroy(pickup.gameObject); }
        foreach (var anchor in pickupAnchors) Instantiate(XPPrefab, anchor, Quaternion.identity, PickupGroup.transform);
        for (int i = 0; i < Fixtures.Length; i++)
        {
            if (Fixtures[i] == null || Fixtures[i].GetComponent<WorldBreakable>().IsBroken)
            {
                if (Fixtures[i] != null) { Fixtures[i].SetActive(false); Destroy(Fixtures[i]); }
                Fixtures[i] = Instantiate(CratePrefab, fixtureAnchors[i], Quaternion.identity);
            }
            Fixtures[i].SetActive(Scenario >= 2);
        }
        for (int i = 0; i < Enemies.Length; i++)
        {
            var enemy = Enemies[i];
            // A live bomber owns a delayed explosion sequence. Reset the complete prefab lifecycle,
            // not just its transform/health, so a previous scenario cannot destroy a new target.
            if (enemy != null) { enemy.SetActive(false); Destroy(enemy); }
            enemy = Enemies[i] = Instantiate(EnemyPrefabs[i % EnemyPrefabs.Length], enemyAnchors[i], Quaternion.identity, EnemyRoot);
            bool active = Scenario > 0 && (Scenario == 3 || i < 24);
            enemy.SetActive(active);
            enemy.transform.position = enemyAnchors[i];
            var rb = enemy.GetComponent<Rigidbody2D>(); rb.position = enemyAnchors[i]; rb.linearVelocity = Vector2.zero;
            rb.constraints = Scenario == 3 ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.FreezeAll;
            // Long-lived targets keep the comparison population stable while production damage/FX run.
            enemy.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(100000);
            foreach (var movement in enemy.GetComponents<EnemyMovement>()) movement.enabled = Scenario == 3;
        }
        foreach (var fx in ImpactSamples) fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void Update()
    {
        if (!Ready) return;
        if (Input.GetKeyDown(KeyCode.F5)) SetScenario(0);
        if (Input.GetKeyDown(KeyCode.F6)) SetScenario(1);
        if (Input.GetKeyDown(KeyCode.F7)) SetScenario(2);
        if (Input.GetKeyDown(KeyCode.F8)) SetScenario(3);
        if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
        if (Input.GetKeyDown(KeyCode.R)) SetScenario(Scenario);
        if (Scenario >= 2 && (fxTimer -= Time.deltaTime) <= 0)
        {
            fxTimer = 1.6f;
            foreach (var fx in ImpactSamples) fx.Play(true);
        }
    }
    void LateUpdate()
    {
        if (!Ready) return;
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectPreset(i);
                // The production development shortcuts also select rings with 1–4.
                // Clear only their transient highlight after Update so comparison lighting stays equal.
                foreach (var ring in Station.Rings) ring.SetSelected(false);
            }
        if (!FollowPlayer) return;
        Vector3 p = Player.transform.position;
        // Exact world texel grid: 32 PPU at the authored 768x432 render resolution.
        GameplayCamera.transform.position = new Vector3(Mathf.Round(p.x * 32) / 32, Mathf.Round(p.y * 32) / 32, -10);
    }
    void OnGUI()
    {
        if (!ShowHelp) return;
        GUI.color = new Color(.8f, .84f, .86f);
        GUI.Label(new Rect(16, 12, 680, 24), "SURFACE VISUAL LAB     " + (Preset + 1) + " / " + Names[Preset]);
        GUI.Label(new Rect(16, Screen.height - 30, 1100, 24), "1–4  location     F5  solo     F6  24 enemies     F7  ORBITAL + FX     F8  live crowd     WASD  move     R  reset     H  hide UI");
    }
    void OnDestroy()
    {
        if (Station != null && Station.Combat != null) Station.Combat.Hit -= OnHit;
        if (ownedRun != null) Destroy(ownedRun.gameObject);
    }
}
