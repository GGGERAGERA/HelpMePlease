using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Subject42.Combat.OrbitalStation;
using System.Reflection;
using System.Linq;

public sealed partial class CharacterIdentityConfigurationTests
{
    private const string GeraDataPath =
        "Assets/_Project/Scriptable Objects/Characters/01_Gera.asset";
    private const string VikaDataPath =
        "Assets/_Project/Scriptable Objects/Characters/03_Vika.asset";
    private const string DimagDataPath =
        "Assets/_Project/Scriptable Objects/Characters/02_Di-mag.asset";

    [Category("Core")]
    [TestCase(GeraDataPath, CharacterId.Gera, OrbitalPathType.Circle, false)]
    [TestCase(DimagDataPath, CharacterId.DiMag, OrbitalPathType.FigureEight, false)]
    [TestCase(VikaDataPath, CharacterId.Vika, OrbitalPathType.Custom, true)]
    public void CharacterOwnsItsProductionPath(string assetPath, CharacterId id, OrbitalPathType expected, bool draws)
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
        Assert.That(character.Id, Is.EqualTo(id));
        Assert.That(character.ProductionPrefab, Is.Not.Null);
        Assert.That(character.Portrait, Is.Not.Null);
        Assert.That(character.GameplayIcon, Is.Not.Null);
        Assert.That(character.orbitalPath, Is.EqualTo(expected));
        var existingLocalization = LocalizationService.Instance;
        var localization = LocalizationService.EnsureExists();
        try
        {
            // Initialize only the fixture dependencies: runtime Awake also calls
            // DontDestroyOnLoad, which is invalid in EditMode.
            if (LocalizationService.Instance == null)
            {
                var table = Resources.Load<LocalizationTable>("Localization/LocalizationTable");
                Assert.That(table, Is.Not.Null);
                typeof(LocalizationService).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(localization, table);
                var language = typeof(LocalizationService).GetMethod("LoadLanguage", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
                typeof(LocalizationService).GetProperty(nameof(LocalizationService.CurrentLanguage)).GetSetMethod(true)
                    .Invoke(localization, new[] { language });
                typeof(LocalizationService).GetProperty(nameof(LocalizationService.Instance)).GetSetMethod(true)
                    .Invoke(null, new object[] { localization });
            }
            Assert.That(LocalizationService.Instance, Is.SameAs(localization));
            CharacterSelectionUsesGameplayIconOnLeftAndPortraitOnRight(assetPath);
            MissingGameplayIconDoesNotFallBackToPortrait(assetPath);
        }
        finally
        {
            if (existingLocalization == null && localization != null)
            {
                typeof(LocalizationService).GetProperty(nameof(LocalizationService.Instance)).GetSetMethod(true)
                    .Invoke(null, new object[] { existingLocalization });
                Object.DestroyImmediate(localization.gameObject);
            }
        }
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

    [Category("Core")]
    [Test]
    public void ProductionCharacterIdsAreUniqueAndExpected()
    {
        CharacterData[] characters =
        {
            AssetDatabase.LoadAssetAtPath<CharacterData>(GeraDataPath),
            AssetDatabase.LoadAssetAtPath<CharacterData>(DimagDataPath),
            AssetDatabase.LoadAssetAtPath<CharacterData>(VikaDataPath)
        };

        Assert.That(characters.Select(character => character.Id),
            Is.EquivalentTo(new[] { CharacterId.Gera, CharacterId.DiMag, CharacterId.Vika }));
        Assert.That(characters.Select(character => character.Id).Distinct().Count(),
            Is.EqualTo(characters.Length));
        Assert.That(ProductionCharacterValidator.Validate(characters, out string error),
            Is.True,
            error);
    }

    private void CharacterSelectionUsesGameplayIconOnLeftAndPortraitOnRight(string assetPath)
    {
        CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
        var entry = BunkerSelectionSourceHub.BuildCharacterEntryForTests(character, selected: false);

        Assert.That(entry.Id, Is.EqualTo(character.Id.ToString()));
        Assert.That(entry.Icon, Is.SameAs(character.Portrait));
        Assert.That(entry.CharacterVisual, Is.SameAs(character.GameplayIcon));
        Assert.That(character.GameplayIcon, Is.Not.Null);
        Assert.That(character.Portrait, Is.Not.SameAs(character.GameplayIcon));

        var root = PrefabUtility.LoadPrefabContents(
            "Assets/_Project/prefabs/UI/Stations/BunkerSelectionWindow.prefab");
        try
        {
            var window = new SerializedObject(root.GetComponent<BunkerSelectionWindow>());
            var card = (BunkerSelectionCardView)window.FindProperty("cardPrefab").objectReferenceValue;
            var detail = (BunkerSelectionDetailView)window.FindProperty("detailView").objectReferenceValue;
            var icon = (UnityEngine.UI.Image)new SerializedObject(card).FindProperty("icon").objectReferenceValue;
            var portrait = (UnityEngine.UI.Image)new SerializedObject(detail).FindProperty("portrait").objectReferenceValue;

            card.Bind(entry);
            detail.Bind(entry);
            Assert.That(icon.sprite, Is.SameAs(character.GameplayIcon));
            Assert.That(portrait.sprite, Is.SameAs(character.Portrait));

            // A missing gameplay icon must not silently turn the left card into a portrait.
            entry.CharacterVisual = null;
            card.Bind(entry);
            Assert.That(icon.sprite, Is.Null);
            Assert.That(icon.enabled, Is.False);
            Assert.That(portrait.sprite, Is.SameAs(character.Portrait));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private void MissingGameplayIconDoesNotFallBackToPortrait(string assetPath)
    {
        var character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath));
        try
        {
            var data = new SerializedObject(character);
            data.FindProperty("gameplayIcon").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(character.Portrait, Is.Not.Null);
            Assert.That(character.GameplayIcon, Is.Null);
            Assert.That(character.HasValidProductionIdentity, Is.False);
            Assert.That(BunkerSelectionSourceHub.BuildCharacterEntryForTests(character, false).CharacterVisual,
                Is.Null);
        }
        finally { Object.DestroyImmediate(character); }
    }

    [Category("Core")]
    [Test]
    public void ProductionFacingFlipsTheVisibleCharacterWithoutFlippingStation()
    {
        foreach (string path in new[] { GeraDataPath, DimagDataPath, VikaDataPath })
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            var player = Object.Instantiate(character.ProductionPrefab);
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

    [Category("Core")]
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
