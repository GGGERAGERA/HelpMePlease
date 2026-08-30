using UnityEngine;

public sealed class RunSelectionManager : MonoBehaviour
{
    public static RunSelectionManager Instance { get; private set; }

    public event System.Action<CharacterData> CharacterSelected;
    public event System.Action<WeaponData> WeaponSelected;

    public CharacterData SelectedCharacter { get; private set; }
    public WeaponData SelectedWeapon { get; private set; }
    public AnomalyStabilizerData SelectedAnomalyStabilizer { get; private set; }

    public bool IsReady => SelectedCharacter != null && SelectedWeapon != null;

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
            return;

        SelectedCharacter = character;
        CharacterSelected?.Invoke(character);
        Debug.Log($"[RunSelectionManager] Character selected: {character?.name}");
    }

    public void SelectWeapon(WeaponData weapon)
    {
        if (weapon == null)
            return;

        SelectedWeapon = weapon;
        WeaponSelected?.Invoke(weapon);
        Debug.Log($"[RunSelectionManager] Weapon selected: {weapon?.name}");
    }

    public void SelectAnomalyStabilizer(AnomalyStabilizerData stabilizer)
    {
        SelectedAnomalyStabilizer = stabilizer;
        Debug.Log($"[RunSelectionManager] Anomaly stabilizer selected: {stabilizer?.name}");
    }

    public AnomalyStabilizerData ConsumeAnomalyStabilizer()
    {
        AnomalyStabilizerData selected = SelectedAnomalyStabilizer;
        SelectedAnomalyStabilizer = null;
        return selected;
    }

    public void ClearRunSelection()
    {
        SelectedCharacter = null;
        SelectedWeapon = null;
        SelectedAnomalyStabilizer = null;
    }
}
