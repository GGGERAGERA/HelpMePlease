using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    }
    public void SetExperience(int currentExp, int requiredExp, int level)
    {
        Debug.Log($"HUD XP: {currentExp}/{requiredExp}, level {level}");
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
}