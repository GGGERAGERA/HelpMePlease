using UnityEngine;

/// <summary>
/// Applies the persistent selection to the bunker scene player. The bunker
/// keeps its authored player root so camera, intro and transition references
/// remain valid; only the selected visual and weapon are recreated.
/// </summary>
public sealed class BunkerPlayerLoadoutController : MonoBehaviour
{
    private const string WeaponPointName = "WeaponPoint";

    private RunSelectionManager selection;
    private GameObject player;
    private Transform activeVisual;
    private BaseWeapon activeWeapon;
    private CharacterData activeCharacter;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError(
                "[BunkerPlayerLoadout] Bunker player was not found.",
                this);
            return;
        }

        activeVisual = FindVisual(player);
        BindSelectionManager();
        ApplyCurrentSelection();
    }

    private void Update()
    {
        if (selection != RunSelectionManager.Instance)
        {
            BindSelectionManager();
            ApplyCurrentSelection();
        }
    }

    private void OnDestroy()
    {
        UnbindSelectionManager();
    }

    private void BindSelectionManager()
    {
        UnbindSelectionManager();
        selection = RunSelectionManager.Instance;

        if (selection == null)
            return;

        selection.CharacterSelected += ApplyCharacter;
        selection.WeaponSelected += ApplyWeapon;
    }

    private void UnbindSelectionManager()
    {
        if (selection == null)
            return;

        selection.CharacterSelected -= ApplyCharacter;
        selection.WeaponSelected -= ApplyWeapon;
        selection = null;
    }

    private void ApplyCurrentSelection()
    {
        if (selection == null || player == null)
            return;

        if (selection.SelectedCharacter != null)
            ApplyCharacter(selection.SelectedCharacter);

        if (selection.SelectedWeapon != null && activeWeapon == null)
            ApplyWeapon(selection.SelectedWeapon);
    }

    private void ApplyCharacter(CharacterData character)
    {
        if (player == null || character == null ||
            character.characterPrefab == null || character == activeCharacter)
        {
            return;
        }

        Transform sourceVisual = FindVisual(character.characterPrefab);
        if (sourceVisual == null)
        {
            Debug.LogError(
                $"[BunkerPlayerLoadout] Character '{character.name}' has no " +
                "Animator visual root.",
                character);
            return;
        }

        Transform parent = activeVisual != null
            ? activeVisual.parent
            : player.transform;
        Transform replacement = Instantiate(sourceVisual.gameObject, parent)
            .transform;
        replacement.localPosition = sourceVisual.localPosition;
        replacement.localRotation = sourceVisual.localRotation;
        replacement.localScale = sourceVisual.localScale;

        if (activeVisual != null)
            Destroy(activeVisual.gameObject);

        activeVisual = replacement;
        activeCharacter = character;

        CharacterMovement2D movement =
            player.GetComponent<CharacterMovement2D>();
        movement?.SetVisualRoot(activeVisual);
        PlayerLoadoutFactory.ApplyCharacterStats(player, character);

        if (selection != null && selection.SelectedWeapon != null)
            ApplyWeapon(selection.SelectedWeapon);
    }

    private void ApplyWeapon(WeaponData weapon)
    {
        if (player == null || weapon == null)
            return;

        if (activeWeapon != null)
        {
            activeWeapon.gameObject.SetActive(false);
            Destroy(activeWeapon.gameObject);
        }

        CharacterCombatType combatType = activeCharacter != null
            ? activeCharacter.combatType
            : CharacterCombatType.AutoFire;
        activeWeapon = PlayerLoadoutFactory.SpawnWeapon(
            player,
            weapon,
            combatType,
            WeaponPointName);

        PlayerWeaponOrbitVisual.Ensure(player, activeWeapon);
    }

    private static Transform FindVisual(GameObject root)
    {
        if (root == null)
            return null;

        Animator animator = root.GetComponentInChildren<Animator>(true);
        return animator != null ? animator.transform : null;
    }
}
