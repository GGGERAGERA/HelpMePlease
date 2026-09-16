using Subject42.Combat.OrbitalStation;
using UnityEngine;

namespace Subject42.DebugLabs
{
    public sealed class CustomOrbitLab : CustomOrbitDrawing
    {
        public const string ScenePath = "Assets/_Project/Dev/Labs/OrbitalLab/CustomOrbitLab.unity";
        public enum TravelDirection { AlongDrawing, AgainstDrawing }
        public OrbitalMountView[] Mounts;
        [Min(0)] public float OrbitSpeed = 2f;
        public TravelDirection Direction;
        [Range(1, 16)] public int MountCount = 4;
        public float TravelDistance => travelDistance;
        private float travelDistance, previousLength;
        private Vector2 scroll;
        protected override Rect PanelRect => new(12, 12, Mathf.Min(310, Screen.width * .3f), Mathf.Min(535, Screen.height - 24));
        protected override void OnEnable()
        {
            OrbitRoot.gameObject.SetActive(true);
            Player.MovementIntent = MovementIntent;
            base.OnEnable();
        }
        private void OnDisable()
        {
            if (Player != null) Player.MovementIntent = null;
            if (OrbitRoot != null) OrbitRoot.gameObject.SetActive(false);
        }
        private Vector2 MovementIntent() => Running
            ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero;
        protected override void Update()
        {
            MountCount = Mathf.Clamp(MountCount, 1, Mounts.Length);
            base.Update();
            if (Running) travelDistance = Mathf.Repeat(travelDistance + Mathf.Max(0, OrbitSpeed) * Time.deltaTime *
                (Direction == TravelDirection.AlongDrawing ? 1 : -1), TotalPathLength);
            UpdateMarkers();
        }
        protected override void OnPathChanged()
        {
            travelDistance = previousLength > 0 ? travelDistance / previousLength * TotalPathLength : 0;
            previousLength = TotalPathLength;
            UpdateMarkers();
        }
        private void UpdateMarkers()
        {
            for (int i = 0; i < Mounts.Length; i++)
            {
                bool visible = Path != null && i < MountCount;
                Mounts[i].gameObject.SetActive(visible);
                if (visible) Mounts[i].transform.localPosition = Path.PositionAtDistance(travelDistance + TotalPathLength * i / MountCount);
            }
        }
        protected override void OnGUI()
        {
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("CUSTOM ORBIT / VIKA / LAB");
            GUILayout.Label($"{State}" + (Running ? " / ORBITING" : ""));
            GUILayout.Label(notice, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUI.enabled = State == PathState.VALID && !Running;
            if (GUILayout.Button("CONFIRM [Enter]")) Confirm();
            GUI.enabled = true;
            if (GUILayout.Button("CLEAR / REDRAW [R]")) Clear();
            GUILayout.Label($"TotalPathLength: {TotalPathLength:F2} units");
            GUILayout.Label($"Arc spacing: {TotalPathLength / Mathf.Max(1, MountCount):F2} units");
            OrbitSpeed = Slider("Orbit Speed (units/s)", OrbitSpeed, 0, 10);
            if (GUILayout.Button("Direction: " + Direction)) Direction = Direction == TravelDirection.AlongDrawing
                ? TravelDirection.AgainstDrawing : TravelDirection.AlongDrawing;
            MountCount = Mathf.RoundToInt(Slider("Mount Count", MountCount, 1, Mounts.Length));
            GUI.enabled = State != PathState.DRAWING;
            MaxDrawRadius = Slider("Max Draw Radius", MaxDrawRadius, 1f, 8f);
            PointMinDistance = Slider("Point Min Distance", PointMinDistance, .02f, .5f);
            CloseThreshold = Slider("Close Threshold", CloseThreshold, .1f, 2f);
            Smoothing = Slider("Smoothing", Smoothing, 0, 1);
            GUI.enabled = true;
            PathWidth = Slider("Path Width", PathWidth, .015f, .2f);
            GUILayout.Label("Spacing follows the line, including crossings.\nSelf-intersections are allowed.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (StartCircle.gameObject.activeSelf)
            {
                Vector3 start = DrawingCamera.WorldToScreenPoint(StartCircle.transform.position);
                GUI.Label(new Rect(start.x + 10, Screen.height - start.y - 25, 80, 24), "START");
            }
        }
        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.Label($"{label}: {value:F2}");
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
