#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Subject42.Dev.VisualEffects
{
    public sealed partial class EffectLabController
    {
        private GUIStyle headingStyle, textStyle, smallStyle, rowStyle, selectedStyle;

        private void OnGUI()
        {
            if (headingStyle == null)
            {
                headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                smallStyle = new GUIStyle(textStyle) { fontSize = 11 };
                rowStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 12, clipping = TextClipping.Clip };
                selectedStyle = new GUIStyle(rowStyle); selectedStyle.normal.textColor = new Color(.3f, 1f, .86f);
            }
            EditorGUI.DrawRect(new Rect(8, 8, 350, Screen.height - 16), new Color(.025f, .04f, .06f, .97f));
            GUILayout.BeginArea(new Rect(20, 18, 326, Screen.height - 36));
            GUILayout.Label("EFFECTS LAB", headingStyle);
            GUILayout.Label($"{catalog.Effects.Length} позиций · {catalog.CoveredPrefabs} FX-prefab", textStyle);
            GUILayout.Space(7);
            string nextSearch = GUILayout.TextField(search, GUILayout.Height(25));
            int nextCategory = EditorGUILayout.Popup(category, new[] { "Все типы", "Частицы", "Следы", "Линии", "Спрайты / экран", "Presentation API" });
            int nextScope = EditorGUILayout.Popup(scope, new[] { "Все источники", "В production-данных", "Без production-связей" });
            if (nextSearch != search || nextCategory != category || nextScope != scope)
            { search = nextSearch; category = nextCategory; scope = nextScope; Refilter(); }
            GUILayout.Label($"Найдено: {filtered.Count}", smallStyle);
            Rect viewport = GUILayoutUtility.GetRect(1, Mathf.Max(160, Screen.height - 380), GUILayout.ExpandWidth(true));
            const float rowHeight = 27;
            listScroll = GUI.BeginScrollView(viewport, listScroll, new Rect(0, 0, viewport.width - 18, filtered.Count * rowHeight));
            int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / rowHeight));
            int last = Mathf.Min(filtered.Count, first + Mathf.CeilToInt(viewport.height / rowHeight) + 1);
            for (int i = first; i < last; i++)
            {
                int index = filtered[i]; var entry = catalog.Effects[index];
                string title = (entry.InProduction ? "● " : "  ") + entry.DisplayName;
                if (GUI.Button(new Rect(0, i * rowHeight, viewport.width - 20, rowHeight - 2), title,
                    index == CurrentCatalogIndex ? selectedStyle : rowStyle)) SelectEffect(index);
            }
            GUI.EndScrollView();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀")) Step(-1);
            if (GUILayout.Button("Повторить")) Replay();
            if (GUILayout.Button("Стоп")) { loop = false; StopPreview(); }
            if (GUILayout.Button("▶")) Step(1);
            GUILayout.EndHorizontal();
            loop = GUILayout.Toggle(loop, "Автоповтор");
            GUILayout.Label($"Период: {replayInterval:F1} с · частиц сейчас: {LiveParticleCount}", smallStyle);
            replayInterval = GUILayout.HorizontalSlider(replayInterval, 1.5f, 12f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Открыть Shader Lab")) RequestOtherScene(EffectLabCatalog.ShaderScenePath);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (Current == null) return;
            float width = Mathf.Max(320, Screen.width - 390);
            EditorGUI.DrawRect(new Rect(374, Screen.height - 192, width, 180), new Color(.025f, .04f, .06f, .96f));
            GUILayout.BeginArea(new Rect(388, Screen.height - 182, width - 28, 164));
            GUILayout.BeginHorizontal();
            GUILayout.Label(Current.Library + " · " + Current.Kind + (Current.InProduction ? " · В PRODUCTION-ДАННЫХ" : " · БИБЛИОТЕКА / DEV"), textStyle);
            if (GUILayout.Button("Выделить ассет", GUILayout.Width(135))) Ping(Current.SourceAssetPath);
            GUILayout.EndHorizontal();
            GUILayout.Label(Current.SourceAssetPath + (Current.HierarchyPath.Length == 0 ? "" : " / " + Current.HierarchyPath), smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Масштаб стенда: {previewScale:F2}", smallStyle, GUILayout.Width(155));
            float logScale = GUILayout.HorizontalSlider(Mathf.Log10(previewScale), -2.3f, 1.3f, GUILayout.Width(190));
            if (Mathf.Abs(logScale - Mathf.Log10(previewScale)) > .015f) { previewScale = Mathf.Pow(10, logScale); Replay(); }
            if (GUILayout.Button("Вписать", GUILayout.Width(80))) FitEffect();
            if (GUILayout.Button(view3D ? "Ракурс: 3D" : "Ракурс: 2D", GUILayout.Width(115)))
            {
                view3D = !view3D;
                previewCamera.transform.SetPositionAndRotation(new Vector3(2, view3D ? 9 : 0, -18),
                    Quaternion.LookRotation(new Vector3(0, view3D ? -9 : 0, 18)));
            }
            GUILayout.EndHorizontal();
            if (Current.Callers.Length > 0)
            {
                var caller = Current.Callers[callerIndex % Current.Callers.Length];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Источник {callerIndex + 1}/{Current.Callers.Length}: {caller.ComponentType}.{caller.PropertyPath}", textStyle);
                if (GUILayout.Button("Другой источник", GUILayout.Width(145))) { callerIndex = (callerIndex + 1) % Current.Callers.Length; Replay(); }
                if (caller.ScriptPath.Length > 0 && GUILayout.Button("Код", GUILayout.Width(55))) Ping(caller.ScriptPath);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(Current.Notes, smallStyle);
            if (error.Length > 0) GUILayout.Label(error, textStyle);
            GUILayout.EndArea();
        }

        private void Step(int offset)
        {
            if (filtered.Count == 0) return;
            int ordinal = filtered.IndexOf(CurrentCatalogIndex);
            SelectEffect(filtered[(ordinal + offset + filtered.Count) % filtered.Count]);
        }

        private static void Ping(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            Selection.activeObject = asset; EditorGUIUtility.PingObject(asset);
        }

        public static void RequestOtherScene(string path)
        {
            SessionState.SetString(EffectLabCatalog.PendingSceneKey, path);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
