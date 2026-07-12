using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Game/LevelData")]
public class LevelData : ScriptableObject
{
    public int baseExpToNextLevel = 100; // Базовое количество опыта для следующего уровня
    public float expGrowth = 1.2f; // Коэффициент роста опыта для каждого следующего уровня
    public int maxLevel = 10; // Максимальный уровень

}
