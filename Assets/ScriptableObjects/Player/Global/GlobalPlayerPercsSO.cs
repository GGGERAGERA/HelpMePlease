using UnityEngine;

// Это создаст пункт в меню создания ассетов
[CreateAssetMenu(fileName = "PlayerStats", menuName = "ScriptableObject/Player/GlobalPlayerPercs")]
public class GlobalPleerPercsSO : ScriptableObject
{
    // Это публичное поле будет хранить наш выбор
    // Можно хранить индекс, имя, префаб - что угодно!
    
    [Header("Player")]
    
    [Header("GlobalPlayerStats")]
        public string GlobalplayerName = "Hero1";
    public int Globalplayerkills = 0;
    public int GlobalplayerMoney = 0;
    public int GlobalplayerMaxHealth = 0;
    public float GlobalHPrecover = 0;
    public int GlobalShield = 0;
    public float GlobalplayerSpeed = 0f;

    public float Globalpower = 0f;
    public float GlobalprojectileSpeed = 0f;
    public float Globalrange = 0f;
    public float Globalreload = 0f;
    public int GlobalReanimate = 0;
    public float Globalmagnet = 0f;
    public float GlobalLuck = 0f;
    public float Globalupgrade = 0f;
    public int Globalskip = 0;

}