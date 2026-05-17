using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Experience")]
    [SerializeField] private Slider experienceSlider;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI experienceText;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI killsText;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Boss")]
    [SerializeField] private TextMeshProUGUI bossText;

    [Header("Low HP")]
    [SerializeField] private CanvasGroup lowHpVignette;
    [SerializeField] private float lowHpThreshold = 0.3f;

    private void Awake()
    {
        Instance = this;
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
        UpdateLowHpVignette(currentHealth, maxHealth);
    }

    public void SetTimer(float timeLeft)
    {
        timeLeft = Mathf.Max(0f, timeLeft);

        int minutes = Mathf.FloorToInt(timeLeft / 60f);
        int seconds = Mathf.FloorToInt(timeLeft % 60f);

        if (timerText != null)
            timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void SetKills(int kills)
    {
        if (killsText != null)
        {
            killsText.text = $"KILLS: {kills}";
        }
    }
    public void SetExperience(int currentExp, int requiredExp, int level)
    {
        if (experienceSlider != null)
        {
            experienceSlider.maxValue = requiredExp;
            experienceSlider.value = currentExp;
        }

        if (levelText != null)
        {
            levelText.text = $"Lv. {level}";
        }

        if (experienceText != null)
        {
            experienceText.text = $"{currentExp} / {requiredExp}";
        }
    }
    public void ShowBossText(string text, float duration = 5f)
    {
        StartCoroutine(ShowBossTextRoutine(text, duration));
    }
    private void UpdateLowHpVignette(float currentHealth, float maxHealth)
    {
        if (lowHpVignette == null || maxHealth <= 0f)
            return;

        float healthPercent = currentHealth / maxHealth;

        if (healthPercent > lowHpThreshold)
        {
            lowHpVignette.alpha = 0f;
            return;
        }

        float danger = 1f - (healthPercent / lowHpThreshold);
        lowHpVignette.alpha = Mathf.Lerp(0.15f, 0.55f, danger);
    }


    private IEnumerator ShowBossTextRoutine(string text, float duration)
    {
        if (bossText == null)
            yield break;

        bossText.text = text;
        bossText.gameObject.SetActive(true);

        yield return new WaitForSeconds(duration);

        bossText.text = "";
        bossText.gameObject.SetActive(false);
    }
}