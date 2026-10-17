using UnityEngine;
using UnityEngine.UI;

// Only the rotating numbered ring. Labels and the ball share these exact angles.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RouletteWheelGraphic : MaskableGraphic
{
    public const float PocketDegrees = 360f / 37;
    private int winner = -1;
    public void Highlight(int number) { winner = number; SetVerticesDirty(); }
    public static float PocketAngle(int index) => 90 - index * PocketDegrees;
    public static Vector2 Polar(float angle, float radius) => new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
    public static int NumberAt(float wheelAngle, float ballAngle)
    {
        int index = Mathf.RoundToInt(Mathf.Repeat(90 - (ballAngle - wheelAngle), 360) / PocketDegrees) % 37;
        return RouletteSession.Wheel[index];
    }
    public static Color NumberColor(int number) => number == 0 ? new Color32(39, 133, 98, 255)
        : RouletteSession.IsRed(number) ? new Color32(181, 49, 64, 255) : new Color32(21, 26, 32, 255);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        for (int i = 0; i < 37; i++)
        {
            int n = RouletteSession.Wheel[i];
            float angle = PocketAngle(i);
            Color c = NumberColor(n);
            for (int s = 0; s < 4; s++)
            {
                float a = angle - PocketDegrees * .5f + PocketDegrees * s / 4;
                float b = a + PocketDegrees / 4;
                Quad(vh, a, b, 126, 188, c);
                Quad(vh, a, b, 185, 188, n == winner ? new Color32(255, 223, 128, 255) : new Color32(132, 111, 70, 255));
                if (n == winner) Quad(vh, a, b, 126, 133, new Color32(255, 223, 128, 255));
            }
            Quad(vh, angle - PocketDegrees * .5f, angle - PocketDegrees * .5f + .35f, 126, 188, new Color32(190, 164, 104, 255));
        }
    }
    private static void Quad(VertexHelper vh, float a, float b, float inner, float outer, Color color)
    {
        int start = vh.currentVertCount;
        foreach (Vector2 p in new[] { Polar(a, inner), Polar(a, outer), Polar(b, outer), Polar(b, inner) })
            vh.AddVert(new Vector3(Mathf.Round(p.x), Mathf.Round(p.y)), color, Vector2.zero);
        vh.AddTriangle(start, start + 2, start + 1); vh.AddTriangle(start, start + 3, start + 2);
    }
}
