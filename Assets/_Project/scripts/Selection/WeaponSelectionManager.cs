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
        if (weapon == null)
            return;

        selectedWeapon = weapon;

        int index = System.Array.IndexOf(allWeapons, weapon);

        if (index >= 0)
        {
            PlayerPrefs.SetInt("SelectedWeapon", index);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogWarning("WeaponSelectionManager: selected weapon is not in allWeapons array: " + weapon.weaponName);
        }

        Debug.Log("Selected weapon: " + weapon.weaponName);
    }

    public WeaponData GetSelectedWeapon()
    {
        if (selectedWeapon != null)
            return selectedWeapon;

        int index = PlayerPrefs.GetInt("SelectedWeapon", -1);

        if (allWeapons != null && index >= 0 && index < allWeapons.Length)
            selectedWeapon = allWeapons[index];

        return selectedWeapon;
    }
    public void ClearSelection()
    {
        selectedWeapon = null;
        PlayerPrefs.DeleteKey("SelectedWeapon");
    }
}