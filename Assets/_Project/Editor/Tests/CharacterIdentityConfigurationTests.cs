using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Subject42.Combat.OrbitalStation;
using System.Reflection;

public sealed class CharacterIdentityConfigurationTests
{
    private const string GeraDataPath =
        "Assets/_Project/Scriptable Objects/Characters/01_Gera.asset";
    private const string VikaDataPath =
        "Assets/_Project/Scriptable Objects/Characters/03_Vika.asset";
    private const string DimagDataPath =
        "Assets/_Project/Scriptable Objects/Characters/02_Di-mag.asset";

    [TestCase(GeraDataPath, OrbitalPathType.Circle, false)]
    [TestCase(DimagDataPath, OrbitalPathType.FigureEight, false)]
    [TestCase(VikaDataPath, OrbitalPathType.Custom, true)]
    public void CharacterOwnsItsProductionPath(string assetPath, OrbitalPathType expected, bool draws)
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
        Assert.That(character.orbitalPath, Is.EqualTo(expected));
        var manager = RunStateManager.EnsureExists();
        var stage = ScriptableObject.CreateInstance<StageProfileData>();
        var rule = ScriptableObject.CreateInstance<WorldRuleData>();
        var anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        try
        {
            manager.BeginNewRun(character, null, stage, rule, anomaly);
            Assert.That(manager.OrbitalStationState.UsesCustomPaths, Is.EqualTo(draws));
        }
        finally
        {
            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(rule);
            Object.DestroyImmediate(anomaly);
        }
    }

    [Test]
    public void ProductionFacingFlipsTheVisibleCharacterWithoutFlippingStation()
    {
        foreach (string path in new[] { GeraDataPath, DimagDataPath, VikaDataPath })
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            var player = Object.Instantiate(OrbitalPresentationConfig.Active.GetPlayerPrefab(character.characterPrefab));
            try
            {
                var movement = player.GetComponent<CharacterMovement2D>();
                Assert.That(movement.VisualRoot, Is.Not.Null, path);
                Assert.That(movement.VisualRoot.gameObject.activeInHierarchy, Is.True, path);
                Assert.That(movement.VisualRoot.GetComponentInChildren<SpriteRenderer>(), Is.Not.Null, path);
                movement.SetVisualRoot(movement.VisualRoot);
                Vector3 original = movement.VisualRoot.localScale;
                var station = player.GetComponentInChildren<OrbitalStationView>().transform;
                Vector3 stationScale = station.lossyScale;
                var facing = typeof(CharacterMovement2D).GetMethod("UpdateFacing", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (float direction in new[] { 1f, -1f, 1f })
                {
                    facing.Invoke(movement, new object[] { direction });
                    Assert.That(movement.VisualRoot.localScale.x, Is.EqualTo(-direction * Mathf.Abs(original.x)), path);
                    Assert.That(movement.VisualRoot.localScale.y, Is.EqualTo(original.y), path);
                    Assert.That(station.lossyScale, Is.EqualTo(stationScale), path);
                }
                Assert.That(player.GetComponentInChildren<Animator>().runtimeAnimatorController,
                    Is.SameAs(character.characterPrefab.GetComponentInChildren<Animator>().runtimeAnimatorController), path);
            }
            finally { Object.DestroyImmediate(player); }
        }
    }

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
            Is.EqualTo("Assets/_Project/art/339a1a4a-a7df-475a-9ca7-579919373785.png"));
        AssertActiveFacingVisual(vika.characterPrefab, "Vika4");
    }

    [Test]
    public void CharacterPresentationHasBothLanguagesAndMatchesProductionPatterns()
    {
        var table = Resources.Load<LocalizationTable>("Localization/LocalizationTable");
        Assert.That(table, Is.Not.Null);
        string[] paths = { GeraDataPath,
            "Assets/_Project/Scriptable Objects/Characters/02_Di-mag.asset", VikaDataPath };
        foreach (string path in paths)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            Assert.That(character, Is.Not.Null, path);
            foreach (string key in new[] { character.nameKey, character.orbitalPatternKey,
                         character.traitKey, character.descriptionKey })
            {
                foreach (GameLanguage language in new[] { GameLanguage.Russian, GameLanguage.English })
                {
                    Assert.That(table.TryGet(key, language, out string text), Is.True, key);
                    Assert.That(text, Is.Not.Empty, key);
                }
            }
        }
        var vika = AssetDatabase.LoadAssetAtPath<CharacterData>(VikaDataPath);
        Assert.That(table.TryGet(vika.orbitalPatternKey, GameLanguage.Russian, out string pattern), Is.True);
        Assert.That(pattern, Is.EqualTo("ОРБИТАЛЬНЫЙ ПАТТЕРН\nЭКСПЕРИМЕНТАЛЬНЫЙ"));
        Assert.That(table.TryGet(vika.traitKey, GameLanguage.Russian, out string trait), Is.True);
        Assert.That(trait, Is.EqualTo("Пользователь сам формирует траекторию каждого нового кольца."));
        Assert.That(table.TryGet(vika.descriptionKey, GameLanguage.Russian, out string description), Is.True);
        Assert.That(description, Is.EqualTo("ДАННЫЕ ЭКСПЕРИМЕНТА НЕПОЛНЫЕ."));
        Assert.That(table.TryGet(vika.descriptionKey, GameLanguage.English, out description), Is.True);
        Assert.That(description, Is.EqualTo("EXPERIMENT DATA INCOMPLETE."));
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
