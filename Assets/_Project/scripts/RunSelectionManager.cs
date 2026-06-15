using UnityEngine;

public class RunSelectionManager : MonoBehaviour
{
    public static RunSelectionManager Instance { get; private set; }

    public CharacterData SelectedCharacter { get; private set; }
    public WeaponData SelectedWeapon { get; private set; }

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

    public void SelectCharacter(CharacterData character)
    {
        if (character == null)
        {
            Debug.LogWarning("RunSelectionManager: tried to select null character.");
            return;
        }

        SelectedCharacter = character;
    }

    public void SelectWeapon(WeaponData weapon)
    {
        if (weapon == null)
        {
            Debug.LogWarning("RunSelectionManager: tried to select null weapon.");
            return;
        }

        SelectedWeapon = weapon;
    }

    public bool HasCharacter()
    {
        return SelectedCharacter != null;
    }

    public bool HasWeapon()
    {
        return SelectedWeapon != null;
    }

    public bool IsReady()
    {
        return HasCharacter() && HasWeapon();
    }

    public void ClearRunSelection()
    {
        SelectedCharacter = null;
        SelectedWeapon = null;
    }
}
