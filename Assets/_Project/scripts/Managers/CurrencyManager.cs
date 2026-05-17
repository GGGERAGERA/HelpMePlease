using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    public int TotalGold { get; private set; }

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
        TotalGold += amount;
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
}