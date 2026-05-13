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
        Kills++;

        HUDManager.Instance?.SetKills(Kills);
    }
}