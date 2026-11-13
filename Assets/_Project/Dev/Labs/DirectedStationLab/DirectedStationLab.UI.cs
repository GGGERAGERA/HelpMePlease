#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Subject42.DebugLabs.Telekinetic
{
    public sealed partial class DirectedStationLab
    {
        private static readonly string[] PresetNames = { "1  Одна пушка", "2  Линия", "3  Широкая" };
        private const string DensityControl = "DirectedStationEnemyCount";
        private static readonly Color PanelColor = new(.03f, .06f, .09f, .94f);
        // Coordinates of the orbit-only controls in the shared 1600×900 constructor.
        private static readonly Rect AutoRotateButton = new(88, 761, 218, 38);
        private static readonly Rect ConstructorHint = new(230, 823, 1140, 40);
        private string densityText;
        private bool densityFocused;
        private GUIStyle titleStyle, labelStyle, buttonStyle, inputStyle, hintStyle;

        private float UiScale => Mathf.Min(Screen.width / 960f, Screen.height / 540f);
        private Rect Panel => new(16f, Screen.height / UiScale - 108f, 682f, 92f);
        private bool PointerOverControls(Vector2 screen) =>
            Panel.Contains(new Vector2(screen.x, Screen.height - screen.y) / UiScale);

        private void OnGUI()
        {
            if (station == null || !station.IsInitialized) return;
            EnsureStyles();
            if (Editing)
            {
                densityFocused = false;
                DrawConstructorHint();
                return;
            }

            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1));
            DrawControls();
            GUI.matrix = matrix;
        }

        private void DrawControls()
        {
            Rect panel = Panel;
            Fill(panel, PanelColor);
            GUILayout.BeginArea(new Rect(panel.x + 12, panel.y + 6, panel.width - 24, panel.height - 12));
            GUILayout.Label("НАПРАВЛЕННАЯ СТАНЦИЯ", titleStyle);
            GUILayout.BeginHorizontal();
            for (int index = 0; index < PresetNames.Length; index++)
            {
                var previous = GUI.backgroundColor;
                if (preset == index) GUI.backgroundColor = new Color(.36f, .86f, 1f);
                bool clicked = GUILayout.Button(PresetNames[index], buttonStyle, GUILayout.Width(106), GUILayout.Height(25));
                GUI.backgroundColor = previous;
                if (clicked) SelectPreset(index);
            }
            GUILayout.Space(6);
            GUILayout.Label("Враги", labelStyle, GUILayout.Width(37));
            GUI.SetNextControlName(DensityControl);
            densityText = GUILayout.TextField(densityText, 10, inputStyle, GUILayout.Width(49), GUILayout.Height(25));
            densityFocused = GUI.GetNameOfFocusedControl() == DensityControl;
            bool apply = GUILayout.Button("OK", buttonStyle, GUILayout.Width(34), GUILayout.Height(25));
            if (apply || densityFocused && Event.current.type == EventType.KeyDown &&
                Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                if (int.TryParse(densityText, out int requested) && requested >= 0) SetEnemyCount(requested);
                else densityText = enemyCount.ToString();
                GUI.FocusControl(null);
                densityFocused = false;
                if (!apply) Event.current.Use();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("R  Сброс", buttonStyle, GUILayout.Width(74), GUILayout.Height(25))) ResetFight();
            if (GUILayout.Button("Tab  Сетка", buttonStyle, GUILayout.Width(77), GUILayout.Height(25))) SetEditing(true);
            GUILayout.EndHorizontal();
            GUILayout.Label("WASD · движение     Мышь · прицел     Огонь автоматический", labelStyle);
            GUILayout.EndArea();
        }

        private void DrawConstructorHint()
        {
            var matrix = GUI.matrix;
            int depth = GUI.depth;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            Vector2 origin = (new Vector2(Screen.width, Screen.height) - new Vector2(1600, 900) * scale) * .5f;
            GUI.matrix = Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.depth = -10;
            var input = Event.current;
            if (input.type == EventType.Repaint)
            {
                Fill(AutoRotateButton, new Color(.037f, .052f, .064f));
                Fill(ConstructorHint, new Color(.037f, .052f, .064f));
                GUI.Label(ConstructorHint, "ЛКМ · перенос     R · поворот формы     Delete · в запас     Tab · в бой", hintStyle);
            }
            else if (input.type == EventType.ScrollWheel && new Rect(520, 161, 560, 560).Contains(input.mousePosition) ||
                input.isMouse && AutoRotateButton.Contains(input.mousePosition) ||
                input.type == EventType.KeyDown && input.keyCode == KeyCode.F)
                input.Use();
            GUI.depth = depth;
            GUI.matrix = matrix;
        }

        private static void Fill(Rect rect, Color tint)
        {
            var color = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
        }

        private void ReadCombatShortcuts()
        {
            if (densityFocused) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectPreset(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectPreset(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectPreset(2);
            else if (Input.GetKeyDown(KeyCode.R)) ResetFight();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            labelStyle.normal.textColor = new Color(.75f, .83f, .88f);
            titleStyle = new GUIStyle(labelStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(.61f, .9f, 1f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            hintStyle = new GUIStyle(labelStyle) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        }
    }
}
#endif
