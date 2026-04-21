using UnityEngine;
using UnityEngine.UI;

public class ExperienceUI : MonoBehaviour
{
    public Text levelText;
    public Text expText;

    void Start()
    {
        // Подписываемся на события
        ExperienceManager.Instance.OnLevelUp.AddListener(UpdateLevel);
        ExperienceManager.Instance.OnExperienceChanged.AddListener(UpdateExp);
        // Инициализация
        UpdateLevel(ExperienceManager.Instance.currentLevel);
        UpdateExp(ExperienceManager.Instance.currentExp, ExperienceManager.Instance.GetRequiredExpForLevel(ExperienceManager.Instance.currentLevel));
    }

    void UpdateLevel(int newLevel)
    {
        levelText.text = "Level: " + newLevel;
    }

    void UpdateExp(int current, int required)
    {
        expText.text = $"Exp: {current} / {required}";
    }
}