using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class PlayerBuffs
{
    [Header("Buffs")]
    [Tooltip("Ссылка на ScriptableObject с параметрами игрока")]
    [SerializeField] private PlayerBuffsSO PlayerBuffs1;
    public int AttackBonus { get; private set; }
    private float duration;

    public PlayerBuffs(PlayerBuffsSO PlayerBuffs1)
    {
        AttackBonus = PlayerBuffs1.PlayerBuffsMaxHealth;
        //duration = so.Duration;
    }

    public IEnumerator DeleteBuff()
    {
        yield return new WaitForSeconds(duration);
        // Удаляем из списка активных баффов (в PlayerStats)
    }
}
