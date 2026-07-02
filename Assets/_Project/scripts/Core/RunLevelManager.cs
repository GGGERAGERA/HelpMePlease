using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RunLevelManager : MonoBehaviour
{
    public static RunLevelManager Instance { get; private set; }

    [Header("Player Reset")]
    [SerializeField] private Transform levelStartPoint;
    [SerializeField] private bool healPlayerOnNextLevel = false;
    [SerializeField] private float healPercentOnNextLevel = 0.35f;

    [Header("Scaling")]
    [SerializeField] private float enemyHealthMultiplierPerLevel = 1.35f;
    [SerializeField] private float enemySpeedMultiplierPerLevel = 1.12f;
    [SerializeField] private float spawnRateMultiplierPerLevel = 0.85f;

    [Header("Level Lighting")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private int darkLevel = 2;
    [SerializeField] private float darkLevelIntensity = 0.1f;

    public int CurrentLevel => RunStateManager.Instance != null
    ? RunStateManager.Instance.CurrentLevel
    : 1;

    private bool isChangingLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }


    public int GetNextLevelNumber()
    {
        return CurrentLevel + 1;
    }
}