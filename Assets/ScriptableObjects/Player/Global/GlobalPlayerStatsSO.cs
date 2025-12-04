using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PlayerStats", menuName = "ScriptableObject/Player/GlobalPlayerPercs")]
public class GlobalPlayerStatsSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Player")]
    
    [Header("GlobalPlayerStats")]
        public string GlobalplayerName = "Hero1";
    public int Globalplayerkills = 0;
    public int GlobalplayerMoney = 0;
    public int GlobalplayerMaxHealth = 50;
    public float GlobalHPrecover = 0.01f; //0.01 HP per Second or frame
    public int GlobalShield = 1;
    public float GlobalplayerSpeed = 1f;
    public int GlobalAttackBonus = 1;

    //public float Globalpower = 0f;
    public float GlobalPprojectileSpeed = 1f;
    public float Globalrange = 1f;
    public float Globalreload = 2.5f; //reload time
    public int GlobalReanimate = 0;
    public float Globalmagnet = 1f;
    public float GlobalLuck = 0.05f;
    public float GlobalUpgrade = 0f; //бафф к знаниям (апгрейд уровня)
    public int GlobalSkip = 0; //Пропуск карточек

}