using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Game/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponName;

    [TextArea(3, 6)]
    public string description;

    public Sprite icon;

    [Header("Prefab")]
    public GameObject weaponPrefab;

    [Header("Combat Stats")]
    public int damage = 10;
    public float fireRate = 0.5f;
    public float range = 10f;
    public int bulletsPerShot = 1;
    public int pierce = 0;

    [Header("UI Preview")]
    public int fireRateRPM = 180;

    [TextArea(2, 4)]
    public string specialDescription;

    [Header("Sound")]
    public AudioClip attackSound;
    public Vector2 pitchRange = new Vector2(0.96f, 1.04f);
    public float soundVolume = 0.35f;
}