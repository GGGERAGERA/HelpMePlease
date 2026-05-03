using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelSelection : MonoBehaviour
{
    public Slider levelSlider;
    public TextMeshProUGUI levelNumberText;
    public int maxLevel = 5;

    private int currentLevel = 1;

    void Start()
    {
        if (levelSlider != null)
        {
            levelSlider.minValue = 1;
            levelSlider.maxValue = maxLevel;
            levelSlider.wholeNumbers = true;
            levelSlider.value = currentLevel;
            levelSlider.onValueChanged.AddListener(OnLevelChanged);
        }
        UpdateText();
    }

    void OnLevelChanged(float value)
    {
        currentLevel = Mathf.RoundToInt(value);
        UpdateText();
        PlayerPrefs.SetInt("SelectedLevel", currentLevel);
        PlayerPrefs.Save();
    }

    void UpdateText()
    {
        if (levelNumberText != null)
            levelNumberText.text = $"Level {currentLevel}";
    }
}