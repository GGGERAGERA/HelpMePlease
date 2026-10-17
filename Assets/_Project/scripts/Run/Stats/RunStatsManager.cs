using UnityEngine;

public class RunStatsManager : MonoBehaviour
{
    public static RunStatsManager Instance;
    public event System.Action RewardRelevantStatsChanged;
    public event System.Action ProgressChanged;

    public int Kills { get; private set; }
    public float KillRewardUnits { get; private set; }
    public float RunTime { get; private set; }
    private int elapsedRewardMinutes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ProgressChanged += RefreshExpedition;
    }

    private void OnDestroy()
    {
        ProgressChanged -= RefreshExpedition;
        if (Instance == this)
            Instance = null;
    }

    private void RefreshExpedition() => RunStateManager.Instance?.RefreshExpeditionProgress();

    private void Update()
    {
        RunTime += Time.deltaTime;
        // Notify on the frame the accumulated run-time threshold is crossed,
        // including when the previous scene ended partway through a minute.
        ProgressChanged?.Invoke();

        int currentMinute = Mathf.FloorToInt(RunTime / 60f);

        if (currentMinute != elapsedRewardMinutes)
        {
            elapsedRewardMinutes = currentMinute;
            RewardRelevantStatsChanged?.Invoke();
        }
    }

    public void AddKill()
    {
        AddKill(1f);
    }

    public void AddKill(float rewardMultiplier)
    {
        Kills++;
        KillRewardUnits += Mathf.Max(1f, rewardMultiplier);
        ProgressChanged?.Invoke();
        RewardRelevantStatsChanged?.Invoke();
    }
}
