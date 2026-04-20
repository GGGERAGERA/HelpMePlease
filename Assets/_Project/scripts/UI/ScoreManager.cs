using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("UI References")]
    public Text crystal;
    public Text gemsText;

    private int crystalCount = 0;
    private int gems = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        UpdateUI();
    }

    public void AddCrystal(int amount)
    {
        crystalCount += amount;
        UpdateUI();
    }

    public void AddGems(int amount)
    {
        
        gems += amount;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (crystal != null) crystal.text = "Crystals: " + crystalCount;
        if (gemsText != null) gemsText.text = "Gems: " + gems;
    }
}