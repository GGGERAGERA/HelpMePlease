using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public static class ProductionCharacterValidator
{
    public static bool Validate(
        IReadOnlyList<CharacterData> characters,
        out string error)
    {
        if (characters == null || characters.Count != 3)
        {
            error = "production character set must contain exactly Gera, DiMag and Vika";
            return false;
        }

        HashSet<CharacterId> ids = new();
        foreach (CharacterData character in characters)
        {
            if (character == null)
            {
                error = "production character entry is missing";
                return false;
            }

            if (!ids.Add(character.Id))
            {
                error = $"duplicate production CharacterId '{character.Id}'";
                return false;
            }

            if (!ValidateCharacter(character, out error))
                return false;
        }

        if (!ids.SetEquals(new[] { CharacterId.Gera, CharacterId.DiMag, CharacterId.Vika }))
        {
            error = "production CharacterIds must be Gera, DiMag and Vika";
            return false;
        }

        error = "OK";
        return true;
    }

    public static bool ValidateCharacter(CharacterData character, out string error)
    {
        if (character == null)
        {
            error = "CharacterData is missing";
            return false;
        }

        if (!character.HasValidProductionIdentity)
        {
            error = $"CharacterData '{character.name}' has incomplete production identity";
            return false;
        }

        if (!HasExpectedOrbitalPath(character))
        {
            error = $"CharacterData '{character.name}' has unexpected ORBITAL path '{character.orbitalPath}'";
            return false;
        }

        CharacterMovement2D movement =
            character.ProductionPrefab.GetComponent<CharacterMovement2D>();
        if (movement == null)
        {
            error = $"CharacterData '{character.name}' production prefab has no CharacterMovement2D";
            return false;
        }

        Transform visualRoot = movement.VisualRoot;
        if (visualRoot == null || !visualRoot.gameObject.activeSelf ||
            !visualRoot.IsChildOf(character.ProductionPrefab.transform))
        {
            error = $"CharacterData '{character.name}' production prefab has invalid facing visual root";
            return false;
        }

        SpriteRenderer renderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null || renderer.sprite == null || !renderer.enabled ||
            !IsActiveInPrefab(renderer.transform, character.ProductionPrefab.transform))
        {
            error = $"CharacterData '{character.name}' facing visual has no active renderer";
            return false;
        }

        error = "OK";
        return true;
    }

    private static bool HasExpectedOrbitalPath(CharacterData character)
    {
        return character.Id switch
        {
            CharacterId.Gera => character.orbitalPath == OrbitalPathType.Circle,
            CharacterId.DiMag => character.orbitalPath == OrbitalPathType.FigureEight,
            CharacterId.Vika => character.orbitalPath == OrbitalPathType.Custom,
            _ => false
        };
    }

    private static bool IsActiveInPrefab(Transform node, Transform root)
    {
        for (Transform current = node; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
                return false;
            if (current == root)
                return true;
        }
        return false;
    }
}
