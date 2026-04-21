using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Game/LevelData")]
public class LevelData : ScriptableObject
{
    public int baseExpToNextLevel = 100; // Базовое количество опыта для следующего уровня
    public float expGrowthRate = 1.5f; // Коэффициент роста опыта для каждого следующего уровня
    public int maxLevel = 10; // Максимальный уровень

}
