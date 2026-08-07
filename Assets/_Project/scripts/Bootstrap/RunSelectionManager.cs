using UnityEngine;

public sealed class RunSelectionManager : MonoBehaviour
{
    public static RunSelectionManager Instance { get; private set; }

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
        SelectedCharacter = character;
        Debug.Log($"[RunSelectionManager] Character selected: {character?.name}");
    }

    public void SelectWeapon(WeaponData weapon)
    {
        SelectedWeapon = weapon;
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
