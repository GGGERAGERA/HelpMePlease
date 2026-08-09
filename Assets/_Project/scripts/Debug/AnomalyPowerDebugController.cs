using UnityEngine;

public sealed class AnomalyPowerDebugController : MonoBehaviour
{
    private readonly System.Collections.Generic.List<EnemyHealth>
        killAllTargets = new(128);
    private Transform player;
    private GravityOrbPower gravityOrb;
    private ArcNodePower arcNode;
    private RedBeamPower redBeam;
    private PowerTestController powerTest;
    private bool gravityOrbEnabled;
    private bool arcNodeEnabled;
    private bool redBeamEnabled;
    private bool gravityOrbSiteLocked;

    public bool GravityOrbEnabled => gravityOrbEnabled;
    public bool ArcNodeEnabled => arcNodeEnabled;
    public bool RedBeamEnabled => redBeamEnabled;
    public bool GravityOrbSiteLocked => gravityOrbSiteLocked;

    public void Configure(PowerTestController test)
    {
        powerTest = test;
    }

    private void Update()
    {
        ResolvePlayer();

        if (Input.GetKeyDown(KeyCode.Alpha4) && !gravityOrbSiteLocked)
        {
            gravityOrbEnabled = !gravityOrbEnabled;
            ApplyPowerStates();
            LogState("Gravity Orb", gravityOrbEnabled);
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            arcNodeEnabled = !arcNodeEnabled;
            ApplyPowerStates();
            LogState("Arc Node", arcNodeEnabled);
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            redBeamEnabled = !redBeamEnabled;
            ApplyPowerStates();
            LogState("Red Beam", redBeamEnabled);
        }

        if (Input.GetKeyDown(KeyCode.F8))
            KillAllEnemies();
    }

    public void BeginGravitySiteRewardLock()
    {
        gravityOrbSiteLocked = true;
        gravityOrbEnabled = false;
        ApplyPowerStates();
    }

    public void GrantGravityOrbFromSite()
    {
        gravityOrbSiteLocked = false;
        gravityOrbEnabled = true;
        ApplyPowerStates();
    }

    public void ClearGravitySiteReward()
    {
        gravityOrbSiteLocked = false;
        gravityOrbEnabled = false;
        ApplyPowerStates();
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        player = playerObject.transform;
        gravityOrb = playerObject.GetComponent<GravityOrbPower>();
        if (gravityOrb == null)
            gravityOrb = playerObject.AddComponent<GravityOrbPower>();

        arcNode = playerObject.GetComponent<ArcNodePower>();
        if (arcNode == null)
            arcNode = playerObject.AddComponent<ArcNodePower>();

        redBeam = playerObject.GetComponent<RedBeamPower>();
        if (redBeam == null)
            redBeam = playerObject.AddComponent<RedBeamPower>();

        ApplyPowerStates();
    }

    private void ApplyPowerStates()
    {
        if (gravityOrb != null)
            gravityOrb.enabled = gravityOrbEnabled;

        if (arcNode != null)
            arcNode.enabled = arcNodeEnabled;

        if (redBeam != null)
            redBeam.enabled = redBeamEnabled;
    }

    private static void LogState(string powerName, bool enabled)
    {
        Debug.Log($"[AnomalyPowers] {powerName}: {(enabled ? "ON" : "OFF")}");
    }

    private void KillAllEnemies()
    {
        killAllTargets.Clear();

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy != null && !enemy.IsDead)
                killAllTargets.Add(enemy);
        }

        int killed = 0;
        for (int i = 0; i < killAllTargets.Count; i++)
        {
            EnemyHealth enemy = killAllTargets[i];
            if (enemy == null || enemy.IsDead)
                continue;

            enemy.TakeDamage(float.MaxValue, enemy.transform.position, false);
            if (enemy.IsDead)
                killed++;
        }

        killAllTargets.Clear();
        Debug.Log($"DEBUG KILL ALL: killed {killed} enemies");
    }

    private void OnGUI()
    {
        int enemiesAlive = EnemyHealth.ActiveInstances.Count;
        int kills = powerTest != null ? powerTest.Kills : 0;
        float killsPerSecond = powerTest != null
            ? powerTest.KillsPerSecond
            : 0f;
        string status =
            "CONTROLS\n" +
            "F1  Weapon Menu\n" +
            "0    Core: None\n" +
            "2    Core: Chain\n" +
            "4    Gravity Orb ON/OFF\n" +
            "5    Arc Node ON/OFF\n" +
            "6    Red Beam ON/OFF\n" +
            "F4  Start / Reset Power Test\n" +
            "Shift+F4  Stop Power Test\n" +
            "F5  Toggle Weapon Core\n" +
            "E    Start Gravity Trial (near site)\n" +
            "F6  Start / Reset Gravity Site\n" +
            "Shift+F6  Stop Gravity Site\n" +
            "F8  KILL ALL ENEMIES\n\n" +
            "STATUS\n" +
            $"Weapon Core: {WeaponCoreDebugSelector.ActiveCore}\n" +
            $"Gravity Orb: {(gravityOrbEnabled ? "ON" : gravityOrbSiteLocked ? "LOCKED" : "OFF")}\n" +
            $"Arc Node: {(arcNodeEnabled ? "ON" : "OFF")}\n" +
            $"Red Beam: {(redBeamEnabled ? "ON" : "OFF")}\n" +
            $"Enemies Alive: {enemiesAlive}\n" +
            $"Kills: {kills}\n" +
            $"Kills/sec: {killsPerSecond:F1}";
        GUI.Box(
            new Rect(Screen.width - 345f, 14f, 330f, 415f),
            status
        );
    }

    private void OnDestroy()
    {
        if (gravityOrb != null)
            gravityOrb.enabled = false;

        if (arcNode != null)
            arcNode.enabled = false;

        if (redBeam != null)
            redBeam.enabled = false;
    }
}
