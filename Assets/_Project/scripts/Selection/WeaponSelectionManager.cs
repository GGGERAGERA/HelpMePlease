using UnityEngine;

public class WeaponSelectionManager : MonoBehaviour
{
    public static WeaponSelectionManager Instance { get; private set; }

    [Header("All Weapons")]
    [SerializeField] private WeaponData[] allWeapons;

    private WeaponData selectedWeapon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectWeapon(WeaponData weapon)
    {
        selectedWeapon = weapon;

        int index = System.Array.IndexOf(allWeapons, weapon);
        PlayerPrefs.SetInt("SelectedWeapon", index);
        PlayerPrefs.Save();

        Debug.Log("Selected weapon: " + weapon.weaponName);
    }

    public WeaponData GetSelectedWeapon()
    {
        return selectedWeapon;
    }
}