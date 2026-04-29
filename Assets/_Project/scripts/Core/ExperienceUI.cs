using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExperienceUI : MonoBehaviour
{
    [Header("Level Display")]
    public TextMeshProUGUI levelText;     // сюда перетащите Text (TMP) с "LVL: 1
    public Slider levelSlider;            // полоса прогресса

    private ExperienceManager expManager;

    void Start()
    {
        expManager = ExperienceManager.Instance;
        if (expManager != null)
        {
            expManager.OnLevelUp.AddListener(UpdateLevel);
            expManager.OnExperienceChanged.AddListener(UpdateExp);

            UpdateLevel(expManager.currentLevel);
            if (expManager.OnExperienceChanged != null && expManager.currentExp != 0)
                UpdateExp(expManager.currentExp, expManager.GetRequiredExpForCurrentLevel());
        }
    }

    void UpdateLevel(int newLevel)
    {
        if (levelText != null)
            levelText.text = $"LVL: {newLevel}";   // или "Level " + newLevel

        // Обновляем максимальное значение слайдера при переходе на новый уровень
        if (levelSlider != null && expManager != null)
            levelSlider.maxValue = expManager.GetRequiredExpForCurrentLevel();
    }

    void UpdateExp(int current, int required)
    {

        if (levelSlider != null)
            levelSlider.value = current;
    }
}