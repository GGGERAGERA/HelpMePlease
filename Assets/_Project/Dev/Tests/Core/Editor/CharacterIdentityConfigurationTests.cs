using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;

public sealed class CharacterIdentityConfigurationTests
{
    [Category("Core")]
    [TestCase("01_Gera", CharacterId.Gera, OrbitalPathType.Circle)]
    [TestCase("02_Di-mag", CharacterId.DiMag, OrbitalPathType.FigureEight)]
    [TestCase("03_Vika", CharacterId.Vika, OrbitalPathType.Custom)]
    public void ProductionCharacterHasValidIdentityAndOrbitalPath(
        string assetName, CharacterId id, OrbitalPathType path)
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/" + assetName + ".asset");
        Assert.That(character, Is.Not.Null);
        Assert.That(character.HasValidProductionIdentity, Is.True);
        Assert.That(character.Id, Is.EqualTo(id));
        Assert.That(character.orbitalPath, Is.EqualTo(path));
        if (id != CharacterId.DiMag) return;

        var movement = character.ProductionPrefab.GetComponent<CharacterMovement2D>();
        Assert.That(movement, Is.Not.Null);
        Assert.That(movement.VisualRoot, Is.Not.Null);
        Assert.That(movement.VisualRoot.gameObject.activeSelf, Is.True);
        Assert.That(movement.VisualRoot.GetComponentInChildren<SpriteRenderer>(), Is.Not.Null);
        var station = character.ProductionPrefab.GetComponentInChildren<OrbitalStationView>(true);
        Assert.That(station, Is.Not.Null);
        Assert.That(station.transform.IsChildOf(movement.VisualRoot), Is.False,
            "Facing must bind the character visual independently of the station.");
    }
}
