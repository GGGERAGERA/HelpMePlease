using System;
using System.Linq;
using UnityEngine;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticRig
    {
        private const float UiWidth=1600, UiHeight=900, GridX=386, GridY=52, CellSize=92;
        private GUIStyle textStyle, smallStyle, buttonStyle, stockNameStyle, headerStyle;
        private Vector2 weaponScroll, moduleScroll;
        private string hoverText;
        private bool reserveDragging;
        private static readonly TelekineticBuild TooltipReserveContext=new();
        private int brushTurns,dragTurns;
        private Vector2Int dragOffset;
        private float UiScale=>Mathf.Min(Screen.width/UiWidth,Screen.height/UiHeight);
        private bool PointerOverPanel() => Input.mousePosition.y<72*UiScale || Input.mousePosition.y>Screen.height-105*UiScale;
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
            if (textStyle!=null) return;
            textStyle=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true}; textStyle.normal.textColor=new Color(.8f,.87f,.91f);
            smallStyle=new GUIStyle(textStyle){fontSize=13};
            stockNameStyle=new GUIStyle(textStyle){fontSize=17,wordWrap=false,alignment=TextAnchor.MiddleLeft};
            headerStyle=new GUIStyle(textStyle){fontSize=22,fontStyle=FontStyle.Bold,wordWrap=false,alignment=TextAnchor.MiddleCenter};
            buttonStyle=new GUIStyle(GUI.skin.button){fontSize=16,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(14,8,4,4)};
        }
        private static void Fill(Rect rect,Color color)
        { var old=GUI.color; GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old; }
        private void Text(float x,float y,float w,float h,string value,bool small=false) => GUI.Label(new Rect(x,y,w,h),value,small?smallStyle:textStyle);
        private bool Button(float x,float y,float w,float h,string label) => GUI.Button(new Rect(x,y,w,h),label,buttonStyle);
        private static Rect CellRect(Vector2Int p)=>new(GridX+(p.x+4)*CellSize,GridY+(4-p.y)*CellSize,CellSize-3,CellSize-3);
        private static Vector2 Center(Vector2Int p)=>CellRect(p).center;
        private static void Link(Vector2 a,Vector2 b,Color color,float width)
        {
            var matrix=GUI.matrix; var delta=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            Fill(new Rect(a.x,a.y-width*.5f,delta.magnitude,width),color); GUI.matrix=matrix;
        }
        private bool ActiveLink(TelekineticItem a,TelekineticItem b) =>
            a!=null && b!=null && (a.Kind==TelekineticPart.Energy && b.IsModule || b.Kind==TelekineticPart.Energy && a.IsModule ||
            a.IsModule && Affects(a.Kind,b) || b.IsModule && Affects(b.Kind,a));
        private bool SelectedLink(TelekineticItem a,TelekineticItem b) => ActiveLink(a,b) &&
            (selected==a || selected==b || selected!=null && selected.Kind==TelekineticPart.Energy &&
            (a.IsModule && TelekineticBuild.Adjacent(selected,a) || b.IsModule && TelekineticBuild.Adjacent(selected,b)));
        private bool AffectedWeapon(TelekineticItem c) => c.IsWeapon && selected!=null &&
            (c==selected || ActiveLink(c,selected) && TelekineticBuild.Adjacent(c,selected) ||
            selected.Kind==TelekineticPart.Energy && Build.Neighbours(selected).Any(m=>m.IsModule && ActiveLink(c,m) && TelekineticBuild.Adjacent(c,m)));
        private void OnGUI()
        {
            if (!ready || station.IsRunEnded || station.Owner.IsDead || SceneTransitionOverlay.IsTransitioning) return;
            EnsureStyles(); var old=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(new Vector3(UiScale,UiScale,1));
            if (Editing && CanEdit) DrawConstructor(); else if (!Editing && Time.timeScale>0) DrawCombatHud();
            if (Editing && CanEdit && !string.IsNullOrEmpty(hoverText))
            {
                var mouse=Event.current.mousePosition;
                float height=smallStyle.CalcHeight(new GUIContent(hoverText),264)+16;
                var rect=new Rect(Mathf.Clamp(mouse.x+18,8,UiWidth-288),Mathf.Clamp(mouse.y+18,8,UiHeight-height-8),280,height);
                Fill(rect,new Color(.025f,.035f,.05f,1)); GUI.Label(new Rect(rect.x+8,rect.y+8,264,height-16),hoverText,smallStyle);
            }
            GUI.matrix=old;
        }
        private void DrawConstructor()
        {
            hoverText=null;
            if (reserveSelection==null || !brush.HasValue) reserveDragging=false;
            Fill(new Rect(0,0,UiWidth,UiHeight),new Color(.025f,.04f,.06f,1));
            HandleConstructorKeys(Event.current);
            GUI.Label(new Rect(GridX,12,CellSize*9,32),"Конструктор силы",headerStyle);
            DrawStockColumn(new Rect(20,8,280,872),true,ref weaponScroll);
            DrawStockColumn(new Rect(1300,8,280,872),false,ref moduleScroll);
            for (int y=-4;y<=4;y++) for (int x=-4;x<=4;x++)
            {
                var p=new Vector2Int(x,y); var rect=CellRect(p); var c=Build.At(p);
                bool available=brush.HasValue && c==null && Build.CanPlace(brush??TelekineticPart.Frame,p,brush.HasValue?brushTurns:0);
                Color color=p==Vector2Int.zero ? new Color(.12f,.42f,.51f) : available ? new Color(.065f,.19f,.16f) : new Color(.055f,.075f,.095f);
                if (Build.Disconnected.Contains(p)) color=new Color(.65f,.12f,.13f);
                if (Mathf.Abs(x)>Build.AvailableRadius || Mathf.Abs(y)>Build.AvailableRadius) color=new Color(.02f,.025f,.03f);
                Fill(rect,color);
                if (c!=null && new Rect(rect.x,rect.y,CellSize,CellSize).Contains(Event.current.mousePosition)) hoverText=ItemTooltip(c);
                if (p==Vector2Int.zero)
                {
                    Fill(new Rect(rect.center.x-10,rect.center.y-10,20,20),FrameTint(TelekineticPart.Pistol));
                    Fill(new Rect(rect.center.x-6,rect.center.y-6,12,12),new Color(.05f,.12f,.17f));
                }
                HandleCell(p,c,new Rect(rect.x,rect.y,CellSize,CellSize),Event.current);
            }
            foreach (var c in Build.Items)
            {
                bool problem=Build.Disconnected.Any(c.Contains);
                Color fill=problem ? new Color(.55f,.12f,.12f) : PlateTint(c.Kind);
                DrawFootprint(c,fill,problem?Color.red:selected==c?new Color(.8f,.93f,1):FrameTint(c.Kind),selected==c?3:c.IsWeapon?2.5f:1.5f);
            }
            foreach (var c in Build.Items) for (int i=0;i<c.CellCount;i++)
            {
                Vector2Int p=c.Cell(i);
                void Edge(Vector2Int target,bool active)
                {
                    Vector2 a=Center(p),b=Center(target),axis=(b-a).normalized;
                    Link(a+axis*(CellSize*.36f),b-axis*(CellSize*.36f),active?new Color(.3f,1,.85f):new Color(.3f,.43f,.51f),active?4:2);
                }
                foreach (var side in TelekineticBuild.Sides)
                {
                    var target=p+side; var n=Build.At(target);
                    if (target==Vector2Int.zero) Edge(target,false);
                    else if (n!=null && n!=c && n.Id>c.Id) Edge(target,SelectedLink(c,n));
                }
            }
            foreach (var c in Build.Items)
            {
                Vector2 center=c.Kind==TelekineticPart.RIC?Center(c.Position):UiCenter(c.Center);
                DrawIcon(new Rect(center.x-28,center.y-28,56,56),c.Kind);
                var anchor=CellRect(c.Position);
                Fill(new Rect(anchor.x+5,anchor.y+5,4,4),Color.white);
            }
            DrawPlacementPreview();
            HandleReserveDrop(Event.current);
            if (dragging!=null && Event.current.type==EventType.MouseUp) { dragging=null; Event.current.Use(); }
        }
        private void DrawStockColumn(Rect panel,bool weapons,ref Vector2 scroll)
        {
            Fill(panel,new Color(.055f,.075f,.1f));
            GUI.Label(new Rect(panel.x+8,panel.y+4,panel.width-16,32),weapons?"Оружия":"Модули",headerStyle);
            var stock=Build.Reserve.Where(item=>item.IsWeapon==weapons).ToArray();
            var viewport=new Rect(panel.x+8,panel.y+44,panel.width-16,panel.height-52);
            bool inside=viewport.Contains(Event.current.mousePosition);
            scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,panel.width-36,Mathf.Max(viewport.height-1,stock.Length*58)));
            for (int i=0;i<stock.Length;i++)
            {
                var item=stock[i]; var rect=new Rect(0,i*58,panel.width-36,50);
                Fill(rect,PlateTint(item.Kind));
                Color border=reserveSelection==item?new Color(.8f,.93f,1):FrameTint(item.Kind);
                Border(rect,border,weapons?2:1);
                DrawIcon(new Rect(rect.x+8,rect.y+7,36,36),item.Kind);
                GUI.Label(new Rect(rect.x+56,rect.y,rect.width-62,rect.height),TelekineticBuild.Label(item.Kind),stockNameStyle);
                if (inside && rect.Contains(Event.current.mousePosition))
                {
                    hoverText=ItemTooltip(item);
                    HandleReserveInput(item,Event.current);
                }
            }
            GUI.EndScrollView();
        }
        private static void Border(Rect rect,Color color,float width)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,width),color); Fill(new Rect(rect.x,rect.yMax-width,rect.width,width),color);
            Fill(new Rect(rect.x,rect.y,width,rect.height),color); Fill(new Rect(rect.xMax-width,rect.y,width,rect.height),color);
        }
        private void HandleReserveInput(TelekineticItem item,Event e)
        {
            if (!CanEdit || e.type!=EventType.MouseDown || e.button!=0) return;
            reserveSelection=item; brush=item.Kind; brushTurns=item.QuarterTurns;
            selected=dragging=null; merging=false; reserveDragging=true; e.Use();
        }
        private void HandleReserveDrop(Event e)
        {
            if (!reserveDragging || e.type!=EventType.MouseUp || e.button!=0) return;
            reserveDragging=false;
            if (reserveSelection!=null && brush.HasValue && PointerCell(e.mousePosition,out var p)) Place(brush.Value,p,brushTurns);
            e.Use();
        }
        private static Vector2 UiCenter(Vector2 p) => new(GridX+(p.x+4.5f)*CellSize,GridY+(4.5f-p.y)*CellSize);
        private static string ShapeLabel(TelekineticPart kind) => kind switch
        {
            TelekineticPart.Shotgun or TelekineticPart.DMG or TelekineticPart.Energy or TelekineticPart.Pierce or TelekineticPart.Splash or TelekineticPart.Shred=>"1×2",
            TelekineticPart.Saw or TelekineticPart.Double or TelekineticPart.Chain or TelekineticPart.Echo=>"2×2", TelekineticPart.RIC=>"Г · 3 кл.", _=>"1×1"
        };
        private void DrawFootprint(TelekineticItem item,Color fill,Color border,float width)
        {
            for (int i=0;i<item.CellCount;i++)
            {
                var r=CellRect(item.Cell(i)); Fill(new Rect(r.x,r.y,CellSize,CellSize),fill);
            }
            for (int i=0;i<item.CellCount;i++)
            {
                var p=item.Cell(i); var r=CellRect(p); r.width=r.height=CellSize;
                if (!item.Contains(p+Vector2Int.up)) Fill(new Rect(r.x,r.y,r.width,width),border);
                if (!item.Contains(p+Vector2Int.down)) Fill(new Rect(r.x,r.yMax-width,r.width,width),border);
                if (!item.Contains(p+Vector2Int.left)) Fill(new Rect(r.x,r.y,width,r.height),border);
                if (!item.Contains(p+Vector2Int.right)) Fill(new Rect(r.xMax-width,r.y,width,r.height),border);
            }
        }
        private bool PointerCell(out Vector2Int p) => PointerCell(Event.current.mousePosition,out p);
        private static bool PointerCell(Vector2 mouse,out Vector2Int p)
        {
            p=new Vector2Int(Mathf.FloorToInt((mouse.x-GridX)/CellSize)-4,4-Mathf.FloorToInt((mouse.y-GridY)/CellSize));
            return Mathf.Abs(p.x)<=4 && Mathf.Abs(p.y)<=4;
        }
        private void DrawPlacementPreview()
        {
            if ((!brush.HasValue && dragging==null) || !PointerCell(out var pointer)) return;
            var anchor=dragging!=null?pointer-dragOffset:pointer;
            int turns=dragging!=null?dragTurns:brushTurns;
            var kind=dragging!=null?dragging.Kind:brush.Value;
            bool valid=dragging!=null?Build.CanMove(dragging,anchor,turns):Build.CanPlace(kind,anchor,turns);
            var preview=new TelekineticItem(0,kind,anchor,turns);
            var color=valid?new Color(.2f,1,.65f):new Color(1,.25f,.2f);
            DrawFootprint(preview,new Color(color.r,color.g,color.b,.22f),color,2);
        }
        private void RotateInteraction()
        {
            if (dragging!=null) { dragTurns=(dragTurns+1)%4; dragOffset=TelekineticItem.RotateOffset(dragOffset,1); }
            else if (brush.HasValue) { brushTurns=(brushTurns+1)%4; notice="Поворот размещаемой формы: "+brushTurns*90+"°"; }
            else if (selected!=null) Rotate(selected);
        }
        private void HandleCell(Vector2Int p,TelekineticItem cell,Rect rect,Event e)
        {
            if (!CanEdit) return;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type==EventType.MouseDown && e.button==0)
            {
                if (merging) { if (Merge(selected,cell)) { merging=false; } e.Use(); return; }
                if (cell!=null) { selected=dragging=cell; dragOffset=p-cell.Position; dragTurns=cell.QuarterTurns; brush=null; reserveSelection=null; Build.Disconnected.Clear(); notice="Выбрано: "+TelekineticBuild.Label(cell.Kind)+". R — повернуть целый предмет."; }
                else if (brush.HasValue) Place(brush.Value,p,brushTurns);
                e.Use();
            }
            else if (e.type==EventType.MouseUp && e.button==0 && dragging!=null)
            {
                var anchor=p-dragOffset;
                if (dragging.Position!=anchor || dragging.QuarterTurns!=dragTurns) Move(dragging,anchor,dragTurns);
                dragging=null; e.Use();
            }
        }
        private void RemoveSelected() { if (Remove(selected)) { selected=null; merging=false; } }
        private void HandleConstructorKeys(Event e)
        {
            if (!CanEdit || e.type!=EventType.KeyDown) return;
            switch(e.keyCode)
            {
                case KeyCode.R: RotateInteraction(); break;
                case KeyCode.Delete: RemoveSelected(); break;
                case KeyCode.M:
                    merging=selected!=null && selected.CanMerge && selected.Level<3;
                    brush=null; dragging=null; break;
                case KeyCode.F: AutoRotate=!AutoRotate; break;
                default: return;
            }
            e.Use();
        }
        private string ItemTooltip(TelekineticItem item)
        {
            string text=TelekineticBuild.Label(item.Kind)+" · "+TelekineticBuild.Roman(item.Level)+"\n"+CompactEffect(item);
            if (item.IsModule) text+=$"\nКД {Modules.Cooldown(item,Build.Items.Contains(item)?Build:TooltipReserveContext):0.##} с";
            else if (item.IsWeapon)
            {
                var profile=Settings(item); float interval=item.Kind==TelekineticPart.Saw?profile.ContactHitInterval:profile.FireInterval;
                float rate=Build.Items.Contains(item)?RateFactor(item):1;
                text+=$"\nКД {interval/rate:0.##} с";
            }
            return text;
        }
        private string CompactEffect(TelekineticItem item)
        {
            float strength=Modules.Strength(item);
            return item.Kind switch
            {
                TelekineticPart.Pistol=>"Выстрел снарядом",TelekineticPart.Shotgun=>"Выстрел дробью",TelekineticPart.Sniper=>"Дальнобойный выстрел",TelekineticPart.Saw=>"Контактный урон",
                TelekineticPart.DMG=>$"Следующая атака: +{(Modules.DamageMultiplier-1)*strength:P0} урона",
                TelekineticPart.FR=>$"Частота +{(Modules.RapidMultiplier-1)*strength:P0} на {Modules.RapidDuration:0.#} с",
                TelekineticPart.Double=>"Дополнительная атака",TelekineticPart.RIC=>$"Рикошеты: +{item.Level}",
                TelekineticPart.Energy=>$"КД соседних модулей ÷{1+Modules.EnergyBonus*item.Level:0.##}",
                TelekineticPart.Pierce=>$"Пробитие целей: +{Mathf.RoundToInt(Modules.PierceBodies*strength)}",
                TelekineticPart.Splash=>"Урон по области",TelekineticPart.Chain=>"Урон дополнительной цели",
                TelekineticPart.Crit=>$"Следующая атака: ×{1+(Modules.CritMultiplier-1)*strength:0.##} урона",
                TelekineticPart.Slow=>$"Замедление на {Modules.SlowSeconds:0.#} с",TelekineticPart.Shred=>"Обходит щиты",
                TelekineticPart.Knock=>"Усиливает отталкивание",TelekineticPart.Echo=>$"Повтор атаки через {Modules.EchoDelay:0.##} с",
                _=>"Соединяет части конструкции"
            };
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
            Text(36,705,650,28,GlitchWarning?"⚠ GLITCH: ПЕРЕСТАНОВКА":Suppressed?"МОДУЛИ ПОДАВЛЕНЫ":ActiveAnomaly?.ToString()??"",true);
        }
    }
}
