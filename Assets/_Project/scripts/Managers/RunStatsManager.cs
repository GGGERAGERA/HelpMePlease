using UnityEngine;

public class RunStatsManager : MonoBehaviour
{
    public static RunStatsManager Instance;

    public int Kills { get; private set; }
    public float RunTime { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        RunTime += Time.deltaTime;
    }

    public void AddKill()
    {
        Kills++;
    }
}
