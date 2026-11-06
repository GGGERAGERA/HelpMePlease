#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Subject42.Dev.VisualEffects
{
    // Scene-owned preview controls. The authored materials remain unchanged.
    public sealed class ShaderLabController : MonoBehaviour
    {
        [Serializable]
        public sealed class Page
        {
            public string Title;
            public GameObject Root;
            public Renderer[] Samples = Array.Empty<Renderer>();
        }

        [SerializeField] private Page[] pages = Array.Empty<Page>();
        [SerializeField] private int initialPage;
        private static readonly int LabTimeId = Shader.PropertyToID("_LabTime");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private MaterialPropertyBlock properties;
        private float animationTime;
        private float playbackSpeed = 1f;
        private float intensity = 1f;
        private bool paused;
        private int selectedPage;
        private string[] pageTitles;

        public IEnumerable<Renderer> Samples => pages.SelectMany(p => p.Samples);
        public float AnimationTime => animationTime;
        public int SelectedPage => selectedPage;
        public int PageCount => pages.Length;
        public Renderer[] GetPageSamples(int index) => pages[index].Samples;
        public bool Paused { get => paused; set => paused = value; }
        public float Intensity { get => intensity; set => intensity = Mathf.Clamp01(value); }

        public void SelectPage(int index)
        {
            if (index < 0 || index >= pages.Length) throw new ArgumentOutOfRangeException(nameof(index));
            selectedPage = index;
            for (int i = 0; i < pages.Length; i++) pages[i].Root.SetActive(i == index);
        }

        public void ResetAnimation() => animationTime = 0f;

        private void OnEnable()
        {
            properties = new MaterialPropertyBlock();
            pageTitles = pages.Select(p => p.Title).ToArray();
            SelectPage(initialPage);
            ApplyProperties();
        }

        private void Update()
        {
            if (!paused) animationTime += Time.unscaledDeltaTime * playbackSpeed;
            ApplyProperties();
        }

        private void ApplyProperties()
        {
            foreach (var page in pages)
            foreach (Renderer sample in page.Samples)
            {
                sample.GetPropertyBlock(properties);
                properties.SetFloat(LabTimeId, animationTime);
                properties.SetFloat(IntensityId, intensity);
                sample.SetPropertyBlock(properties);
            }
        }

        private void OnGUI()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Mathf.Min(1f, Screen.width / 1100f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            int page = GUI.Toolbar(new Rect(24, 20, Mathf.Min(width - 48, 1080), 30), selectedPage, pageTitles);
            if (page != selectedPage) SelectPage(page);
            EditorGUI.DrawRect(new Rect(12, height - 90, width - 24, 78), new Color(0.025f, 0.04f, 0.06f, 0.97f));
            GUILayout.BeginArea(new Rect(26, height - 83, width - 52, 64));
            GUILayout.Label("Пиксельные шейдеры · локальное время стенда · эксперименты, не игровые настройки");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Продолжить" : "Пауза", GUILayout.Width(105))) paused = !paused;
            if (GUILayout.Button("Сначала", GUILayout.Width(80))) ResetAnimation();
            GUILayout.Space(12);
            GUILayout.Label($"Скорость: {playbackSpeed:F2}×", GUILayout.Width(112));
            playbackSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.1f, 2f, GUILayout.Width(110));
            GUILayout.Space(12);
            GUILayout.Label($"Сила: {intensity:F2}", GUILayout.Width(80));
            float nextIntensity = GUILayout.HorizontalSlider(intensity, 0f, 1f, GUILayout.Width(110));
            if (nextIntensity != intensity) Intensity = nextIntensity;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Открыть эффекты", GUILayout.Width(160)))
                EffectLabController.RequestOtherScene(EffectLabCatalog.EffectsScenePath);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }
    }
}
#endif
