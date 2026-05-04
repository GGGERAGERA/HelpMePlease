using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Game/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public GameObject weaponPrefab;
    public int damage = 10;
    public float fireRate = 0.5f;
    public float range = 10f;
    public Sprite icon;
    public AudioClip attackSound;
}