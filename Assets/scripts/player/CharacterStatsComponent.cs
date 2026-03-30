// CharacterStatsComponent.cs
using UnityEngine;

// Компонент, который висит НА ПРЕФАБЕ ИГРОКА
public class CharacterStatsComponent : MonoBehaviour
{
    public int CharacterHealth = 100;
    public int CharacterShield = 5;
    public int CharacterDamage = 10;
    public float CharacterSpeed = 1.5f;
    public float CharacterWeaponRate = 0.5f;
    public float CharacterMoneyBonus = 2.5f;
    public int Luck = 1;
}