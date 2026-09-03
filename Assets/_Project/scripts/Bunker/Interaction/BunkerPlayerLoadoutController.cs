using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the persistent selection to the bunker scene player. The bunker
/// keeps its authored player root so camera, intro and transition references
/// remain valid; only the selected visual and weapon are recreated.
/// </summary>
public sealed class BunkerPlayerLoadoutController : MonoBehaviour
{
    private const string WeaponPointName = "WeaponPoint";

    [Header("Controlled Bunker Player")]
    [SerializeField] private Transform controlledPlayerRoot;
    [SerializeField] private Transform controlledPlayerVisualRoot;
    [SerializeField] private Transform controlledPlayerFacingVisualRoot;

    private RunSelectionManager selection;
    private GameObject player;
    private Transform activeVisual;
    private BaseWeapon activeWeapon;
    private CharacterData activeCharacter;
    private float fallbackMoveSpeed;

    private void Start()
    {
        if (controlledPlayerRoot == null ||
            controlledPlayerVisualRoot == null ||
            controlledPlayerFacingVisualRoot == null)
        {
            Debug.LogError(
                "[BunkerPlayerLoadout] Controlled player references are missing.",
                this);
            return;
        }

        player = controlledPlayerRoot.gameObject;
        if (player.GetComponent<CharacterMovement2D>() == null ||
            player.GetComponent<Rigidbody2D>() == null ||
            player.GetComponent<Collider2D>() == null)
        {
            Debug.LogError(
                "[BunkerPlayerLoadout] Assigned root is not the controlled " +
                "bunker player.",
                controlledPlayerRoot);
            player = null;
            return;
        }

        activeVisual = controlledPlayerVisualRoot;
        CharacterMovement2D movement =
            player.GetComponent<CharacterMovement2D>();
        fallbackMoveSpeed = movement.speed;
        movement.SetVisualRoot(controlledPlayerFacingVisualRoot);
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
    }

    private void UnbindSelectionManager()
    {
        if (selection == null)
            return;

        selection.CharacterSelected -= ApplyCharacter;
        selection = null;
    }

    private void ApplyCurrentSelection()
    {
        if (selection == null || player == null)
            return;

        CharacterData character = selection.SelectedCharacter;
        if (character != null)
            ApplyCharacter(character);
        else
            PlayerLoadoutFactory.ApplyCharacterStats(
                player, null, fallbackMoveSpeed);
    }

    private void ApplyCharacter(CharacterData character)
    {
        if (player == null || character == null)
        {
            return;
        }

        if (character == activeCharacter)
        {
            PlayerLoadoutFactory.ApplyCharacterStats(
                player, character, fallbackMoveSpeed);
            return;
        }

        if (character.characterPrefab == null)
            return;

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
        Transform facingVisual = ResolveFacingVisual(
            character.characterPrefab,
            sourceVisual,
            replacement);
        movement?.SetVisualRoot(facingVisual);
        PlayerLoadoutFactory.ApplyCharacterStats(
            player, character, fallbackMoveSpeed);

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

    private static Transform ResolveFacingVisual(
        GameObject characterPrefab,
        Transform sourceVisual,
        Transform replacementVisual)
    {
        CharacterMovement2D sourceMovement =
            characterPrefab.GetComponent<CharacterMovement2D>();
        Transform sourceFacing = sourceMovement != null
            ? sourceMovement.VisualRoot
            : null;

        if (sourceFacing == null || !sourceFacing.IsChildOf(sourceVisual))
        {
            Debug.LogError(
                $"[BunkerPlayerLoadout] Character '{characterPrefab.name}' " +
                "has no facing visual below its Animator root.",
                characterPrefab);
            return replacementVisual;
        }

        var childIndices = new List<int>();
        Transform current = sourceFacing;
        while (current != sourceVisual)
        {
            childIndices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        Transform resolved = replacementVisual;
        for (int i = childIndices.Count - 1; i >= 0; i--)
        {
            int childIndex = childIndices[i];
            if (childIndex < 0 || childIndex >= resolved.childCount)
                return replacementVisual;
            resolved = resolved.GetChild(childIndex);
        }

        return resolved;
    }
}
