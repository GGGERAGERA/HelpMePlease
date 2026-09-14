using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterIdentityConfigurationTests
{
    private const string GeraDataPath =
        "Assets/_Project/Scriptable Objects/Characters/01_Gera.asset";
    private const string VikaDataPath =
        "Assets/_Project/Scriptable Objects/Characters/03_Vika.asset";

    [Test]
    public void CharacterAssetsMatchCanonicalGeraAndVikaVisuals()
    {
        CharacterData gera = AssetDatabase.LoadAssetAtPath<CharacterData>(GeraDataPath);
        CharacterData vika = AssetDatabase.LoadAssetAtPath<CharacterData>(VikaDataPath);

        Assert.That(gera, Is.Not.Null);
        Assert.That(gera.characterName, Is.EqualTo("Gera"));
        Assert.That(AssetDatabase.GetAssetPath(gera.characterPrefab),
            Is.EqualTo("Assets/_Project/prefabs/players/p_Player1 Variant.prefab"));
        Assert.That(AssetDatabase.GetAssetPath(gera.portrait),
            Is.EqualTo("Assets/_Project/art/image_2026-04-26_01-41-48.png"));
        AssertActiveFacingVisual(gera.characterPrefab, "Gera4");

        Assert.That(vika, Is.Not.Null);
        Assert.That(vika.characterName, Is.EqualTo("Vika"));
        Assert.That(AssetDatabase.GetAssetPath(vika.characterPrefab),
            Is.EqualTo("Assets/_Project/prefabs/players/p_Player3.prefab"));
        Assert.That(AssetDatabase.GetAssetPath(vika.portrait),
            Is.EqualTo("Assets/_Project/art/01f2275083991207c33e47bc77865466.jpg"));
        AssertActiveFacingVisual(vika.characterPrefab, "Vika4");
    }

    private static void AssertActiveFacingVisual(
        GameObject characterPrefab,
        string expectedName)
    {
        Assert.That(characterPrefab, Is.Not.Null);
        CharacterMovement2D movement =
            characterPrefab.GetComponent<CharacterMovement2D>();
        Assert.That(movement, Is.Not.Null);
        Assert.That(movement.VisualRoot, Is.Not.Null);
        Assert.That(movement.VisualRoot.name, Is.EqualTo(expectedName));
        Assert.That(movement.VisualRoot.gameObject.activeSelf, Is.True);
    }
}
