using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public class CustomOrbitDrawing : MonoBehaviour
    {
        public enum PathState { EMPTY, DRAWING, VALID, INVALID }
        [Header("Authored scene references")]
        public Camera DrawingCamera;
        public CharacterMovement2D Player;
        public Transform OrbitRoot;
        public LineRenderer PathLine;
        public LineRenderer StartCircle;
        public LineRenderer DrawArea;
        [Header("Live parameters")]
        [Range(.02f, .5f)] public float PointMinDistance = .12f;
        [Range(.1f, 2f)] public float CloseThreshold = .7f;
        [Range(0, 1)] public float Smoothing = .65f;
        [Range(.015f, .2f)] public float PathWidth = .055f;
        [Min(1f)] public float MaxDrawRadius = 5.5f;
        public PathState State { get; private set; }
        public bool Running { get; private set; }
        public float TotalPathLength => path?.TotalPathLength ?? 0f;
        private readonly List<Vector2> stroke = new();
        private CustomOrbitPath path;
        private float builtSmoothing;
        protected string notice = "Hold LMB and draw. Return near START, then release.";
        public string ProgressLabel { get; set; }
        private float UiScale => Mathf.Clamp(Screen.height / 900f, .65f, 1.3f);
        protected virtual Rect PanelRect
        {
            get
            {
                Vector3 top = DrawingCamera.WorldToScreenPoint(Player.transform.position + Vector3.up * MaxDrawRadius);
                float width = Mathf.Min(620f * UiScale, Screen.width - 24f);
                float height = 158f * UiScale;
                return new Rect(Mathf.Clamp(top.x - width * .5f, 12f, Screen.width - width - 12f),
                    Mathf.Max(12f, Screen.height - top.y - height - 12f), width, height);
            }
        }
        private GUIStyle titleStyle, instructionStyle, statusStyle, buttonStyle;
        private bool interrupted;
        private static readonly Color Cyan = new(.2f, .85f, 1f);
        private static readonly Color AreaColor = new(.55f, .9f, 1f, .25f);

        public CustomOrbitPath Path => path;
        public string Notice => notice;
        public event System.Action<CustomOrbitPath> Confirmed;
        protected virtual void OnEnable() => Clear();
        protected virtual void OnPathChanged() { }

        protected virtual void Update()
        {
            PointMinDistance = Mathf.Max(.02f, PointMinDistance);
            CloseThreshold = Mathf.Max(.1f, CloseThreshold);
            MaxDrawRadius = Mathf.Max(1f, MaxDrawRadius);
            if (Input.GetKeyDown(KeyCode.R)) Clear();
            bool overPanel = PanelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            if (!Running && Input.GetMouseButtonDown(0) && !overPanel) BeginStroke(MouseLocal());
            if (State == PathState.DRAWING)
            {
                if (Input.GetMouseButton(0)) AppendPoint(MouseLocal());
                if (Input.GetMouseButtonUp(0)) EndStroke(MouseLocal());
            }
            if (State == PathState.VALID && !Mathf.Approximately(builtSmoothing, Smoothing)) BuildPath();
            if (Input.GetKeyDown(KeyCode.Return)) Confirm();
            PathLine.widthMultiplier = Mathf.Max(.015f, PathWidth);
            UpdateDrawArea();
            OnPathChanged();
        }

        private bool InsideDrawArea(Vector2 point) =>
            ((Vector2)(OrbitRoot.TransformPoint(point) - Player.transform.position)).sqrMagnitude <= MaxDrawRadius * MaxDrawRadius;

        private void UpdateDrawArea()
        {
            DrawArea.gameObject.SetActive(!Running);
            if (Running) return;
            DrawArea.transform.localScale = Vector3.one * MaxDrawRadius;
            DrawArea.startColor = DrawArea.endColor = InsideDrawArea(MouseLocal())
                ? AreaColor : new Color(1f, .3f, .3f, .4f);
        }

        private Vector2 MouseLocal()
        {
            Ray ray = DrawingCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.forward, OrbitRoot.position);
            plane.Raycast(ray, out float distance);
            return OrbitRoot.InverseTransformPoint(ray.GetPoint(distance));
        }

        public void BeginStroke(Vector2 point)
        {
            if (!InsideDrawArea(point)) return;
            Clear();
            State = PathState.DRAWING;
            stroke.Add(point);
            notice = "Return inside the START circle and release LMB.";
            ShowStroke();
            StartCircle.gameObject.SetActive(true);
            StartCircle.transform.localPosition = point;
            StartCircle.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                StartCircle.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * CloseThreshold);
            }
        }
        public void AppendPoint(Vector2 point)
        {
            if (State != PathState.DRAWING || interrupted) return;
            if (!InsideDrawArea(point)) return;
            if (Vector2.Distance(stroke[stroke.Count - 1], point) < PointMinDistance) return;
            if (stroke.Count >= 4096)
            {
                interrupted = true;
                notice = "Stroke too long (4096 points). Press R to redraw.";
                return;
            }
            stroke.Add(point);
            PathLine.positionCount = stroke.Count;
            PathLine.SetPosition(stroke.Count - 1, point);
        }
        public void EndStroke(Vector2 point)
        {
            if (State != PathState.DRAWING) return;
            if (!InsideDrawArea(point))
            {
                State = PathState.INVALID;
                notice = "Release inside DRAW AREA. Press R to redraw.";
                ShowStroke();
                return;
            }
            // Closure uses the real release location, even below the input sampling distance.
            stroke.Add(point);
            if (interrupted) { State = PathState.INVALID; ShowStroke(); return; }
            BuildPath();
        }
        private void BuildPath()
        {
            builtSmoothing = Smoothing;
            if (!CustomOrbitPath.TryBuild(stroke, PointMinDistance, CloseThreshold, Smoothing, out path, out notice))
            {
                State = PathState.INVALID;
                Running = false;
                ShowStroke();
                return;
            }
            State = PathState.VALID;
            PathLine.loop = true;
            PathLine.positionCount = path.PointCount;
            for (int i = 0; i < path.PointCount; i++) PathLine.SetPosition(i, path.GetPoint(i));
            PathLine.startColor = PathLine.endColor = Cyan;
            OnPathChanged();
        }
        public void Confirm()
        {
            if (State != PathState.VALID) return;
            for (int i = 0; i < path.PointCount; i++)
                if (!InsideDrawArea(path.GetPoint(i)))
                {
                    State = PathState.INVALID;
                    Running = false;
                    notice = "Path outside DRAW AREA. Press R to redraw.";
                    PathLine.startColor = PathLine.endColor = new Color(1, .3f, .3f);
                    path = null;
                    OnPathChanged();
                    DrawArea.gameObject.SetActive(true);
                    return;
                }
            Running = true;
            DrawArea.gameObject.SetActive(false);
            StartCircle.gameObject.SetActive(false);
            Confirmed?.Invoke(path);
        }
        public void Clear()
        {
            path = null;
            stroke.Clear();
            Running = false;
            interrupted = false;
            State = PathState.EMPTY;
            PathLine.positionCount = 0;
            PathLine.loop = false;
            StartCircle.gameObject.SetActive(false);
            OnPathChanged();
            DrawArea.transform.localScale = Vector3.one * MaxDrawRadius;
            DrawArea.startColor = DrawArea.endColor = AreaColor;
            DrawArea.gameObject.SetActive(true);
            notice = "Hold LMB and draw. Return near START, then release.";
        }
        private void ShowStroke()
        {
            PathLine.loop = false;
            PathLine.positionCount = stroke.Count;
            for (int i = 0; i < stroke.Count; i++) PathLine.SetPosition(i, stroke[i]);
            PathLine.startColor = PathLine.endColor = State == PathState.INVALID ? new Color(1, .3f, .3f) : new Color(1, .78f, .2f);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (focused || State != PathState.DRAWING) return;
            State = PathState.INVALID;
            notice = "Drawing interrupted. Press R to redraw.";
            ShowStroke();
        }
        protected virtual void OnGUI()
        {
            int previousDepth = GUI.depth;
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GUI.depth = -10;
            GUI.enabled = true;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                instructionStyle = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter };
                statusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
                buttonStyle = new GUIStyle(GUI.skin.button) { richText = true, fontStyle = FontStyle.Bold };
            }
            float scale = UiScale;
            titleStyle.fontSize = Mathf.RoundToInt(24 * scale);
            instructionStyle.fontSize = Mathf.RoundToInt(21 * scale);
            statusStyle.fontSize = Mathf.RoundToInt(16 * scale);
            buttonStyle.fontSize = Mathf.RoundToInt(19 * scale);
            titleStyle.normal.textColor = Cyan;
            instructionStyle.normal.textColor = Color.white;
            statusStyle.normal.textColor = State == PathState.INVALID ? new Color(1f, .4f, .35f) : Color.white;
            Rect area = PanelRect;
            GUI.color = new Color(.015f, .045f, .06f, .94f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(.2f, .85f, 1f, .65f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * scale), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x + 16 * scale, area.y + 9 * scale, area.width - 170 * scale, 32 * scale), "НАРИСУЙТЕ ОРБИТУ", titleStyle);
            GUI.Label(new Rect(area.x + area.width - 166 * scale, area.y + 12 * scale, 150 * scale, 26 * scale), ProgressLabel, statusStyle);
            string instruction = State == PathState.VALID
                ? "<b><color=#70FFD4>ENTER</color></b> — подтвердить орбиту"
                : "<b><color=#55DFFF>ЛКМ</color></b> — удерживайте и рисуйте";
            string detail = State == PathState.VALID ? "ГОТОВО · замкнутая орбита" : State == PathState.INVALID
                ? "INVALID · замкните линию у START внутри области" : "Вернитесь к START и отпустите ЛКМ";
            GUI.Label(new Rect(area.x + 10 * scale, area.y + 44 * scale, area.width - 20 * scale, 30 * scale), instruction, instructionStyle);
            int largeFont = instructionStyle.fontSize;
            instructionStyle.fontSize = Mathf.RoundToInt(16 * scale);
            GUI.Label(new Rect(area.x + 10 * scale, area.y + 77 * scale, area.width - 20 * scale, 24 * scale), detail, instructionStyle);
            instructionStyle.fontSize = largeFont;
            float buttonWidth = (area.width - 42 * scale) * .5f;
            GUI.enabled = State == PathState.VALID;
            if (GUI.Button(new Rect(area.x + 16 * scale, area.y + 110 * scale, buttonWidth, 34 * scale), "<color=#70FFD4>ENTER</color>  ПОДТВЕРДИТЬ", buttonStyle)) Confirm();
            GUI.enabled = true;
            if (GUI.Button(new Rect(area.x + 26 * scale + buttonWidth, area.y + 110 * scale, buttonWidth, 34 * scale), "R  ПЕРЕРИСОВАТЬ", buttonStyle)) Clear();
            DrawStartLabel();
            GUI.color = previousColor;
            GUI.enabled = previousEnabled;
            GUI.depth = previousDepth;
        }
        protected void DrawStartLabel()
        {
            if (StartCircle.gameObject.activeSelf)
            {
                Vector3 start = DrawingCamera.WorldToScreenPoint(StartCircle.transform.position);
                GUI.Label(new Rect(start.x + 10, Screen.height - start.y - 25, 80, 24), "START");
            }
        }
    }
}
