using UnityEngine;

public sealed class RunSelectionManager : MonoBehaviour
{
    public static RunSelectionManager Instance { get; private set; }

    public CharacterData SelectedCharacter { get; private set; }
    public WeaponData SelectedWeapon { get; private set; }

    public bool HasCharacter => SelectedCharacter != null;
    public bool HasWeapon => SelectedWeapon != null;
    public bool IsReady => HasCharacter && HasWeapon;

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
            Debug.LogWarning("[RunSelectionManager] Tried to select null character.");
            return;
        }

        SelectedCharacter = character;
    }

    public void SelectWeapon(WeaponData weapon)
    {
        if (weapon == null)
        {
            Debug.LogWarning("[RunSelectionManager] Tried to select null weapon.");
            return;
        }

        SelectedWeapon = weapon;
    }

    public void ClearRunSelection()
    {
        SelectedCharacter = null;
        SelectedWeapon = null;
    }
}
