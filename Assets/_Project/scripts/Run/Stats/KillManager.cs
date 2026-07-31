using UnityEngine;

public class KillManager : MonoBehaviour
{
    public static KillManager Instance;

    public int Kills { get; private set; }

    private void Start()
    {
        HUDManager.Instance?.SetKills(Kills);
    }
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void AddKill()
    {
        AddKill(1f);
    }

    public void AddKill(float rewardMultiplier)
    {
        Kills++;
        HUDManager.Instance?.SetKills(Kills);
        RunStatsManager.Instance?.AddKill(rewardMultiplier);
    }
}
