using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Subject42.Combat.OrbitalStation;
using System.Reflection;
using System.Linq;

public sealed partial class CharacterIdentityConfigurationTests
{

    [Category("Extended")]
    [Test]
    public void CharacterAssetsMatchCanonicalGeraAndVikaVisuals()
    {
        CharacterData gera = AssetDatabase.LoadAssetAtPath<CharacterData>(GeraDataPath);
        CharacterData vika = AssetDatabase.LoadAssetAtPath<CharacterData>(VikaDataPath);

        Assert.That(gera, Is.Not.Null);
        Assert.That(gera.characterName, Is.EqualTo("Gera"));
        Assert.That(AssetDatabase.GetAssetPath(gera.ProductionPrefab),
            Is.EqualTo("Assets/_Project/prefabs/players/Production/Player_2_p_Player1 Variant.prefab"));
        Assert.That(AssetDatabase.GetAssetPath(gera.Portrait),
            Is.EqualTo("Assets/_Project/art/image_2026-04-26_01-41-48.png"));
        AssertActiveFacingVisual(gera.ProductionPrefab, "Gera4");

        Assert.That(vika, Is.Not.Null);
        Assert.That(vika.characterName, Is.EqualTo("Vika"));
        Assert.That(AssetDatabase.GetAssetPath(vika.ProductionPrefab),
            Is.EqualTo("Assets/_Project/prefabs/players/Production/Player_0_p_Player3.prefab"));
        Assert.That(AssetDatabase.GetAssetPath(vika.Portrait),
            Is.EqualTo("Assets/_Project/art/339a1a4a-a7df-475a-9ca7-579919373785.png"));
        AssertActiveFacingVisual(vika.ProductionPrefab, "Vika4");
    }
}
