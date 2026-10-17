using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.UI;

// Retains the existing HUD binding and prefab identity; now presents pointer steering.
public sealed class BulletTimeHudView : MaskableGraphic
{
    [SerializeField] private HUDManager hud;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private Color readyColor = new(.18f, .8f, .88f, 1f);
    [SerializeField] private Color activeColor = new(.45f, 1f, 1f, 1f);
    private OrbitalStationRuntime station;
    private OrbitalCenterShift centerShift;
    private bool pointerHeld;
    private bool slowTimeActive;

    private static readonly string[] Orbit =
    {
        "....#####....", "..##.....##..", ".#.........#.", ".#.........#.",
        "#...........#", "#.....#.....#", "#....###....#", "#.....#.....#",
        "#...........#", ".#.........#.", ".#.........#.", "..##.....##..", "....#####...."
    };
    private static readonly string[] LeftArrow = { "..#....", ".#.....", "#######", ".#.....", "..#...." };
    private static readonly string[] RightArrow = { "....#..", ".....#.", "#######", ".....#.", "....#.." };
    private static readonly string[] Mouse =
    {
        "...OOOOOOOOO...", "..OddddORRRRO..", ".OdddddORRRRRO.",
        "OddddddORRRRRRO", "OddddddWRRRRRRO", "OddddddWRRRRRRO",
        "OddddddWRRRRRRO", "OddddddORRRRRRO", "OOOOOOOOOOOOOOO",
        "OdddddddddddddO", "OdddddddddddddO", "OdddddddddddddO",
        "OdddddddddddddO", "OdddddddddddddO", ".OdddddddddddO.",
        "..OdddddddddO..", "...OOOOOOOOO..."
    };
    private static readonly string[] Clock =
    {
        "....#####....", "..##.....##..", ".#.........#.", ".#....#....#.",
        "#.....#.....#", "#.....#.....#", "#.....####..#", "#...........#",
        "#...........#", ".#.........#.", ".#.........#.", "..##.....##..", "....#####...."
    };
    private static readonly string[] ShiftKey =
    {
        ".OOOOOOOOOOOOOOO.", "OdddddddddddddddO", "Odddddd###ddddddO",
        "Oddddd#ddd#dddddO", "Odddd#ddddd#ddddO", "Oddd####d####dddO",
        "Odddddd#d#ddddddO", "Odddddd#d#ddddddO", "Odddddd#d#ddddddO",
        "Odddddd###ddddddO", "OdddddddddddddddO", "OdddddddddddddddO",
        ".OOOOOOOOOOOOOOO."
    };

    public void Bind(GameObject player)
    {
        station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>() : null;
        centerShift = station != null ? station.GetComponentInChildren<OrbitalCenterShift>() : null;
    }

    private void LateUpdate()
    {
        bool available = station != null && station.IsInitialized;
        visibility.alpha = available && hud.IsInformationVisible ? 1f : 0f;
        bool held = available && centerShift != null && centerShift.CanControl && centerShift.IsPointerSteering;
        bool slowed = available && station.BulletTime.IsActive;
        if (held == pointerHeld && slowed == slowTimeActive) return;
        pointerHeld = held;
        slowTimeActive = slowed;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float pixel = Mathf.Floor(Mathf.Min(rect.width / 66f, rect.height / 34f));
        if (pixel < 1f) return;
        var origin = new Vector2(rect.center.x - 66f * pixel * .5f, rect.center.y + 34f * pixel * .5f);
        DrawPixels(mesh, Orbit, 11, 0, origin, pixel);
        DrawPixels(mesh, LeftArrow, 0, 4, origin, pixel);
        DrawPixels(mesh, RightArrow, 28, 4, origin, pixel);
        DrawPixels(mesh, Mouse, 10, 17, origin, pixel);
        DrawPixels(mesh, Clock, 47, 0, origin, pixel, true);
        DrawPixels(mesh, ShiftKey, 45, 19, origin, pixel, true);
    }

    private void DrawPixels(VertexHelper mesh, string[] rows, int xOffset, int yOffset, Vector2 origin, float pixel, bool slowTime = false)
    {
        // Runs of identical pixels share a quad. Rebuilt only for layout/tint/hold changes.
        var dim = new Color(readyColor.r * .55f, readyColor.g * .55f, readyColor.b * .55f, 1f);
        HudPixelGlyph.Draw(mesh, rows, xOffset, yOffset, origin, pixel,
            slowTime && slowTimeActive ? activeColor : readyColor,
            pointerHeld ? activeColor : dim, color);
    }
}
