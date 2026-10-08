using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the persistent selection to the bunker scene player. The bunker
/// keeps its authored player root so camera, intro and transition references
/// remain valid; only the selected character visual is recreated.
/// </summary>
public sealed class BunkerPlayerLoadoutController : MonoBehaviour
{

    [Header("Controlled Bunker Player")]
    [SerializeField] private Transform controlledPlayerRoot;
    [SerializeField] private Transform controlledPlayerVisualRoot;
    [SerializeField] private Transform controlledPlayerFacingVisualRoot;
    [SerializeField] private ControlOnboarding onboardingPrefab;

    private RunSelectionManager selection;
    private RunSelectionManager selectionOwner;
    private BunkerSelectionSourceHub selectionSource;
    public void BindScene(RunSelectionManager owner, BunkerSelectionSourceHub source)
    {
        selectionOwner = owner;
        selectionSource = source;
    }
    private GameObject player;
    private Transform activeVisual;
    private CharacterData activeCharacter;
    private float fallbackMoveSpeed;
    public bool IsReady => player != null && selection != null && activeCharacter != null;

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

        PlayerRuntimeReference.Bind(player);
        activeVisual = controlledPlayerVisualRoot;
        CharacterMovement2D movement =
            player.GetComponent<CharacterMovement2D>();
        fallbackMoveSpeed = movement.AuthoredMoveSpeed;
        movement.SetVisualRoot(controlledPlayerFacingVisualRoot);
        if (onboardingPrefab != null)
            Instantiate(onboardingPrefab, controlledPlayerRoot.position, Quaternion.identity).Bind(movement);
        BindSelectionManager();
        ApplyCurrentSelection();
    }

    private void OnDestroy()
    {
        if (PlayerRuntimeReference.CachedPlayer == player) PlayerRuntimeReference.Clear();
        UnbindSelectionManager();
    }

    private void BindSelectionManager()
    {
        UnbindSelectionManager();
        selection = selectionOwner;

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
        if (character == null)
        {
            // Use the station's content order and unlock rules before the first
            // interaction, rather than moving with the scene prefab's speed.
            character = selectionSource != null ? selectionSource.GetDefaultCharacter() : null;
            if (character != null)
            {
                // The subscribed handler applies the loadout exactly once.
                selection.SelectCharacter(character);
                return;
            }
        }
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
            return;

        if (character.ProductionPrefab == null)
            return;

        Transform sourceVisual = FindVisual(character.ProductionPrefab);
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
            character.ProductionPrefab,
            sourceVisual,
            replacement);
        movement?.SetVisualRoot(facingVisual);
        PlayerLoadoutFactory.ApplyCharacterStats(
            // Bunker traversal uses the authored hub speed, independently of combat character balance.
            player, character, fallbackMoveSpeed, moveSpeedOverride: fallbackMoveSpeed);

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
