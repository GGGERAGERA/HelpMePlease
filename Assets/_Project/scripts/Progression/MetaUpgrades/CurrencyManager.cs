using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    [ContextMenu("Добавить 1000 монет (Debug)")]
    public void DebugAddCoins()
    {
        AddGold(1000);
    }

    public static CurrencyManager Instance;
    public int TotalGold { get; private set; }

    // Ивент, на который будут подписываться скрипты UI
    public System.Action<int> OnGoldUpdated; 

    private float goldGainMultiplier = 1f;
    private const string GoldKey = "TOTAL_GOLD";

    private void Awake()
{
    if (Instance == null)
    {
        Instance = this;
        
        // Фикс ошибки: делаем объект корневым, чтобы DontDestroyOnLoad работал
        if (transform.parent != null)
        {
            transform.SetParent(null); 
        }
        
        DontDestroyOnLoad(gameObject);
        LoadGold();
    }
    else
    {
        Destroy(gameObject);
    }
}

    public void AddGold(int amount)
    {
        Debug.Log(
            $"[CurrencyManager] AddGold called: +{amount}. " +
            $"Before={TotalGold}"
        );
        int finalAmount = Mathf.RoundToInt(amount * goldGainMultiplier);
        TotalGold += finalAmount;
        SaveGold();
        
        // Оповещаем всех подписчиков, что золото изменилось и передаем новое число
        OnGoldUpdated?.Invoke(TotalGold);
    }

    public bool SpendGold(int amount)
    {
        if (TotalGold < amount)
            return false;

        TotalGold -= amount;
        SaveGold();
        OnGoldUpdated?.Invoke(TotalGold);
        return true;
    }

    private void SaveGold()
    {
        PlayerPrefs.SetInt(GoldKey, TotalGold);
        PlayerPrefs.Save();
    }

    private void LoadGold()
    {
        TotalGold = PlayerPrefs.GetInt(GoldKey, 0);
    }

    public void AddGoldGainPercent(float percent)
    {
        // CurrencyManager persists across MVP reloads while the meta applier is
        // scene-local. Re-applying the same saved bonus must be idempotent.
        goldGainMultiplier = 1f + Mathf.Max(0f, percent);
    }
}
