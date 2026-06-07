using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class WeaponClickHandler : MonoBehaviour
{
    [SerializeField] private WeaponData weapon;
    [SerializeField] private WeaponSelectionUI selectionUI;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnWeaponClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnWeaponClick);
    }

    private void OnWeaponClick()
    {
        if (weapon == null)
        {
            Debug.LogWarning($"{name}: WeaponData is not assigned.");
            return;
        }

        if (selectionUI == null)
        {
            Debug.LogWarning($"{name}: WeaponSelectionUI is not assigned.");
            return;
        }

        selectionUI.SelectWeapon(weapon);
    }
}