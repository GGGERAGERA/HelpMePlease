using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.UI;

// Pointer steering and Bullet Time share the existing HUD binding and pixel grid.
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
    private const int ChargeWidth = 15;
    private int chargePixels = ChargeWidth;

    private static readonly string[] ChargeTrack =
    {
        "OOOOOOOOOOOOOOOOO", "OdddddddddddddddO",
        "OdddddddddddddddO", "OOOOOOOOOOOOOOOOO"
    };
    private static readonly string[] ChargePixel = { "#", "#" };

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
        // Cache only display resolution; the ability remains the charge owner.
        int charge = available ? Mathf.CeilToInt(station.BulletTime.Energy * ChargeWidth) : 0;
        if (held == pointerHeld && slowed == slowTimeActive && charge == chargePixels) return;
        pointerHeld = held;
        slowTimeActive = slowed;
        chargePixels = charge;
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
        HudPixelGlyph.Draw(mesh, ChargeTrack, 45, 14, origin, pixel, readyColor, readyColor, color);
        for (int i = 0; i < chargePixels; i++)
            DrawPixels(mesh, ChargePixel, 46 + i, 15, origin, pixel, true);
        DrawPixels(mesh, ShiftKey, 45, 19, origin, pixel, true);
    }

    private void DrawPixels(VertexHelper mesh, string[] rows, int xOffset, int yOffset, Vector2 origin, float pixel, bool slowTime = false)
    {
        // Runs of identical pixels share a quad. Rebuild at pixel charge boundaries.
        var dim = new Color(readyColor.r * .55f, readyColor.g * .55f, readyColor.b * .55f, 1f);
        Color ink = readyColor;
        if (slowTime)
            ink = chargePixels == 0 ? dim : chargePixels <= ChargeWidth / 3
                ? new Color(1f, .65f, .2f, 1f) : slowTimeActive ? activeColor : readyColor;
        HudPixelGlyph.Draw(mesh, rows, xOffset, yOffset, origin, pixel,
            ink,
            pointerHeld ? activeColor : dim, color);
    }
}
