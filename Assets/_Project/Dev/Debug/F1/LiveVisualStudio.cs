#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Subject42.Visuals
{
    // Gesture ownership is shared by sliders, the window header and resize handle.
    public sealed class LiveVisualGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        public Action Begin, End;
        public Action<PointerEventData> Drag;
        public void OnPointerDown(PointerEventData data) => Begin?.Invoke();
        public void OnPointerUp(PointerEventData data) => End?.Invoke();
        public void OnDrag(PointerEventData data) => Drag?.Invoke(data);
        public void OnEndDrag(PointerEventData data) => End?.Invoke();
    }

    public sealed class LiveVisualStudio : MonoBehaviour
    {
        private static LiveVisualStudio instance;
        private const string Pref = "Subject42.LiveVisualStudio.";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Color Background = new(.035f, .045f, .06f, .97f);
        private static readonly Color RowColor = new(.085f, .105f, .135f, 1f);
        private static readonly Color Accent = new(.13f, .58f, .72f, 1f);
        private static readonly Color Muted = new(.65f, .71f, .77f, 1f);
        private readonly List<Action> valueRefresh = new();
        private readonly HashSet<string> expanded = new(StringComparer.Ordinal);
        private readonly HashSet<string> favorites = new(StringComparer.Ordinal);
        private readonly List<VisualTarget> pickHits = new();
        private LiveVisualRuntime runtime;
        private VisualTarget selected;
        private GameObject uiRoot, body, windowSettings;
        private Canvas canvas;
        private CanvasGroup opacity;
        private RectTransform panel, content, targetsRoot, propertiesRoot, selectionOutline, baselineInfoRoot;
        private TextMeshProUGUI status, targetInfo, modeInfo;
        private TMP_InputField targetSearch, propertySearch, presetName;
        private string scope = "Type", expandedTarget;
        private bool minimized, showTargets, onlyFavorites, picking, heldGesture, refreshing, showBaselineNotes, relatedTargets, showTargetDetails;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private float nextRefresh, savedHeight = 810f;
        private int consumedPointerFrame = -1;
        public static bool IsOpen => instance != null && instance.uiRoot != null && instance.uiRoot.activeSelf;
        public VisualTarget Selected => selected;

        public static void Open()
        {
            if (LiveVisualRuntime.Instance == null) { Debug.LogWarning("Live Visual Studio: production-сцена ещё не готова."); return; }
            if (instance == null) instance = new GameObject("Live Visual Studio").AddComponent<LiveVisualStudio>();
            instance.Show();
        }

        public static bool BlocksGameplayPointer => instance != null && instance.OwnsPointer();
        public static bool BlocksKeyboardInput => instance != null && instance.TextFieldFocused;
        private bool TextFieldFocused
        {
            get
            {
                var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return IsOpen && focused != null && focused.transform.IsChildOf(panel) &&
                    focused.GetComponent<TMP_InputField>() is { isFocused: true };
            }
        }
        private bool OwnsPointer()
        {
            bool held = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (!held) heldGesture = false;
            bool over = IsOpen && RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition);
            if (over && held) heldGesture = true;
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool typing = IsOpen && focused != null && focused.transform.IsChildOf(panel) && focused.GetComponent<TMP_InputField>() is { isFocused: true };
            return over || heldGesture || picking || typing || consumedPointerFrame == Time.frameCount;
        }

        private void Show()
        {
            if (uiRoot == null) Build();
            if (runtime != LiveVisualRuntime.Instance)
            {
                if (runtime != null) runtime.Changed -= RuntimeChanged;
                runtime = LiveVisualRuntime.Instance; runtime.Changed += RuntimeChanged;
            }
            if (!IsOpen)
            {
                previousCursorVisible = Cursor.visible; previousCursorLock = Cursor.lockState;
                uiRoot.SetActive(true);
            }
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            if (selected == null || !selected.IsAlive)
                selected = runtime.Targets.FirstOrDefault(t => t.IsAlive && t.IsActive && t.Component is SpriteRenderer && t.Group.StartsWith("Враги", StringComparison.Ordinal)) ?? runtime.Targets.FirstOrDefault(t => t.IsAlive);
            RefreshSelection(); Canvas.ForceUpdateCanvases(); Constrain();
        }

        private void Hide()
        {
            if (!IsOpen) return;
            picking = false; consumedPointerFrame = Time.frameCount; uiRoot.SetActive(false);
            if (!Subject42DebugMenu.IsDebugMenuOpen) { Cursor.visible = previousCursorVisible; Cursor.lockState = previousCursorLock; }
            SaveWindow();
        }

        private void Build()
        {
            uiRoot = new GameObject("Live Visual Studio UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot.transform.SetParent(transform, false);
            canvas = uiRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue - 2;
            var scaler = uiRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f); scaler.matchWidthOrHeight = .5f;
            panel = Rect("Visual Studio Panel", uiRoot.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f); panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(Pref + "X", 14f), PlayerPrefs.GetFloat(Pref + "Y", -46f));
            panel.sizeDelta = new Vector2(Mathf.Clamp(PlayerPrefs.GetFloat(Pref + "Width", 450f), 360f, 720f), savedHeight);
            panel.gameObject.AddComponent<Image>().color = Background;
            opacity = panel.gameObject.AddComponent<CanvasGroup>(); opacity.alpha = Mathf.Clamp(PlayerPrefs.GetFloat(Pref + "Alpha", .97f), .3f, 1f);
            foreach (string key in PlayerPrefs.GetString(Pref + "Favorites", "").Split('\n')) if (key.Length > 0) favorites.Add(key);
            var header = Rect("Drag Header", panel); header.anchorMin = new Vector2(0f, 1f); header.anchorMax = Vector2.one;
            header.pivot = new Vector2(.5f, 1f); header.sizeDelta = new Vector2(0f, 46f);
            header.gameObject.AddComponent<Image>().color = new Color(.08f, .14f, .18f, 1f);
            var title = Text(header, "Live Visual Studio", 19f, Color.white); Fill(title.rectTransform, 12, 94);
            var drag = header.gameObject.AddComponent<LiveVisualGesture>();
            drag.Drag = e => { panel.anchoredPosition += e.delta / canvas.scaleFactor; Constrain(); };
            drag.End = SaveWindow;
            var minimize = Button(header, "—", () => { minimized = !minimized; body.SetActive(!minimized); panel.sizeDelta = new Vector2(panel.sizeDelta.x, minimized ? 46 : savedHeight); Constrain(); });
            AnchorTopRight(minimize.GetComponent<RectTransform>(), 48, 8, 34, 30);
            var hide = Button(header, "×", Hide); AnchorTopRight(hide.GetComponent<RectTransform>(), 8, 8, 34, 30);
            body = Rect("Body", panel).gameObject; Fill(body.GetComponent<RectTransform>(), 0, 0, 0, 46);
            var scrollRoot = Rect("Scroll", body.transform); Fill(scrollRoot, 9, 9, 12, 0);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28f;
            var viewport = Rect("Viewport", scrollRoot); Fill(viewport); viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .01f); viewport.gameObject.AddComponent<RectMask2D>();
            content = Vertical("Content", viewport, 6); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.content = content; scroll.viewport = viewport;
            var modes = Horizontal(content, 34);
            foreach (VisualComparisonMode mode in Enum.GetValues(typeof(VisualComparisonMode))) { var captured = mode; Button(modes, mode.ToString(), () => { Safe(() => runtime.SetMode(captured)); RefreshValues(); }); }
            modeInfo = Label(content, "", 12, Muted, 34);
            var information = Horizontal(content, 26);
            Button(information, "Источник Original", () => { showBaselineNotes = !showBaselineNotes; RefreshBaselineNotes(); });
            Button(information, "Настройки окна", () => windowSettings.SetActive(!windowSettings.activeSelf));
            baselineInfoRoot = Vertical("Baseline Notes", content, 3);
            windowSettings = Vertical("Window Settings", content, 3).gameObject;
            var window = Horizontal(windowSettings.transform, 28);
            Button(window, "F8 · скрыть", Hide); Button(window, "Ширина -", () => Resize(-35)); Button(window, "Ширина +", () => Resize(35));
            var alphaRow = Horizontal(windowSettings.transform, 26); Text(alphaRow, "UI alpha", 12, Muted).gameObject.AddComponent<LayoutElement>().preferredWidth = 68;
            var alpha = Slider(alphaRow, .3f, 1f, opacity.alpha); alpha.onValueChanged.AddListener(v => { opacity.alpha = v; }); alpha.gameObject.AddComponent<LiveVisualGesture>().End = SaveWindow;
            windowSettings.SetActive(false);
            var actions = Horizontal(content, 32);
            Button(actions, "Пипетка", () => { picking = !picking; pickHits.Clear(); SetStatus(picking ? "Щёлкните объект в Game View. Esc отменяет выбор." : "Выбор отменён."); });
            Button(actions, "Объекты", () => { relatedTargets = false; showTargets = !showTargets; BuildTargets(); });
            Button(actions, "Компоненты", () => { relatedTargets = !relatedTargets; showTargets = false; BuildTargets(); });
            Button(actions, "Обновить", () => { runtime.RefreshScene(); RefreshSelection(); });
            Button(actions, "Детали", () => { showTargetDetails = !showTargetDetails; targetInfo.GetComponent<LayoutElement>().preferredHeight = showTargetDetails ? 140 : 50; RefreshValues(); });
            targetSearch = Field(content, "Найти объект / тип / путь", "", null);
            targetSearch.onValueChanged.AddListener(_ => { showTargets = true; BuildTargets(); });
            targetsRoot = Vertical("Target Browser", content, 3);
            targetInfo = Label(content, "", 12, Muted, 50); targetInfo.richText = false;
            var scopes = Horizontal(content, 30);
            foreach (string item in new[] { "Group", "Type", "Instance" }) { string captured = item; Button(scopes, item, () => { scope = captured; RefreshSelection(); }); }
            Label(content, "Group: категория · Type: тип · Instance: один экземпляр", 11, Muted, 18);
            var history = Horizontal(content, 30);
            Button(history, "Отмена", () => Safe(runtime.Undo)); Button(history, "Повтор", () => Safe(runtime.Redo));
            Button(history, "Сброс объекта", () => Safe(() => { if (selected == null || !selected.IsAlive) throw new InvalidOperationException("Для сброса объекта сначала выберите существующую цель."); runtime.Reset(scope, selected); }));
            Button(history, "Сброс всех", () => Safe(() => runtime.Reset(scope)));
            var parameterFilter = Horizontal(content, 30);
            propertySearch = Field(parameterFilter, "Поиск параметра / категории", "", null);
            propertySearch.onValueChanged.AddListener(_ => BuildProperties());
            var favoriteFilter = Button(parameterFilter, "Избр.", () => { onlyFavorites = !onlyFavorites; BuildProperties(); }); Width(favoriteFilter.gameObject, 60);
            propertiesRoot = Vertical("Parameters", content, 4);
            Label(content, "Профили · только явное сохранение меняет файлы", 14, Color.white, 26);
            presetName = Field(content, "Имя профиля", "MyVisuals", null);
            var files = Horizontal(content, 30);
            Button(files, "Save JSON", () => Safe(() => SaveProfile(false)));
            Button(files, "Load JSON", () => Safe(LoadProfile));
            var clipboard = Horizontal(content, 30);
            Button(clipboard, "JSON → буфер", () => Safe(() => { GUIUtility.systemCopyBuffer = runtime.Export(ProfileName()); SetStatus("JSON скопирован. Instance-настройки исключены."); }));
            Button(clipboard, "JSON ← буфер", () => Safe(() => LoadJson(GUIUtility.systemCopyBuffer)));
            Button(content, "ПРИМЕНИТЬ ПРОФИЛЬ К PRODUCTION", () => Safe(() => SaveProfile(true)));
            Label(content, "Сохранение исключает Instance. Производственный профиль применяется обычной сборкой. Sliders не сохраняют assets.", 11, Muted, 47);
            status = Label(content, "Перетаскивайте заголовок. ± — точный шаг; Shift — в 10 раз точнее.", 12, Muted, 62); status.richText = false;
            selectionOutline = Rect("Selected Renderer Bounds", uiRoot.transform); selectionOutline.SetAsFirstSibling();
            foreach (bool vertical in new[] { false, true }) foreach (bool end in new[] { false, true })
            {
                var edge = Rect("Edge", selectionOutline); edge.gameObject.AddComponent<Image>().color = new Color(.3f, 1f, .85f, .9f); edge.GetComponent<Image>().raycastTarget = false;
                edge.anchorMin = vertical ? new Vector2(end ? 1 : 0, 0) : new Vector2(0, end ? 1 : 0);
                edge.anchorMax = vertical ? new Vector2(end ? 1 : 0, 1) : new Vector2(1, end ? 1 : 0);
                edge.sizeDelta = vertical ? new Vector2(2, 0) : new Vector2(0, 2); edge.anchoredPosition = Vector2.zero;
            }
            var resize = Rect("Resize Grip", panel); resize.anchorMin = resize.anchorMax = new Vector2(1, 0); resize.pivot = new Vector2(1, 0); resize.sizeDelta = new Vector2(18, 18); resize.anchoredPosition = Vector2.zero;
            resize.gameObject.AddComponent<Image>().color = Accent;
            var resizeDrag = resize.gameObject.AddComponent<LiveVisualGesture>(); resizeDrag.Drag = e => { panel.sizeDelta = new Vector2(Mathf.Clamp(panel.sizeDelta.x + e.delta.x / canvas.scaleFactor, 360, 720), panel.sizeDelta.y); Constrain(); }; resizeDrag.End = SaveWindow;
            uiRoot.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) { if (IsOpen) Hide(); else Show(); }
            if (!IsOpen || runtime == null) return;
            OwnsPointer();
            if (picking && Input.GetKeyDown(KeyCode.Escape)) { consumedPointerFrame = Time.frameCount; picking = false; pickHits.Clear(); SetStatus("Выбор отменён."); }
            if (picking && Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition)) Pick();
            if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .15f; RefreshValues(); }
        }
        private void LateUpdate()
        {
            if (selectionOutline == null) return;
            var renderer = selected?.Component as Renderer;
            bool show = IsOpen && renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && Camera.main != null;
            selectionOutline.gameObject.SetActive(show);
            if (!show) return;
            var camera = Camera.main; Bounds bounds = renderer.bounds;
            Vector3 a = camera.WorldToScreenPoint(bounds.min), b = camera.WorldToScreenPoint(bounds.max);
            if (a.z < 0 || b.z < 0) { selectionOutline.gameObject.SetActive(false); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, a, null, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, b, null, out Vector2 max);
            selectionOutline.localPosition = (min + max) * .5f; selectionOutline.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }
        private void RuntimeChanged() { if (IsOpen && !refreshing) RefreshValues(); }
        private void RefreshSelection()
        {
            if (selected != null && expandedTarget != selected.Key)
            {
                expandedTarget = selected.Key; expanded.Clear();
                string correction = selected.Properties.FirstOrDefault(p => p.Category.StartsWith("Коррекция пикселей", StringComparison.Ordinal))?.Category;
                if (correction != null) expanded.Add(correction);
                else if (selected.Properties.Count > 0) expanded.Add(selected.Properties[0].Category);
            }
            BuildTargets(); BuildProperties(); RefreshValues();
        }
        private void RefreshBaselineNotes()
        {
            if (baselineInfoRoot == null || runtime == null) return;
            Clear(baselineInfoRoot); if (!showBaselineNotes) return;
            Label(baselineInfoRoot, "Git-снимок задаёт только поддерживаемые параметры. При отсутствии значения остаётся Current. Изменения кода и геометрии не восстанавливаются ползунком.", 11, Muted, 65);
            foreach (string note in runtime.Original.Notes)
                Label(baselineInfoRoot, note, 11, Muted, 30 + note.Length / 55 * 14);
        }
        private void RefreshValues()
        {
            if (runtime == null || refreshing) return;
            refreshing = true;
            try
            {
                string revision = runtime.Original.BaselineRevision;
                modeInfo.text = $"{runtime.Mode} · {scope} · Git {(string.IsNullOrEmpty(revision) ? "нет" : revision.Substring(0, Mathf.Min(8, revision.Length)))}\nGit: {runtime.OriginalMatchedCount} подключено · {runtime.OriginalUnmatchedCount} вне сцены / без адаптера";
                targetInfo.text = DescribeTarget();
                if (selected != null && selected.IsAlive) foreach (var action in valueRefresh) action();
            }
            finally { refreshing = false; }
        }
        private string DescribeTarget()
        {
            if (selected == null || !selected.IsAlive) return "Объект не выбран или уже возвращён/удалён. Откройте список или пипетку.";
            if (!showTargetDetails)
            {
                string material = selected.Component is Renderer r ? " · " + r.sharedMaterial?.name : "";
                return selected.Label + "\n" + selected.Group + " · Active: " + selected.IsActive + material;
            }
            string details = selected.Label + "\n" + selected.TypeKey + "\n";
            if (selected.Component != null)
                details += "GameObject: " + selected.Component.transform.root.name + "/" + LiveVisualRuntime.RelativePath(selected.Component.transform, selected.Component.transform.root) + "\n";
            if (selected.Component is Renderer renderer)
            {
                var mat = renderer.sharedMaterial; var camera = Camera.main;
                bool inFrame = camera != null && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0 && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), renderer.bounds);
                details += $"Active: {selected.IsActive} · Renderer: {renderer.enabled} · в кадре: {inFrame}\nMaterial: {mat?.name} · Shader: {mat?.shader?.name}\nПерекрытие другими объектами не проверяется.";
            }
            else details += "Active: " + selected.IsActive + (selected.Component == null ? " · runtime config" : " · " + selected.Component.GetType().Name);
            return details;
        }
        private void BuildTargets()
        {
            if (targetsRoot == null || runtime == null) return; Clear(targetsRoot);
            foreach (var hit in pickHits)
            {
                var captured = hit; Button(targetsRoot, "Под курсором: " + hit.Label, () => Select(captured));
            }
            if (relatedTargets && selected?.Component != null)
            {
                var enemy = selected.Component.GetComponentInParent<EnemyHealth>();
                Transform owner = enemy != null ? enemy.transform : selected.Component.transform;
                foreach (var target in runtime.Targets.Where(t => t.IsAlive && t.Component != null && (t.Component.transform == owner || t.Component.transform.IsChildOf(owner))))
                {
                    var captured = target; Button(targetsRoot, target.Label, () => Select(captured));
                }
                return;
            }
            if (!showTargets) return;
            string query = targetSearch.text.Trim();
            var targets = runtime.Targets.Where(t => t.IsAlive && (query.Length == 0 || (t.Group + " " + t.Label + " " + t.TypeKey).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            if (scope != "Instance") targets = targets.GroupBy(t => t.TypeKey).Select(group => group.FirstOrDefault(t => t.IsActive) ?? group.First());
            foreach (var target in targets.OrderBy(t => t.Group).ThenBy(t => t.Label))
            {
                var captured = target; Button(targetsRoot, target.Group + " · " + target.Label + (target.IsActive ? "" : " [inactive]"), () => Select(captured));
            }
            Label(targetsRoot, "Список по Type; Instance показывает отдельные экземпляры. Новые объекты: «Обновить».", 11, Muted, 40);
        }
        private void Select(VisualTarget target) { selected = target; pickHits.Clear(); picking = false; showTargets = false; relatedTargets = false; RefreshSelection(); }
        private void Pick()
        {
            // Consume the entire gesture, including the frame that exits picking mode.
            consumedPointerFrame = Time.frameCount; heldGesture = true;
            var camera = Camera.main;
            if (camera == null) { SetStatus("Не назначена игровая MainCamera."); picking = false; return; }
            Ray ray = camera.ScreenPointToRay(Input.mousePosition); pickHits.Clear();
            foreach (var target in runtime.Targets)
            {
                if (!target.IsAlive || !target.IsActive || target.Component is not Renderer renderer || !renderer.enabled || (camera.cullingMask & 1 << renderer.gameObject.layer) == 0) continue;
                var bounds = renderer.bounds; bounds.Expand(new Vector3(.015f, .015f, .015f));
                if (bounds.IntersectRay(ray)) pickHits.Add(target);
            }
            pickHits.Sort((a, b) =>
            {
                var first = (Renderer)a.Component; var second = (Renderer)b.Component;
                int layer = SortingLayer.GetLayerValueFromID(second.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(first.sortingLayerID));
                return layer != 0 ? layer : second.sortingOrder.CompareTo(first.sortingOrder);
            });
            picking = false;
            if (pickHits.Count == 1) Select(pickHits[0]);
            else { BuildTargets(); SetStatus(pickHits.Count == 0 ? "Под курсором нет зарегистрированных Renderer. Нажмите «Обновить»." : $"Перекрываются Renderer: {pickHits.Count}. Выберите нужный в списке."); }
        }

        private void BuildProperties()
        {
            if (propertiesRoot == null) return; valueRefresh.Clear(); Clear(propertiesRoot);
            if (selected == null || !selected.IsAlive) return;
            string query = propertySearch.text.Trim();
            var properties = selected.Properties.Where(p => (!onlyFavorites || favorites.Contains(FavoriteKey(p))) && (query.Length == 0 || (p.Label + " " + p.Id + " " + p.Category).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            foreach (var group in properties.GroupBy(p => p.Category))
            {
                string category = group.Key;
                bool open = expanded.Contains(category) || query.Length > 0;
                var header = Horizontal(propertiesRoot, 30);
                Button(header, (open ? "- " : "+ ") + category, () => { if (!expanded.Add(category)) expanded.Remove(category); BuildProperties(); });
                var reset = Button(header, "Сброс", () => Safe(() => runtime.Reset(scope, selected, category: category))); Width(reset.gameObject, 54);
                if (!open) continue;
                foreach (var property in group)
                {
                    if (property.Id.EndsWith(".r", StringComparison.Ordinal)) BuildColorTools(property);
                    BuildProperty(property);
                }
            }
            foreach (string reason in selected.Unsupported) Label(propertiesRoot, "Не подключено: " + reason, 11, Muted, Mathf.Clamp(30 + reason.Length / 45 * 15, 45, 105));
            Label(propertiesRoot, "Preview: изолированное проигрывание здесь не подключено; gameplay-компоненты не запускаются повторно.", 11, Muted, 45);
        }
        private string FavoriteKey(VisualProperty property) => selected.TypeKey + "|" + property.Id;
        private void BuildProperty(VisualProperty property)
        {
            var row = Vertical(property.Id, propertiesRoot, 2); row.gameObject.AddComponent<Image>().color = RowColor;
            var heading = Horizontal(row, 24);
            var star = Button(heading, favorites.Contains(FavoriteKey(property)) ? "*" : "+", () =>
            {
                string key = FavoriteKey(property); if (!favorites.Add(key)) favorites.Remove(key);
                PlayerPrefs.SetString(Pref + "Favorites", string.Join("\n", favorites)); PlayerPrefs.Save(); BuildProperties();
            }); Width(star.gameObject, 28);
            Text(heading, property.Label, 14, Color.white).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var reset = Button(heading, "Сброс", () => Safe(() => runtime.Reset(scope, selected, property.Id))); Width(reset.gameObject, 54);
            var controls = Horizontal(row, 25);
            bool logarithmic = property.Min >= 0 && property.Max >= 100 && !property.IsInteger;
            float Encode(float value) => logarithmic ? Mathf.Log10(1 + Mathf.Max(0, value)) : value;
            float Decode(float value) => logarithmic ? Mathf.Pow(10, value) - 1 : value;
            var slider = Slider(controls, Encode(property.Min), Encode(property.Max), Encode(property.Read()));
            bool gesture = false;
            void Begin() { if (!gesture) { runtime.BeginEdit(); gesture = true; } }
            var pointer = slider.gameObject.AddComponent<LiveVisualGesture>(); pointer.Begin = Begin; pointer.End = () => gesture = false;
            slider.onValueChanged.AddListener(value => { if (refreshing) return; Begin(); Safe(() => runtime.Edit(scope, selected, property, Decode(value))); if (!Input.GetMouseButton(0)) gesture = false; });
            var number = Field(controls, "", Number(property.Read()), text =>
            {
                if (refreshing) return;
                if (!float.TryParse(text, NumberStyles.Float, Invariant, out float value) || float.IsNaN(value) || float.IsInfinity(value)) { SetStatus("Введите конечное число через точку."); return; }
                Safe(() => { runtime.BeginEdit(); runtime.Edit(scope, selected, property, value); });
            }); Width(number.gameObject, 82);
            foreach (int sign in new[] { -1, 1 })
            {
                int direction = sign;
                var step = Button(controls, sign < 0 ? "-" : "+", () => Safe(() =>
                {
                    float precision = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? .1f : 1f;
                    float amount = property.IsInteger ? 1 : Mathf.Max(.0001f, Mathf.Min(.1f, Mathf.Abs(property.Read()) * .01f)) * precision;
                    runtime.BeginEdit(); runtime.Edit(scope, selected, property, property.Read() + direction * amount);
                })); Width(step.gameObject, 26);
            }
            var values = Label(row, "", 10, Muted, 28);
            var numericControls = controls.GetComponentsInChildren<Selectable>();
            valueRefresh.Add(() =>
            {
                if (!selected.IsAlive) return; float value = property.Read();
                bool available = property.Available?.Invoke() ?? true;
                foreach (var control in numericControls) control.interactable = available;
                if (!gesture) slider.SetValueWithoutNotify(Encode(value)); if (!number.isFocused) number.SetTextWithoutNotify(Number(value));
                bool has = runtime.TryOriginal(selected, property, out float original);
                values.text = $"Сейчас {Number(value)} · Current {Number(property.Current)} · Git {(has ? Number(original) : "нет → Current")} {(logarithmic ? "· log" : "")}";
            });
            if (!string.IsNullOrEmpty(property.Note)) Label(row, property.Note, 10, Muted, 35);
        }
        private void BuildColorTools(VisualProperty red)
        {
            string prefix = red.Id.Substring(0, red.Id.Length - 2);
            var channels = new[] { "r", "g", "b", "a" }.Select(c => selected.Properties.FirstOrDefault(p => p.Id == prefix + "." + c)).ToArray();
            if (channels.Take(3).Any(p => p == null)) return;
            Color Read() => new(channels[0].Read(), channels[1].Read(), channels[2].Read(), channels[3]?.Read() ?? 1);
            void Write(Color color) { for (int i = 0; i < channels.Length; i++) if (channels[i] != null) runtime.Edit(scope, selected, channels[i], color[i]); }
            var row = Horizontal(propertiesRoot, 28); Text(row, "HEX SDR", 11, Muted).gameObject.AddComponent<LayoutElement>().preferredWidth = 64;
            var hex = Field(row, "#RRGGBBAA", "#" + ColorUtility.ToHtmlStringRGBA(Read()), text => Safe(() =>
            { if (!ColorUtility.TryParseHtmlString(text.StartsWith("#", StringComparison.Ordinal) ? text : "#" + text, out Color color)) throw new ArgumentException("HEX: #RRGGBB или #RRGGBBAA."); runtime.BeginEdit(); Write(color); }));
            valueRefresh.Add(() => { if (!hex.isFocused) hex.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(Read())); });
            foreach (int item in new[] { 0, 1, 2 })
            {
                int channel = item; var hsvRow = Horizontal(propertiesRoot, 23);
                Text(hsvRow, new[] { "Hue 0–360°", "HSV S 0–1", "HSV V / HDR" }[channel], 11, Muted).gameObject.AddComponent<LayoutElement>().preferredWidth = 100;
                Color.RGBToHSV(Read(), out float h, out float s, out float v);
                var slider = Slider(hsvRow, 0, channel == 0 ? 360 : channel == 1 ? 1 : Mathf.Min(channels[0].Max, channels[1].Max, channels[2].Max), channel == 0 ? h * 360 : channel == 1 ? s : v);
                bool gesture = false; var pointer = slider.gameObject.AddComponent<LiveVisualGesture>(); pointer.Begin = () => { runtime.BeginEdit(); gesture = true; }; pointer.End = () => gesture = false;
                slider.onValueChanged.AddListener(value =>
                {
                    if (refreshing) return;
                    Safe(() => { if (!gesture) runtime.BeginEdit(); Color current = Read(); Color.RGBToHSV(current, out float hue, out float saturation, out float brightness);
                        var color = Color.HSVToRGB(channel == 0 ? value / 360 : hue, channel == 1 ? value : saturation, channel == 2 ? value : brightness, true); color.a = current.a; Write(color); });
                });
                valueRefresh.Add(() => { if (gesture) return; Color.RGBToHSV(Read(), out float hue, out float saturation, out float brightness); slider.SetValueWithoutNotify(channel == 0 ? hue * 360 : channel == 1 ? saturation : brightness); });
            }
        }

        private string ProfileName()
        {
            string name = presetName.text.Trim();
            if (name.Length == 0 || name.Length > 80 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..") throw new ArgumentException("Введите имя профиля без символов пути (до 80 знаков).");
            return name;
        }
        private static string ProfileDirectory
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath("Assets/_Project/Data/World/Presentation/Profiles");
#else
                return Path.Combine(Application.persistentDataPath, "VisualProfiles");
#endif
            }
        }
        private void SaveProfile(bool production)
        {
            string name = ProfileName(); Directory.CreateDirectory(ProfileDirectory);
            string path = Path.Combine(ProfileDirectory, name + ".json");
            string json = runtime.Export(name); File.WriteAllText(path, json);
            var verified = JsonUtility.FromJson<VisualProfile>(File.ReadAllText(path)); verified.Validate();
#if UNITY_EDITOR
            string assetPath = "Assets/_Project/Data/World/Presentation/Profiles/" + name + ".json";
            UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            if (production)
            {
                // Keep the approved production snapshot separate from editable named drafts.
                // Saving a draft later must not silently change an already approved release profile.
                assetPath = "Assets/_Project/Data/World/Presentation/LiveVisualProduction.json";
                path = Path.GetFullPath(assetPath);
                File.WriteAllText(path, json);
                UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                var text = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (text == null) throw new IOException("JSON записан, но Unity не импортировал TextAsset.");
                UnityEditor.Undo.RecordObject(runtime.Preset, "Apply Live Visual production profile");
                runtime.Preset.LiveProductionProfile = text; UnityEditor.EditorUtility.SetDirty(runtime.Preset); UnityEditor.AssetDatabase.SaveAssetIfDirty(runtime.Preset);
            }
            SetStatus((production ? "Production использует профиль: " : "JSON сохранён и прочитан: ") + path + "\nInstance-настройки исключены.");
#else
            SetStatus("JSON сохранён: " + path + (production ? "\nДля production перенесите JSON в Editor и нажмите «Применить»." : "") + "\nInstance-настройки исключены.");
#endif
        }
        private void LoadProfile() => LoadJson(File.ReadAllText(Path.Combine(ProfileDirectory, ProfileName() + ".json")));
        private void LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 8 * 1024 * 1024)
                throw new ArgumentException("JSON профиля пуст или превышает 8 MiB.");
            if (string.IsNullOrEmpty(json) || json.Length > 4 * 1024 * 1024) throw new ArgumentException("JSON пуст или превышает 4 MiB.");
            var profile = JsonUtility.FromJson<VisualProfile>(json); if (profile == null) throw new ArgumentException("Неверный JSON профиля.");
            runtime.Load(profile); RefreshValues(); SetStatus("Загружен профиль «" + profile.Name + "».");
        }
        private void Safe(Action action) { try { action(); } catch (Exception exception) { SetStatus(exception.Message); } }
        private void SetStatus(string text) { if (status != null) status.text = text; }
        private void Resize(float amount) { panel.sizeDelta = new Vector2(Mathf.Clamp(panel.sizeDelta.x + amount, 360, 720), panel.sizeDelta.y); Constrain(); SaveWindow(); }
        private void Constrain()
        {
            var rect = ((RectTransform)canvas.transform).rect;
            savedHeight = Mathf.Max(250, rect.height - 65); panel.sizeDelta = new Vector2(panel.sizeDelta.x, minimized ? 46 : savedHeight);
            panel.anchoredPosition = new Vector2(Mathf.Clamp(panel.anchoredPosition.x, 0, Mathf.Max(0, rect.width - panel.sizeDelta.x)), Mathf.Clamp(panel.anchoredPosition.y, -Mathf.Max(0, rect.height - panel.sizeDelta.y), 0));
        }
        private void SaveWindow()
        {
            if (panel == null) return;
            PlayerPrefs.SetFloat(Pref + "X", panel.anchoredPosition.x); PlayerPrefs.SetFloat(Pref + "Y", panel.anchoredPosition.y);
            PlayerPrefs.SetFloat(Pref + "Width", panel.sizeDelta.x); PlayerPrefs.SetFloat(Pref + "Alpha", opacity.alpha); PlayerPrefs.Save();
        }
        private void OnDestroy() { if (runtime != null) runtime.Changed -= RuntimeChanged; if (instance == this) instance = null; }
        private static string Number(float value) => value.ToString("0.####", Invariant);
        private static void Clear(Transform root) { foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        private static RectTransform Rect(string name, Transform parent) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static void Fill(RectTransform rect, float left = 0, float right = 0, float bottom = 0, float top = 0) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static void AnchorTopRight(RectTransform rect, float right, float top, float width, float height) { rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-right, -top); rect.sizeDelta = new Vector2(width, height); }
        private static RectTransform Vertical(string name, Transform parent, float spacing)
        {
            var rect = Rect(name, parent); var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = spacing; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; return rect;
        }
        private static RectTransform Horizontal(Transform parent, float height)
        {
            var rect = Rect("Row", parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 4; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; return rect;
        }
        private static TextMeshProUGUI Text(Transform parent, string value, float size, Color color)
        {
            var rect = Rect("Text", parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        private static TextMeshProUGUI Label(Transform parent, string value, float size, Color color, float height)
        {
            var text = Text(parent, value, size, color); text.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return text;
        }
        private static Button Button(Transform parent, string label, Action action)
        {
            var rect = Rect(label, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = Accent;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(() => action());
            var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 30; layout.flexibleWidth = 1;
            var caption = Text(rect, label, 12, Color.white); caption.alignment = TextAlignmentOptions.Center; Fill(caption.rectTransform, 4, 4); return button;
        }
        private static TMP_InputField Field(Transform parent, string placeholder, string value, Action<string> changed)
        {
            var rect = Rect("Input " + placeholder, parent); rect.gameObject.AddComponent<Image>().color = RowColor; var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 30; layout.flexibleWidth = 1;
            var input = rect.gameObject.AddComponent<TMP_InputField>(); input.characterLimit = 256;
            var viewport = Rect("Text Area", rect); Fill(viewport, 6, 6, 1, 1); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, value, 12, Color.white); Fill(text.rectTransform); text.textWrappingMode = TextWrappingModes.NoWrap;
            var hint = Text(viewport, placeholder, 12, Muted); Fill(hint.rectTransform);
            input.textViewport = viewport; input.textComponent = text; input.placeholder = hint; input.SetTextWithoutNotify(value);
            if (changed != null) input.onEndEdit.AddListener(edited => { if (input.isActiveAndEnabled) changed(edited); }); return input;
        }
        private static Slider Slider(Transform parent, float min, float max, float value)
        {
            var rect = Rect("Slider", parent); rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            rect.gameObject.AddComponent<Image>().color = new Color(.15f, .2f, .25f, 1);
            var slider = rect.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var fillArea = Rect("Fill Area", rect); Fill(fillArea, 5, 5, 8, 8);
            var fill = Rect("Fill", fillArea); Fill(fill); fill.gameObject.AddComponent<Image>().color = Accent;
            var handleArea = Rect("Handle Area", rect); Fill(handleArea, 5, 5);
            var handle = Rect("Handle", handleArea); handle.sizeDelta = new Vector2(10, 0); var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.72f, .9f, .96f);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage; slider.SetValueWithoutNotify(value); return slider;
        }
        private static void Width(GameObject obj, float width) { var layout = obj.GetComponent<LayoutElement>(); layout.preferredWidth = width; layout.flexibleWidth = 0; }
    }
}
#endif
