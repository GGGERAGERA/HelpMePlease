using UnityEngine;

public class RunStatsManager : MonoBehaviour
{
    public static RunStatsManager Instance;
    public event System.Action RewardRelevantStatsChanged;

    public int Kills { get; private set; }
    public float RunTime { get; private set; }
    private int elapsedRewardMinutes;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        RunTime += Time.deltaTime;

        int currentMinute = Mathf.FloorToInt(RunTime / 60f);

        if (currentMinute != elapsedRewardMinutes)
        {
            elapsedRewardMinutes = currentMinute;
            RewardRelevantStatsChanged?.Invoke();
        }
    }

    public void AddKill()
    {
        Kills++;
        RewardRelevantStatsChanged?.Invoke();
    }
}
