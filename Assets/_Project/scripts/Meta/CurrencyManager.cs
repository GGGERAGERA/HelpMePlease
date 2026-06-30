using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    [ContextMenu("Добавить 1000 монет (Debug)")] //Теперь в инспекторе можно кликнуть правой кнопкой 
    //по скрипту CurrencyManager и начислить себе монеты.
    public void DebugAddCoins()
    {
        AddGold(1000);
    }
    public static CurrencyManager Instance;

    public int TotalGold { get; private set; }
    private float goldGainMultiplier = 1f;

    private const string GoldKey = "TOTAL_GOLD";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
        int finalAmount = Mathf.RoundToInt(amount * goldGainMultiplier);
        TotalGold += finalAmount;
        SaveGold();
    }

    public bool SpendGold(int amount)
    {
        if (TotalGold < amount)
            return false;

        TotalGold -= amount;
        SaveGold();

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
        goldGainMultiplier *= 1f + percent;
    }
}