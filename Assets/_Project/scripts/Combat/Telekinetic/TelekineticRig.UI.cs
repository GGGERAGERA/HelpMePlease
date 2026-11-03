using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticRig
    {
        private const float UiWidth = 1600, UiHeight = 900, CellSize = 62, FootprintInset = 7;
        private static readonly Vector2 BoardCenter = new(800, 441);
        private static readonly Rect PreviewBounds = Inset(AreaRect(TelekineticBuild.Radius), -10);
        private static readonly Rect WeaponPanel = new(78, 120, 270, 612);
        private static readonly Rect ModulePanel = new(1202, 120, 320, 612);
        private static readonly Color Cyan = new(.35f, .88f, 1);
        private static readonly Color Good = new(.31f, 1, .71f);
        private static readonly Color Bad = new(1, .34f, .32f);
        private static readonly Color AreaBorder = new(.26f, .68f, .77f);
        private static readonly Color OpenTile = new(.052f, .094f, .127f);
        private static readonly Color OpenGrid = new(.116f, .203f, .257f);
        private static readonly Color FogTile = new(.037f, .068f, .094f);
        private static readonly Color FogGrid = new(.084f, .149f, .190f);
        private const float AreaRevealSeconds = .32f;

        private readonly Dictionary<Vector2Int, TelekineticItem> occupiedCells = new();
        private readonly HashSet<Vector2Int> frontier = new();
        private readonly Dictionary<Vector2Int, float> revealedAt = new();
        private GUIStyle smallStyle, nameStyle, actionStyle, tooltipStyle;
        private Vector2 weaponScroll, moduleScroll, pickupPoint, heldPointer;
        private Vector2Int heldOffset;
        private int heldTurns;
        private bool moving, canStowHeld;
        private TelekineticItem hovered, flashed;
        private float flashAt, rejectedAt = -10;
        private int drawnAreaRadius, previousAreaRadius;
        private float areaRevealedAt;
        private string rejection;
        private float UiScale => Mathf.Min(Screen.width / UiWidth, Screen.height / UiHeight);
        public static Color PartColor(TelekineticPart kind) => kind switch
        {
            TelekineticPart.Frame=>new(.33f,.46f,.52f), TelekineticPart.DMG=>new(1,.38f,.3f), TelekineticPart.FR=>new(1,.74f,.18f),
            TelekineticPart.Double=>new(.35f,.64f,1), TelekineticPart.RIC=>new(.2f,.92f,.78f), TelekineticPart.Energy=>new(.74f,.5f,1),
            TelekineticPart.Pierce=>new(1,.65f,.3f),TelekineticPart.Splash=>new(1,.43f,.15f),TelekineticPart.Chain=>new(.64f,.55f,1),
            TelekineticPart.Crit=>new(1,.35f,.7f),TelekineticPart.Slow=>new(.5f,.85f,1),TelekineticPart.Shred=>new(.68f,.9f,.4f),
            TelekineticPart.Knock=>new(1,.8f,.45f),TelekineticPart.Echo=>new(.55f,.65f,1),_=>new(.4f,.9f,1)
        };

        private void EnsureStyles()
        {
            if (smallStyle != null) return;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            smallStyle.normal.textColor = new Color(.61f, .7f, .76f);
            nameStyle = new GUIStyle(smallStyle) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            nameStyle.normal.textColor = new Color(.89f, .94f, .98f);
            actionStyle = new GUIStyle(nameStyle) { alignment = TextAnchor.MiddleCenter };
            tooltipStyle = new GUIStyle(nameStyle) { wordWrap = true, fontSize = 16, alignment = TextAnchor.UpperLeft };
        }

        private void OnGUI()
        {
            if (!ready || station.IsRunEnded || station.Owner.IsDead || SceneTransitionOverlay.IsTransitioning) return;
            EnsureStyles();
            var matrix = GUI.matrix;
            float scale = UiScale;
            Vector2 origin = new((Screen.width - UiWidth * scale) * .5f, (Screen.height - UiHeight * scale) * .5f);
            GUI.matrix = Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(scale, scale, 1));
            if (Editing && CanEdit) DrawConstructor();
            else if (!Editing && Time.timeScale > 0) DrawCombatHud();
            GUI.matrix = matrix;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) held = null;
        }

        private void ResetBoardFeedback()
        {
            revealedAt.Clear();
            flashed = null;
            rejectedAt = -10;
        }

        private void DrawConstructor()
        {
            Event e = Event.current;
            Vector2 mouse = e.mousePosition;
            hovered = null;
            if (held != null && e.type == EventType.MouseDrag)
            {
                heldPointer = mouse;
                moving |= (mouse - pickupPoint).sqrMagnitude >= 16;
            }

            RefreshBoard();
            bool onBoard = PointerCell(mouse, out var cell);
            if (onBoard) occupiedCells.TryGetValue(cell, out hovered);
            HandleConstructorInput(e, cell, onBoard);
            if (!Editing) return;

            DrawBoard();
            DrawStock(WeaponPanel, true, ref weaponScroll);
            DrawStock(ModulePanel, false, ref moduleScroll);
            DrawActions();

            if (held != null && moving)
            {
                PointerCell(heldPointer, out var heldCell);
                DrawHeld(heldPointer, heldCell);
            }
            else if (held == null && hovered != null) DrawTooltip(mouse, hovered);
        }

        private void RefreshBoard()
        {
            occupiedCells.Clear();
            frontier.Clear();
            occupiedCells.Add(Vector2Int.zero, null);
            foreach (var item in Build.Items)
                for (int i = 0; i < item.CellCount; i++)
                    occupiedCells.Add(item.Cell(i), item);

            foreach (var p in occupiedCells.Keys)
                foreach (var side in TelekineticBuild.Sides)
                {
                    var adjacent = p + side;
                    if (InsideUnlocked(adjacent) && !occupiedCells.ContainsKey(adjacent)) frontier.Add(adjacent);
                }

            foreach (var p in frontier)
                if (!revealedAt.ContainsKey(p)) revealedAt[p] = Time.unscaledTime;
        }

        private static int CellRing(Vector2Int cell) => Mathf.Max(Mathf.Abs(cell.x), Mathf.Abs(cell.y));
        private bool InsideUnlocked(Vector2Int cell) => CellRing(cell) <= Build.AvailableRadius;

        private static Rect AreaRect(int radius)
        {
            float size = (radius * 2 + 1) * CellSize;
            return new Rect(BoardCenter - Vector2.one * (size * .5f), Vector2.one * size);
        }

        private void DrawArea(float now)
        {
            if (Event.current.type != EventType.Repaint) return;

            int active = Build.AvailableRadius;
            if (active != drawnAreaRadius)
            {
                previousAreaRadius = active > drawnAreaRadius ? drawnAreaRadius : active;
                drawnAreaRadius = active;
                areaRevealedAt = now;
            }

            float reveal = Mathf.Clamp01((now - areaRevealedAt) / AreaRevealSeconds);
            int fog = Mathf.Min(active + 1, TelekineticBuild.Radius);
            for (int y = -fog; y <= fog; y++)
            for (int x = -fog; x <= fog; x++)
            {
                var cell = new Vector2Int(x, y);
                int ring = CellRing(cell);
                Rect rect = Inset(CellRect(cell), 1);
                if (ring <= active)
                {
                    float light = ring <= previousAreaRadius ? 1 : reveal;
                    Fill(rect, Color.Lerp(FogTile, OpenTile, light));
                    Border(rect, Color.Lerp(FogGrid, OpenGrid, light), 1);
                }
                else
                {
                    Fill(rect, Color.Lerp(Color.clear, FogTile, reveal));
                    Border(rect, Color.Lerp(Color.clear, FogGrid, reveal), 1);
                }
            }

            // Always mark the actual placement limit, even during the reveal.
            Border(AreaRect(active), AreaBorder, 2);
        }

        private void DrawBoard()
        {
            float now = Time.unscaledTime;
            DrawArea(now);
            foreach (var p in frontier)
            {
                float reveal = 1 - Mathf.Clamp01((now - revealedAt[p]) * 4f);
                Rect rect = Inset(CellRect(p), 2);
                Fill(rect, Color.Lerp(new Color(.045f, .078f, .106f), new Color(.09f, .31f, .35f), reveal));
                Border(rect, Color.Lerp(new Color(.115f, .22f, .28f), Cyan, reveal), 1);
                Fill(new Rect(rect.center - Vector2.one, Vector2.one * 2), new Color(.28f, .47f, .54f));
            }
            // A cell can leave and re-enter the frontier after moving or stowing a part.
            foreach (var p in occupiedCells.Keys) revealedAt.Remove(p);

            Rect core = Inset(CellRect(Vector2Int.zero), FootprintInset);
            Color coreColor = Color.Lerp(new Color(.11f, .54f, .67f), Cyan, .35f + .25f * Mathf.Sin(now * 3));
            Fill(core, new Color(.057f, .195f, .235f));
            Border(core, coreColor, 2);
            Border(Inset(core, 12), coreColor, 1);
            Fill(Inset(core, 22), coreColor);

            foreach (var item in Build.Items)
            {
                bool lifted = held == item && moving;
                float flash = item == flashed ? 1 - Mathf.Clamp01((now - flashAt) * 3) : 0;
                Color outline = item == selected ? Good : item == hovered ? Cyan : FrameTint(item.Kind);
                Color fill = Color.Lerp(PlateTint(item.Kind), Good, flash * .52f);
                outline = Color.Lerp(outline, Good, flash);
                if (now - rejectedAt < .45f && HasDisconnectedCell(item)) outline = Bad;
                if (lifted) { outline.a *= .32f; fill.a *= .45f; }
                DrawFootprint(item, fill, outline, item == selected ? 3 : item.IsWeapon ? 2 : 1);

                if (lifted) continue;
                Vector2 point = ToUi(item.Kind == TelekineticPart.RIC ? item.Position : item.Center);
                DrawOrientedIcon(point, item.IsWeapon ? 46 : 33, item);
                if (item.Level > 1)
                {
                    Rect r = CellRect(item.Position);
                    GUI.Label(new Rect(r.x + 4, r.y + 2, 22, 20), TelekineticBuild.Roman(item.Level), smallStyle);
                }
            }

            foreach (var pair in occupiedCells)
            {
                DrawConnection(pair.Key, pair.Value, Vector2Int.right);
                DrawConnection(pair.Key, pair.Value, Vector2Int.up);
            }
        }

        private bool HasDisconnectedCell(TelekineticItem item)
        {
            for (int i = 0; i < item.CellCount; i++)
                if (Build.Disconnected.Contains(item.Cell(i))) return true;
            return false;
        }

        private void DrawConnection(Vector2Int p, TelekineticItem item, Vector2Int side)
        {
            if (!occupiedCells.TryGetValue(p + side, out var next) || item == next) return;
            if (moving && (held == item && item != null || held == next && next != null)) return;
            bool active = item != null && next != null &&
                (item.IsModule && (next.Kind == TelekineticPart.Energy || Affects(item.Kind, next)) ||
                 next.IsModule && (item.Kind == TelekineticPart.Energy || Affects(next.Kind, item)));
            bool focus = active && (item == (hovered ?? selected) || next == (hovered ?? selected));
            Vector2 axis = new(side.x, -side.y);
            Vector2 center = ToUi(p) + axis * (CellSize * .5f);
            Color color = focus ? Good : active ? new Color(.18f, .63f, .65f) : new Color(.18f, .32f, .39f);
            Vector2 end = axis * (FootprintInset - 1);
            Link(center - end, center + end, color, focus ? 3 : 2);
        }

        private void DrawStock(Rect panel, bool weapons, ref Vector2 scroll)
        {
            if (held != null && moving && !heldFromReserve && held.IsWeapon == weapons)
            {
                bool overPanel = panel.Contains(heldPointer);
                Color tint = canStowHeld ? Good : Bad;
                if (overPanel || canStowHeld)
                {
                    tint.a = overPanel ? .13f : .04f;
                    Fill(panel, tint);
                    tint.a = overPanel ? 1f : .35f;
                    Border(panel, tint, overPanel ? 2 : 1);
                }
            }

            Rect viewport = Rect.MinMaxRect(panel.xMin + 10, panel.yMin + 51, panel.xMax - 10, panel.yMax - 13);
            const float stride = 56;
            int count = 0;
            foreach (var item in Build.Reserve)
                if (item.IsWeapon == weapons) count++;

            var e = Event.current;
            bool inside = viewport.Contains(e.mousePosition);
            float rowWidth = viewport.width - 18;
            scroll = GUI.BeginScrollView(viewport, scroll,
                new Rect(0, 0, rowWidth, Mathf.Max(viewport.height - 1, count * stride)));

            int index = 0;
            foreach (var item in Build.Reserve)
            {
                if (item.IsWeapon != weapons) continue;
                Rect row = new(0, index++ * stride, rowWidth, 49);
                // Keep the scroll position and list order stable while the item is in hand.
                if (held == item) continue;
                bool over = inside && held == null && row.Contains(e.mousePosition);
                Fill(row, over ? new Color(.096f, .17f, .216f) :
                    weapons ? new Color(.055f, .115f, .15f) : new Color(.069f, .084f, .105f));
                Border(row, over ? Cyan : FrameTint(item.Kind), 1);
                DrawIcon(new Rect(row.x + 10, row.y + 8, 34, 34), item.Kind);
                GUI.Label(new Rect(row.x + 53, row.y, row.width - 58, row.height),
                    StockName(item.Kind) + (item.Level > 1 ? " " + TelekineticBuild.Roman(item.Level) : ""), nameStyle);
                if (!over) continue;
                hovered = item;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    BeginHold(item, item.Position, true, e.mousePosition + viewport.position - scroll);
                    e.Use();
                }
            }

            if (count == 0)
                GUI.Label(new Rect(0, 0, rowWidth, 50), "Пусто", smallStyle);
            GUI.EndScrollView();
        }

        private void HandleConstructorInput(Event e, Vector2Int cell, bool onBoard)
        {
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && onBoard && hovered != null:
                    if (merging)
                    {
                        bool merged = Merge(selected, hovered);
                        Feedback(selected, merged);
                        if (merged) merging = false;
                    }
                    else BeginHold(hovered, cell, false, e.mousePosition);
                    e.Use();
                    break;

                case EventType.MouseUp when e.button == 0 && held != null:
                    FinishHold(e.mousePosition, cell, onBoard);
                    e.Use();
                    break;

                case EventType.ScrollWheel when onBoard && held == null && selected?.IsWeapon == true:
                    if (TurnWeapon(selected, e.delta.y > 0 ? -1 : 1)) Flash(selected);
                    e.Use();
                    break;

                case EventType.KeyDown:
                    switch (e.keyCode)
                    {
                        case KeyCode.R:
                            if (held != null)
                            {
                                heldTurns = (heldTurns + 1) % 4;
                                heldOffset = TelekineticItem.RotateOffset(heldOffset, 1);
                                moving = true;
                            }
                            else if (selected != null) Feedback(selected, Rotate(selected));
                            break;
                        case KeyCode.Delete: StowSelected(); break;
                        case KeyCode.M: ToggleMerge(); break;
                        case KeyCode.F: AutoRotate = !AutoRotate; break;
                        default: return;
                    }
                    e.Use();
                    break;
            }
        }

        private void BeginHold(TelekineticItem item, Vector2Int cell, bool fromReserve, Vector2 mouse)
        {
            held = item;
            selected = fromReserve ? null : item;
            heldFromReserve = fromReserve;
            canStowHeld = !fromReserve && Build.CanRemove(item);
            heldOffset = cell - item.Position;
            heldTurns = item.QuarterTurns;
            pickupPoint = heldPointer = mouse;
            rejectedAt = -10;
            moving = fromReserve;
            merging = false;
            Build.Disconnected.Clear();
        }

        private void FinishHold(Vector2 mouse, Vector2Int cell, bool onBoard)
        {
            var item = held;
            held = null;
            if (!moving && (mouse - pickupPoint).sqrMagnitude < 16) return;
            if (onBoard)
            {
                Vector2Int anchor = cell - heldOffset;
                if (!heldFromReserve && item.Position == anchor && item.QuarterTurns == heldTurns) return;
                bool placed = heldFromReserve ? Install(item, anchor, heldTurns) : Move(item, anchor, heldTurns);
                Feedback(item, placed);
                if (placed) selected = item;
            }
            else if (!heldFromReserve && (item.IsWeapon ? WeaponPanel : ModulePanel).Contains(mouse))
                StowSelected();
        }

        private void DrawHeld(Vector2 mouse, Vector2Int cell)
        {
            if (PreviewBounds.Contains(mouse))
            {
                Vector2Int anchor = cell - heldOffset;
                var preview = new TelekineticItem(0, held.Kind, anchor, heldTurns);
                bool crossesBoundary = false;
                for (int i = 0; i < preview.CellCount; i++)
                    crossesBoundary |= !InsideUnlocked(preview.Cell(i));

                if (crossesBoundary)
                {
                    // The placement as a whole is invalid. Mark the in-bounds part in cyan, and
                    // show each blocked cell on the other side of the perimeter in red.
                    DrawFootprint(preview, new Color(Cyan.r, Cyan.g, Cyan.b, .16f), Cyan, 2);
                    for (int i = 0; i < preview.CellCount; i++)
                    {
                        var p = preview.Cell(i);
                        if (!InsideUnlocked(p)) DrawOutsidePreviewCell(p);
                    }
                }
                else
                {
                    bool valid = heldFromReserve ? Build.CanPlace(held.Kind, anchor, heldTurns) :
                        Build.CanMove(held, anchor, heldTurns);
                    Color border = valid ? Good : Bad;
                    Color fill = border;
                    fill.a = .21f + Mathf.Sin(Time.unscaledTime * 7) * .04f;
                    DrawFootprint(preview, fill, border, 2);
                }
            }

            Vector2 point = new(Mathf.Min(mouse.x + 25, UiWidth - 24), Mathf.Min(mouse.y + 25, UiHeight - 24));
            Rect hand = new(point - Vector2.one * 21, Vector2.one * 42);
            Fill(hand, new Color(.035f, .075f, .104f, .95f));
            Border(hand, held.IsWeapon ? Cyan : PartColor(held.Kind), 1);
            DrawIcon(Inset(hand, 4), held.Kind);
        }

        private static void DrawOutsidePreviewCell(Vector2Int cell)
        {
            // At maximum expansion, keep a visible red strip for off-grid cells
            // without drawing over the constructor header or footer.
            Rect tile = Inset(CellRect(cell), 3);
            Rect rect = Rect.MinMaxRect(Mathf.Max(PreviewBounds.xMin, tile.xMin), Mathf.Max(PreviewBounds.yMin, tile.yMin),
                Mathf.Min(PreviewBounds.xMax, tile.xMax), Mathf.Min(PreviewBounds.yMax, tile.yMax));
            if (rect.width < 4 || rect.height < 4) return;

            Fill(rect, new Color(Bad.r, Bad.g, Bad.b, .28f));
            Border(rect, Bad, 2);
            if (rect.width < 18 || rect.height < 18) return;
            Vector2 diagonal = new(7, 7);
            Vector2 opposite = new(7, -7);
            Link(rect.center - diagonal, rect.center + diagonal, Bad, 2);
            Link(rect.center - opposite, rect.center + opposite, Bad, 2);
        }

        private void DrawActions()
        {
            if (Action(new Rect(1380, 45, 132, 42), "Закрыть", true))
            {
                station.Interaction.CloseConstructor();
                return;
            }

            if (Action(new Rect(88, 761, 218, 38), AutoRotate ? "Автоповорот: вкл." : "Автоповорот: выкл.", true))
                AutoRotate = !AutoRotate;

            if (selected != null && held == null && selected.CanMerge && selected.Level < 3 &&
                Action(new Rect(1364, 761, 148, 38), merging ? "Выберите пару" : "Объединить", merging))
                ToggleMerge();

            if (Time.unscaledTime - rejectedAt < 2.4f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1, .62f, .59f);
                GUI.Label(new Rect(340, 744, 840, 30), rejection, smallStyle);
                GUI.color = old;
            }
        }

        private bool Action(Rect rect, string label, bool accent)
        {
            bool over = rect.Contains(Event.current.mousePosition) && held == null;
            Fill(rect, over ? new Color(.11f, .24f, .3f) : new Color(.057f, .092f, .119f));
            Border(rect, over || accent ? new Color(.24f, .52f, .63f) : new Color(.23f, .31f, .36f), 1);
            GUI.Label(rect, label, actionStyle);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void ToggleMerge() => merging = !merging && selected != null && selected.CanMerge && selected.Level < 3;

        private void StowSelected()
        {
            if (selected == null || held != null) return;
            if (Remove(selected))
            {
                Feedback(selected, true);
                CancelSelection();
            }
            else Feedback(selected, false);
        }

        private void Feedback(TelekineticItem item, bool changed)
        {
            if (changed)
            {
                revealedAt.Clear();
                foreach (var p in frontier) revealedAt[p] = Time.unscaledTime - 1;
                Flash(item);
                AudioService.Instance?.Play(AudioCueId.UIConfirm);
            }
            else
            {
                rejectedAt = Time.unscaledTime;
                rejection = Build.Message;
            }
        }

        private void Flash(TelekineticItem item)
        {
            flashed = item;
            flashAt = Time.unscaledTime;
            rejectedAt = -10;
        }

        private void DrawTooltip(Vector2 mouse, TelekineticItem item)
        {
            string name = StockName(item.Kind) + (item.CanMerge ? " " + TelekineticBuild.Roman(item.Level) : "");
            string effect = CompactEffect(item);
            const float width = 256;
            float height = 40 + tooltipStyle.CalcHeight(new GUIContent(effect), width - 24);
            Rect rect = new(Mathf.Clamp(mouse.x + 20, 12, UiWidth - width - 12),
                Mathf.Clamp(mouse.y + 20, 12, UiHeight - height - 12), width, height);
            Fill(rect, new Color(.038f, .061f, .082f));
            Border(rect, new Color(.24f, .45f, .52f), 1);
            GUI.Label(new Rect(rect.x + 12, rect.y + 4, width - 24, 28), name, nameStyle);
            GUI.Label(new Rect(rect.x + 12, rect.y + 32, width - 24, height - 32), effect, tooltipStyle);
        }

        private static string StockName(TelekineticPart kind) => kind switch
        {
            TelekineticPart.Pistol => "Пистолет",
            TelekineticPart.Shotgun => "Дробовик",
            TelekineticPart.Sniper => "Снайперка",
            TelekineticPart.Saw => "Пила",
            _ => TelekineticBuild.Label(kind)
        };

        private string CompactEffect(TelekineticItem item)
        {
            float strength = Modules.Strength(item);
            return item.Kind switch
            {
                TelekineticPart.Pistol => "Одиночные выстрелы",
                TelekineticPart.Shotgun => "Выстрел дробью",
                TelekineticPart.Sniper => "Дальнобойный выстрел",
                TelekineticPart.Saw => "Урон при контакте",
                TelekineticPart.DMG => $"Урон следующей атаки +{(Modules.DamageMultiplier - 1) * strength:P0}",
                TelekineticPart.FR => $"Частота атак +{(Modules.RapidMultiplier - 1) * strength:P0} на {Modules.RapidDuration:0.#} с",
                TelekineticPart.Double => "Дополнительная атака",
                TelekineticPart.RIC => $"Рикошеты +{item.Level}",
                TelekineticPart.Energy => "Ускоряет соседние модули",
                TelekineticPart.Pierce => "Снаряд пробивает врагов",
                TelekineticPart.Splash => "Урон по области",
                TelekineticPart.Chain => "Поражает дополнительную цель",
                TelekineticPart.Crit => $"Следующая атака ×{1 + (Modules.CritMultiplier - 1) * strength:0.##} урона",
                TelekineticPart.Slow => "Замедляет цель",
                TelekineticPart.Shred => "Атака обходит щиты",
                TelekineticPart.Knock => "Усиливает отталкивание",
                TelekineticPart.Echo => "Повторяет атаку с задержкой",
                _ => "Соединяет части конструкции"
            };
        }

        private static Vector2 ToUi(Vector2 cell) => BoardCenter + new Vector2(cell.x, -cell.y) * CellSize;
        private static Rect CellRect(Vector2Int p) => new(ToUi(p) - Vector2.one * (CellSize * .5f), Vector2.one * CellSize);

        private bool PointerCell(Vector2 mouse, out Vector2Int p)
        {
            Vector2 delta = (mouse - BoardCenter) / CellSize;
            p = new Vector2Int(Mathf.FloorToInt(delta.x + .5f), -Mathf.FloorToInt(delta.y + .5f));
            return InsideUnlocked(p);
        }

        private void DrawOrientedIcon(Vector2 point, float size, TelekineticItem item)
        {
            var matrix = GUI.matrix;
            if (item.IsWeapon) GUIUtility.RotateAroundPivot(-item.Heading, point);
            DrawIcon(new Rect(point - Vector2.one * (size * .5f), Vector2.one * size), item.Kind);
            GUI.matrix = matrix;
        }

        private void DrawFootprint(TelekineticItem item, Color fill, Color border, float width)
        {
            for (int i = 0; i < item.CellCount; i++)
            {
                var cell = item.Cell(i);
                if (!InsideUnlocked(cell)) continue;
                var rect = CellRect(cell);
                bool up = !item.Contains(cell + Vector2Int.up), down = !item.Contains(cell + Vector2Int.down);
                bool left = !item.Contains(cell + Vector2Int.left), right = !item.Contains(cell + Vector2Int.right);
                if (up) rect.yMin += FootprintInset;
                if (down) rect.yMax -= FootprintInset;
                if (left) rect.xMin += FootprintInset;
                if (right) rect.xMax -= FootprintInset;
                Fill(rect, fill);
                if (up) Fill(new Rect(rect.x, rect.y, rect.width, width), border);
                if (down) Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), border);
                if (left) Fill(new Rect(rect.x, rect.y, width, rect.height), border);
                if (right) Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), border);
            }
        }

        private static Rect Inset(Rect rect, float inset) => new(rect.position + Vector2.one * inset, rect.size - Vector2.one * (inset * 2));

        private static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Border(Rect rect, Color color, float width)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private static void Link(Vector2 a, Vector2 b, Color color, float width)
        {
            var matrix = GUI.matrix;
            var delta = b - a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y - width * .5f, delta.magnitude, width), color);
            GUI.matrix = matrix;
        }

        private void DrawCombatHud()
        {
            var camera=station.InputOwner.PointerCamera;
            if (camera!=null && (GlitchWarning || Suppressed))
                foreach (var item in Build.Items)
                {
                    if (!GlitchWarning && !item.IsModule && item.Kind!=TelekineticPart.Energy) continue;
                    Vector3 screen=camera.WorldToScreenPoint(views[item.Id].transform.position);
                    Vector2 point=new(screen.x/UiScale,(Screen.height-screen.y)/UiScale);
                    Color color=GlitchWarning ? new Color(1,.25f,.9f) : Color.red;
                    if (GlitchWarning)
                    {
                        float alpha=.5f+.5f*Mathf.Sin(Time.time*18); color.a=alpha;
                        Link(point+new Vector2(-18,-18),point+new Vector2(18,-18),color,3);
                        Link(point+new Vector2(-18,18),point+new Vector2(18,18),color,3);
                    }
                    else
                    {
                        Link(point+new Vector2(-10,-10),point+new Vector2(10,10),color,3);
                        Link(point+new Vector2(-10,10),point+new Vector2(10,-10),color,3);
                    }
                }
            GUI.Label(new Rect(36,705,650,28),GlitchWarning?"⚠ GLITCH: ПЕРЕСТАНОВКА":Suppressed?"МОДУЛИ ПОДАВЛЕНЫ":ActiveAnomaly?.ToString()??"",smallStyle);
        }
    }
}
