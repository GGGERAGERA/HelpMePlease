using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
public sealed class VisualDirectionLabController : MonoBehaviour
{
    private sealed class PrimitiveVisual
    {
        public SpriteRenderer Renderer;
        public int Role;
    }

    private readonly struct Direction
    {
        public readonly string Name;
        public readonly Color Scene, Ground, Grass, Plants;
        public readonly float SceneTint, SceneSat, SceneBright;
        public readonly float GroundTint, GroundSat, GroundBright;
        public readonly float GrassTint, GrassSat, GrassBright;
        public readonly float PlantsTint, PlantsSat, PlantsBright;
        public readonly float Decor, Darken, EnemyBright, EnemySat;
        public readonly Color EnemyTint, PlayerTint, InteractableTint;
        public readonly float EnemyTintStrength, PlayerBright;
        public readonly float ProjectileScale, TrailBright, Vignette;
        public readonly float VegetationDensity;

        public Direction(
            string name, Color scene, float sceneTint, float sceneSat,
            float sceneBright, Color ground, float groundTint,
            float groundSat, float groundBright, Color grass,
            float grassTint, float grassSat, float grassBright,
            Color plants, float plantsTint, float plantsSat,
            float plantsBright, float decor, float darken,
            float enemyBright, float enemySat, Color enemyTint,
            float enemyTintStrength, Color playerTint, float playerBright,
            float projectileScale, float trailBright, float vignette,
            Color interactableTint, float vegetationDensity)
        {
            Name = name; Scene = scene; SceneTint = sceneTint;
            SceneSat = sceneSat; SceneBright = sceneBright;
            Ground = ground; GroundTint = groundTint;
            GroundSat = groundSat; GroundBright = groundBright;
            Grass = grass; GrassTint = grassTint; GrassSat = grassSat;
            GrassBright = grassBright; Plants = plants;
            PlantsTint = plantsTint; PlantsSat = plantsSat;
            PlantsBright = plantsBright; Decor = decor; Darken = darken;
            EnemyBright = enemyBright; EnemySat = enemySat;
            EnemyTint = enemyTint; EnemyTintStrength = enemyTintStrength;
            PlayerTint = playerTint; PlayerBright = playerBright;
            ProjectileScale = projectileScale; TrailBright = trailBright;
            Vignette = vignette; InteractableTint = interactableTint;
            VegetationDensity = vegetationDensity;
        }
    }

    private static readonly Direction[] Directions =
    {
        new("DARK NEON", C("07151D"), .72f, .72f, .48f,
            C("17333A"), .68f, .48f, .58f, C("12505A"), .72f, .62f,
            .55f, C("0C6570"), .68f, .72f, .6f, .42f, .48f,
            2.05f, 2.25f, C("24F4ED"), .38f, C("5CFFF7"), 2.15f,
            1.45f, 2.8f, .55f, C("26FFF2"), .38f),
        new("SATURATED APOCALYPSE", C("FFB14A"), .2f, 1.62f, 1.18f,
            C("8F7B32"), .28f, 1.42f, 1.16f, C("45D64C"), .25f,
            1.85f, 1.22f, C("18B96E"), .32f, 2.05f, 1.16f, 1.05f,
            .08f, 2.25f, 2.45f, C("FF486B"), .32f, C("FFF06A"),
            2.3f, 1.55f, 3.2f, .18f, C("4CFFF4"), .86f),
        new("COLD BIOME", C("8CCDE0"), .3f, .68f, .9f,
            C("7D9AA4"), .38f, .42f, .94f, C("4E99A5"), .42f,
            .72f, .88f, C("39A6A0"), .36f, 1.08f, .94f, .76f, .14f,
            1.82f, 1.8f, C("FF765E"), .22f, C("9EFFF5"), 1.9f,
            1.32f, 2.35f, .32f, C("45F6FF"), .58f),
        new("HIGH-CONTRAST PIXEL", C("DCEEFF"), .08f, .82f, .92f,
            C("53636B"), .44f, .38f, .78f, C("243F42"), .58f,
            .48f, .58f, C("A4C55B"), .48f, .82f, 1.08f, .52f, .22f,
            2.45f, 2.6f, C("FF3C62"), .48f, C("E9FFFF"), 2.6f,
            1.75f, 3.8f, .38f, C("00FFF0"), .28f)
    };

    private readonly Dictionary<SpriteRenderer, bool> vegetation = new();
    private readonly Dictionary<SpriteRenderer, Color> interactables = new();
    private readonly List<PrimitiveVisual> backdropVisuals = new();
    private GameObject backdropRoot;
    private Texture2D primitiveTexture;
    private Sprite primitiveSprite;
    private int currentIndex;
    private bool ready;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(LoadProductionReference());
    }

    private IEnumerator LoadProductionReference()
    {
        Scene existing = SceneManager.GetSceneByName("MVP");
        if (!existing.isLoaded)
            yield return SceneManager.LoadSceneAsync("MVP", LoadSceneMode.Additive);

        WorldEventSpawner eventSpawner = FindFirstObjectByType<WorldEventSpawner>();
        if (eventSpawner != null)
            eventSpawner.enabled = false;

        yield return null;
        yield return null;

        ProductionVisualTuningController tuning =
            FindFirstObjectByType<ProductionVisualTuningController>();
        if (tuning == null)
            tuning = gameObject.AddComponent<ProductionVisualTuningController>();
        tuning.Configure();

        ProductionSectorDebugController sector =
            FindFirstObjectByType<ProductionSectorDebugController>();
        if (sector == null)
            sector = gameObject.AddComponent<ProductionSectorDebugController>();
        sector.SetInvulnerability(true);

        BuildPrimitiveBackdrop();
        ready = true;
        ApplyDirection(0);
    }

    private void Update()
    {
        if (!ready)
            return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyDirection(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyDirection(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyDirection(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyDirection(3);
    }

    private void ApplyDirection(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, Directions.Length - 1);
        Direction d = Directions[currentIndex];
        ProductionVisualTuningController tuning =
            FindFirstObjectByType<ProductionVisualTuningController>();
        ProductionSectorDebugController sector =
            FindFirstObjectByType<ProductionSectorDebugController>();
        if (tuning == null || sector == null)
            return;

        tuning.SetSceneTint(d.Scene); tuning.SetSceneTintAmount(d.SceneTint);
        tuning.SetSceneSaturation(d.SceneSat); tuning.SetSceneBrightness(d.SceneBright);
        tuning.SetBackgroundTint(d.Ground); tuning.SetBackgroundTintAmount(d.GroundTint);
        tuning.SetBackgroundSaturation(d.GroundSat); tuning.SetBackgroundBrightness(d.GroundBright);
        tuning.SetGrassTint(d.Grass); tuning.SetGrassTintAmount(d.GrassTint);
        tuning.SetGrassSaturation(d.GrassSat); tuning.SetGrassBrightness(d.GrassBright);
        tuning.SetPlantsTint(d.Plants); tuning.SetPlantsTintAmount(d.PlantsTint);
        tuning.SetPlantsSaturation(d.PlantsSat); tuning.SetPlantsBrightness(d.PlantsBright);
        tuning.SetPlayerTint(d.PlayerTint); tuning.SetPlayerTintStrength(.28f);
        tuning.SetPlayerBrightness(d.PlayerBright); tuning.SetPlayerSaturation(1.65f);
        tuning.SetWeaponBrightness(d.PlayerBright); tuning.SetWeaponSaturation(1.8f);
        tuning.SetProjectileVisualScale(d.ProjectileScale);
        tuning.SetTrailBrightness(d.TrailBright); tuning.SetTrailAlpha(1.35f);
        tuning.SetLaserBrightness(d.TrailBright); tuning.SetVignetteIntensity(d.Vignette);

        sector.SetPreset(currentIndex == 0
            ? ProductionSectorDebugController.ReadabilityPreset.DarkWorld
            : ProductionSectorDebugController.ReadabilityPreset.HighGameplayContrast);
        sector.SetDecorBrightness(d.Decor); sector.SetEnvironmentDarken(d.Darken);
        sector.SetAnomalyAccent(1.65f);
        sector.SetEnemyScope(ProductionSectorDebugController.EnemyScope.All);
        sector.SetEnemyReadability(ProductionSectorDebugController.EnemyReadability.High);
        sector.SetEnemyBrightness(d.EnemyBright); sector.SetEnemySaturation(d.EnemySat);
        sector.SetEnemyTintStrength(d.EnemyTintStrength);
        sector.SetEnemyRecolorTarget(d.EnemyTint);
        sector.SetEnemyRecolorStrength(d.EnemyTintStrength * .55f);
        sector.SetEnemyOutlineEnabled(currentIndex == 3);
        sector.SetEnemyOutlineStrength(currentIndex == 3 ? 1.25f : .25f);
        sector.SetEnemyOutlineWidth(currentIndex == 3 ? 1.5f : 1f);

        ApplyVegetationDensity(d.VegetationDensity);
        ApplyInteractableLook(d.InteractableTint, currentIndex == 3 ? 1.7f : 1.35f);
        ApplyBackdropPalette(d);
        Debug.Log($"[MVP_VisualLab] {currentIndex + 1} — {d.Name}", this);
    }

    private void BuildPrimitiveBackdrop()
    {
        if (backdropRoot != null)
            return;

        backdropRoot = new GameObject("Visual Lab Primitive Environment");
        backdropRoot.transform.SetParent(transform, false);

        primitiveTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "VisualLabPrimitivePixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        primitiveTexture.SetPixel(0, 0, Color.white);
        primitiveTexture.Apply(false, true);
        primitiveSprite = Sprite.Create(
            primitiveTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(.5f, .5f),
            1f
        );
        primitiveSprite.name = "VisualLabPrimitiveSprite";
        primitiveSprite.hideFlags = HideFlags.DontSave;

        Vector2 center = Vector2.zero;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            center = player.transform.position;

        List<Vector2> clearCenters = CollectBackdropClearCenters(center);
        CreatePrimitive("Ground", center, new Vector2(120f, 120f),
            0f, 0, -90);

        System.Random random = new(42042);

        // Broad overlapping shapes read as fields/biome zones rather than noise.
        for (int cluster = 0; cluster < 28; cluster++)
        {
            Vector2 position = center + RandomPoint(random, 48f);
            if (IsNearClearCenter(position, clearCenters, 6f))
                continue;

            int pieces = 3 + random.Next(0, 3);
            for (int piece = 0; piece < pieces; piece++)
            {
                Vector2 offset = RandomPoint(random, 3.2f);
                Vector2 size = new(
                    Range(random, 4.5f, 10.5f),
                    Range(random, 3.2f, 7.5f)
                );
                float angle = Range(random, -18f, 18f);
                CreatePrimitive(
                    $"Field {cluster:00}.{piece}",
                    position + offset,
                    size,
                    angle,
                    cluster % 3 == 0 ? 2 : 1,
                    -88 + piece % 2
                );
            }
        }

        // A broken, gently curving path gives the eye a large-scale landmark.
        for (int segment = -11; segment <= 11; segment++)
        {
            float x = segment * 4.8f;
            float y = Mathf.Sin(segment * .58f) * 5.5f - 5f;
            Vector2 position = center + new Vector2(x, y);
            CreatePrimitive(
                $"Path {segment + 11:00}",
                position,
                new Vector2(5.5f, 1.55f),
                Mathf.Cos(segment * .58f) * 14f,
                3,
                -85
            );
        }

        // Sparse three-blade tufts: recognizable grass made only from rectangles.
        for (int tuft = 0; tuft < 95; tuft++)
        {
            Vector2 position = center + RandomPoint(random, 50f);
            if (IsNearClearCenter(position, clearCenters, 3.6f))
                continue;

            float size = Range(random, .65f, 1.15f);
            CreatePrimitive($"Grass {tuft:000} L", position +
                new Vector2(-.16f, .02f), new Vector2(.12f, size),
                24f, 4, -82);
            CreatePrimitive($"Grass {tuft:000} M", position +
                new Vector2(0f, .08f), new Vector2(.13f, size * 1.15f),
                0f, 4, -82);
            CreatePrimitive($"Grass {tuft:000} R", position +
                new Vector2(.16f, .02f), new Vector2(.12f, size),
                -24f, 4, -82);
        }

        // Tiny flower heads and paired stones establish scale without filling space.
        for (int accent = 0; accent < 34; accent++)
        {
            Vector2 position = center + RandomPoint(random, 49f);
            if (IsNearClearCenter(position, clearCenters, 3.2f))
                continue;

            if (accent % 3 == 0)
            {
                CreatePrimitive($"Flower {accent:00} Stem", position,
                    new Vector2(.09f, .42f), 0f, 4, -81);
                CreatePrimitive($"Flower {accent:00} Head", position +
                    Vector2.up * .27f, new Vector2(.25f, .25f), 45f,
                    5, -80);
            }
            else
            {
                CreatePrimitive($"Stone {accent:00} A", position,
                    new Vector2(.48f, .32f), Range(random, -25f, 25f),
                    6, -81);
                CreatePrimitive($"Stone {accent:00} B", position +
                    new Vector2(.34f, -.08f), new Vector2(.3f, .2f),
                    Range(random, -25f, 25f), 6, -81);
            }
        }
    }

    private List<Vector2> CollectBackdropClearCenters(Vector2 playerPosition)
    {
        List<Vector2> result = new() { playerPosition };
        foreach (WorldBreakable item in WorldBreakable.ActiveInstances)
            if (item != null) result.Add(item.transform.position);
        ProductionAnomalySite[] sites = FindObjectsByType<ProductionAnomalySite>(
            FindObjectsSortMode.None);
        for (int i = 0; i < sites.Length; i++)
            result.Add(sites[i].transform.position);
        return result;
    }

    private void CreatePrimitive(
        string objectName,
        Vector2 position,
        Vector2 scale,
        float rotation,
        int role,
        int sortingOrder)
    {
        GameObject visual = new(objectName);
        visual.transform.SetParent(backdropRoot.transform, false);
        visual.transform.position = new Vector3(position.x, position.y, 0f);
        visual.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = primitiveSprite;
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        backdropVisuals.Add(new PrimitiveVisual { Renderer = renderer, Role = role });
    }

    private void ApplyBackdropPalette(Direction direction)
    {
        Color baseGround = currentIndex switch
        {
            0 => C("0C2930"),
            1 => C("527B34"),
            2 => C("526F79"),
            _ => C("4A585D")
        };
        Color fieldA = currentIndex switch
        {
            0 => C("0D4650"),
            1 => C("399746"),
            2 => C("407B80"),
            _ => C("304A42")
        };
        Color fieldB = currentIndex switch
        {
            0 => C("12383D"),
            1 => C("77AD39"),
            2 => C("64888B"),
            _ => C("71824B")
        };
        Color path = currentIndex switch
        {
            0 => C("244149"),
            1 => C("B88C48"),
            2 => C("82999F"),
            _ => C("778187")
        };
        Color grass = currentIndex switch
        {
            0 => C("1A7780"),
            1 => C("85DB38"),
            2 => C("55A69F"),
            _ => C("A5C94F")
        };
        Color accent = currentIndex == 1 ? C("FFE04A") :
            Color.Lerp(direction.InteractableTint, Color.white, .18f);
        Color stone = currentIndex switch
        {
            0 => C("31565E"),
            1 => C("80906B"),
            2 => C("9AADB1"),
            _ => C("293238")
        };

        for (int i = 0; i < backdropVisuals.Count; i++)
        {
            PrimitiveVisual visual = backdropVisuals[i];
            if (visual.Renderer == null)
                continue;
            visual.Renderer.color = visual.Role switch
            {
                0 => baseGround,
                1 => fieldA,
                2 => fieldB,
                3 => path,
                4 => grass,
                5 => accent,
                _ => stone
            };
        }
    }

    private static Vector2 RandomPoint(System.Random random, float radius)
    {
        return new Vector2(
            Range(random, -radius, radius),
            Range(random, -radius, radius)
        );
    }

    private static float Range(System.Random random, float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private static bool IsNearClearCenter(
        Vector2 position,
        List<Vector2> centers,
        float radius)
    {
        float squaredRadius = radius * radius;
        for (int i = 0; i < centers.Count; i++)
            if ((position - centers[i]).sqrMagnitude < squaredRadius)
                return true;
        return false;
    }

    private void ApplyVegetationDensity(float density)
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Transform> important = new();
        foreach (WorldBreakable item in WorldBreakable.ActiveInstances)
            if (item != null) important.Add(item.transform);
        ProductionAnomalySite[] sites = FindObjectsByType<ProductionAnomalySite>(
            FindObjectsSortMode.None);
        for (int i = 0; i < sites.Length; i++) important.Add(sites[i].transform);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (!HasAncestor(renderer.transform, "Plants")) continue;
            if (!vegetation.ContainsKey(renderer)) vegetation.Add(renderer, renderer.enabled);
            bool clear = false;
            for (int j = 0; j < important.Count; j++)
                if (important[j] != null && Vector2.Distance(renderer.transform.position,
                    important[j].position) < 2.75f) { clear = true; break; }
            uint hash = unchecked((uint)renderer.gameObject.name.GetHashCode() *
                2654435761u + (uint)i * 2246822519u);
            float sample = (hash & 0xFFFFu) / 65535f;
            renderer.enabled = vegetation[renderer] && !clear && sample <= density;
        }
    }

    private void ApplyInteractableLook(Color tint, float brightness)
    {
        WorldBreakable[] breakables = FindObjectsByType<WorldBreakable>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < breakables.Length; i++)
        {
            SpriteRenderer[] renderers = breakables[i].GetComponentsInChildren<SpriteRenderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                SpriteRenderer renderer = renderers[j];
                if (!interactables.TryGetValue(renderer, out Color source))
                { source = renderer.color; interactables.Add(renderer, source); }
                renderer.color = Color.Lerp(source, tint, .38f) * brightness;
            }
        }
    }

    private static bool HasAncestor(Transform target, string name)
    {
        for (Transform current = target; current != null; current = current.parent)
            if (current.name == name) return true;
        return false;
    }

    private void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Box(new Rect(18f, 18f, 370f, 64f), string.Empty);
        GUI.Label(new Rect(32f, 26f, 340f, 24f), ready
            ? $"VISUAL DIRECTION {currentIndex + 1}/4 — {Directions[currentIndex].Name}"
            : "MVP VISUAL LAB — loading production reference...");
        GUI.Label(new Rect(32f, 50f, 340f, 22f), "1 Dark Neon   2 Saturated   3 Cold Biome   4 High Contrast");
    }

    private static Color C(string html)
    {
        ColorUtility.TryParseHtmlString("#" + html, out Color color);
        return color;
    }

    private void OnDestroy()
    {
        if (primitiveSprite != null)
            Destroy(primitiveSprite);
        if (primitiveTexture != null)
            Destroy(primitiveTexture);
    }
}
#endif
