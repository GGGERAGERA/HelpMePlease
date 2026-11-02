#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace Subject42.DebugLabs.Telekinetic
{
    public sealed partial class TelekineticConstructorLab
    {
        private static readonly string[] PresetNames={"Круговая защита","Штурмовая платформа","Асимметричная сборка","Компактные эффекты","Тяжёлые эффекты"};
        private const float UiWidth=1600, UiHeight=900, GridX=470, GridY=160, CellSize=65;
        private GUIStyle titleStyle, textStyle, smallStyle, cellStyle, buttonStyle;
        private string densityText="30";
        private Vector2 detailScroll;
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
            if (titleStyle!=null) return;
            titleStyle=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold}; titleStyle.normal.textColor=new Color(.76f,.94f,1);
            textStyle=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true}; textStyle.normal.textColor=new Color(.8f,.87f,.91f);
            smallStyle=new GUIStyle(textStyle){fontSize=13};
            cellStyle=new GUIStyle(smallStyle){fontSize=11,wordWrap=false,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
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
            if (!ready) return;
            EnsureStyles(); var old=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(new Vector3(UiScale,UiScale,1));
            if (Editing) DrawConstructor(); else DrawCombatHud();
            if (Editing && !string.IsNullOrEmpty(GUI.tooltip))
            {
                var mouse=Event.current.mousePosition;
                var rect=new Rect(Mathf.Min(mouse.x+18,1310),Mathf.Min(mouse.y+18,800),275,80);
                Fill(rect,new Color(.025f,.035f,.05f,.97f)); GUI.Label(new Rect(rect.x+8,rect.y+5,259,70),GUI.tooltip,smallStyle);
            }
            GUI.matrix=old;
        }
        private void DrawConstructor()
        {
            Fill(new Rect(0,0,1600,900),new Color(.025f,.04f,.06f,.98f));
            Fill(new Rect(26,24,1548,72),new Color(.06f,.1f,.14f));
            GUI.Label(new Rect(46,33,1100,36),"SUBJECT#42   /   TELEKINETIC CONSTRUCTOR LAB",titleStyle);
            Text(48,70,1050,24,"V2 · многоклеточные предметы · 9 × 9 · R — поворот формы на 90°",true);
            if (Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.R)
            { RotateInteraction(); Event.current.Use(); }
            if (Button(1275,38,278,43,"Tab   /   Вернуться в бой")) SetEditing(false);
            Fill(new Rect(26,113,350,760),new Color(.055f,.075f,.1f));
            Text(45,128,310,27,"СОЗДАТЬ ЭЛЕМЕНТ");
            int row=0;
            foreach (TelekineticPart kind in Enum.GetValues(typeof(TelekineticPart)))
            {
                var rect=new Rect(44+row%3*106,164+row/3*46,100,40); var previous=GUI.backgroundColor; GUI.backgroundColor=FrameTint(kind);
                if (GUI.Button(rect,new GUIContent("",TelekineticBuild.Label(kind)+" · "+ShapeLabel(kind)),buttonStyle))
                { brush=kind; brushTurns=0; selected=null; dragging=null; merging=false; Build.Disconnected.Clear(); notice="Выбрано: "+TelekineticBuild.Label(kind)+". R — повернуть форму, ЛКМ — установить."; }
                GUI.backgroundColor=previous; DrawIcon(new Rect(rect.center.x-15,rect.y+5,30,30),kind);
                if (brush==kind) Fill(new Rect(rect.x,rect.yMax-3,rect.width,3),PartColor(kind));
                row++;
            }
            Text(45,451,310,59,"Cyan — оружие. Серые рамки — модули; цвет внутри обозначает эффект. Наведите на иконку для подсказки.",true);
            if (Button(44,524,314,35,"Сбросить конструкцию")) ResetBuild();
            Text(45,577,300,25,"ПРЕСЕТЫ");
            for (int i=0;i<PresetNames.Length;i++) if (Button(44,610+i*36,314,31,PresetNames[i])) LoadPreset(i);
            Text(45,805,310,59,"ЛКМ: выбор / установка · Drag&Drop: перенос или обмен · R: поворот · Esc: отмена",true);

            Text(GridX,119,640,28,merging?"MERGE: выберите модуль, который будет поглощён":brush.HasValue?"Установка "+TelekineticBuild.Label(brush.Value):"КОНСТРУКЦИЯ  /  выберите элемент");
            for (int y=-4;y<=4;y++) for (int x=-4;x<=4;x++)
            {
                var p=new Vector2Int(x,y); var rect=CellRect(p); var c=Build.At(p);
                bool available=c==null && Build.CanPlace(brush??TelekineticPart.Frame,p,brush.HasValue?brushTurns:0);
                Color color=p==Vector2Int.zero ? new Color(.12f,.42f,.51f) : available ? new Color(.065f,.19f,.16f) : new Color(.055f,.075f,.095f);
                if (Build.Disconnected.Contains(p)) color=new Color(.65f,.12f,.13f);
                Fill(rect,color);
                if (c==null && p!=Vector2Int.zero) GUI.Label(rect,available?"+":"·",cellStyle);
                if (p==Vector2Int.zero)
                {
                    Fill(new Rect(rect.x+21,rect.y+21,20,20),FrameTint(TelekineticPart.Pistol));
                    Fill(new Rect(rect.x+25,rect.y+25,12,12),new Color(.05f,.12f,.17f));
                }
                HandleCell(p,c,new Rect(rect.x,rect.y,CellSize,CellSize));
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
                    Link(a+axis*23,b-axis*23,active?new Color(.3f,1,.85f):new Color(.3f,.43f,.51f),active?4:2);
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
                DrawIcon(new Rect(center.x-21,center.y-21,42,42),c.Kind);
                if (c==selected && c.CanMerge) GUI.Label(new Rect(center.x+14,center.y+11,25,21),TelekineticBuild.Roman(c.Level),cellStyle);
                var anchor=CellRect(c.Position);
                Fill(new Rect(anchor.x+5,anchor.y+5,4,4),Color.white);
                if (c.IsModule) Fill(new Rect(anchor.x+4,anchor.yMax-4,(anchor.width-8)*Mathf.Clamp01(1-c.Remaining/Modules.Cooldown(c,Build)),2),PartColor(c.Kind));
            }
            Text(GridX,755,670,66,notice);
            Text(GridX,829,665,36,"Белая точка: ось R · зелёный: вся форма помещается · яркая линия: синергия",true);
            DrawPlacementPreview();
            if (dragging!=null && Event.current.type==EventType.MouseUp) { dragging=null; Event.current.Use(); }
            DrawDetails();
            if (Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.Delete) { RemoveSelected(); Event.current.Use(); }
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
        private bool PointerCell(out Vector2Int p)
        {
            var mouse=Event.current.mousePosition;
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
        private void HandleCell(Vector2Int p,TelekineticItem cell,Rect rect)
        {
            var e=Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type==EventType.MouseDown && e.button==0)
            {
                if (merging) { if (Merge(selected,cell)) { merging=false; } e.Use(); return; }
                if (cell!=null) { selected=dragging=cell; dragOffset=p-cell.Position; dragTurns=cell.QuarterTurns; brush=null; Build.Disconnected.Clear(); notice="Выбрано: "+TelekineticBuild.Label(cell.Kind)+". R — повернуть целый предмет."; }
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
        private void RemoveSelected() { if (Remove(selected)) selected=null; }
        private void DrawDetails()
        {
            Fill(new Rect(1130,113,444,760),new Color(.055f,.075f,.1f));
            Text(1150,128,400,28,"ЭЛЕМЕНТ И СВЯЗИ");
            detailScroll=GUI.BeginScrollView(new Rect(1148,167,402,355),detailScroll,new Rect(0,0,380,630));
            float y=0;
            void Line(string s,float h=30) { Text(0,y,376,h,s,true); y+=h+5; }
            if (selected==null)
            {
                if (brush.HasValue) { Line(TelekineticBuild.Label(brush.Value)+" · "+ShapeLabel(brush.Value)+$" · {brushTurns*90}°"); Line(Description(brush.Value),80); }
                else Line("Выберите иконку: здесь появятся название, итоговые КД и параметры соседнего оружия.",55);
            }
            else
            {
                Line(TelekineticBuild.Label(selected.Kind)+"  "+TelekineticBuild.Roman(selected.Level)+$"  [{selected.Position.x}, {selected.Position.y}]");
                Line($"Форма {ShapeLabel(selected.Kind)} · {selected.QuarterTurns*90}° · {selected.CellCount} кл. · R — повернуть");
                if (selected.IsModule)
                {
                    Line($"Таймер: {selected.Remaining:F2}s / КД {Modules.Cooldown(selected,Build):F2}s");
                    Line($"Активаций: {selected.Activations} · получателей: {LinkedWeapons(selected)}");
                    float energy=Build.Neighbours(selected).Where(n=>n.Kind==TelekineticPart.Energy).Sum(n=>Modules.EnergyBonus*n.Level);
                    Line($"ENERGY: Σ бонусов = {energy:P0}; делитель КД {1+energy:F2}");
                    Line(Description(selected.Kind,Modules.Strength(selected),selected.Level),65);
                }
                if (selected.Kind==TelekineticPart.Energy)
                {
                    Line($"Каждому соседнему модулю: +{Modules.EnergyBonus*selected.Level:P0} к делителю КД. ENERGY не расходуется.",55);
                    foreach (var m in Build.Neighbours(selected).Where(m=>m.IsModule)) Line($"{TelekineticBuild.Label(m.Kind)} [{m.Position.x},{m.Position.y}]: итоговый КД {Modules.Cooldown(m,Build):F2}s");
                }
                foreach (var c in Build.Items.Where(AffectedWeapon))
                {
                    var linked=Build.Neighbours(c).Where(n=>n.IsModule).ToArray(); var profile=Settings(c);
                    float peakDamage=1+linked.Where(n=>n.Kind==TelekineticPart.DMG).Sum(n=>(Modules.DamageMultiplier-1)*Modules.Strength(n));
                    float peakCritical=1+linked.Where(n=>n.Kind==TelekineticPart.Crit).Sum(n=>(Modules.CritMultiplier-1)*Modules.Strength(n));
                    float peakRate=1+linked.Where(n=>n.Kind==TelekineticPart.FR).Sum(n=>(Modules.RapidMultiplier-1)*Modules.Strength(n));
                    Line($"{TelekineticBuild.Label(c.Kind)} [{c.Position.x},{c.Position.y}]: DMG {profile.Damage*DamageFactor(c)*CriticalFactor(c):F1} → {profile.Damage*peakDamage*peakCritical:F1} при заряде",40);
                    float interval=c.Kind==TelekineticPart.Saw?profile.ContactHitInterval:profile.FireInterval;
                    Line($"Интервал {interval/RateFactor(c):F2}s → {interval/peakRate:F2}s при FR; снарядов {profile.Pellets}");
                    Line($"2X: +{ExtraShots(c)} готово · RIC: +{Ricochets(c)} готово · залпов {c.Shots}");
                    Line("Готовые эффекты: "+string.Join(", ",PendingEffects(c).Select(TelekineticBuild.Label)),45);
                }
            }
            GUI.EndScrollView();
            if (Button(1150,532,187,36,"Удалить / Delete")) RemoveSelected();
            if (Button(1345,532,205,36,"Merge → выбрать"))
            { merging=selected!=null && selected.CanMerge && selected.Level<3; notice=merging?"Выберите такой же модуль того же уровня. Выбранный повысит уровень.":"Merge доступен для двух одинаковых модулей I/II."; brush=null; dragging=null; }
            Text(1150,583,400,48,"ВРАГИ / целевая плотность (без верхнего лимита)",true);
            GUI.SetNextControlName("EnemyDensity"); densityText=GUI.TextField(new Rect(1150,626,100,32),densityText);
            if (Button(1260,626,130,32,"Применить") && int.TryParse(densityText,out int density)) EnemyDensity=Mathf.Max(0,density);
            if (Button(1400,626,70,32,"100")) { EnemyDensity=100; densityText="100"; }
            if (Button(1480,626,70,32,"150")) { EnemyDensity=150; densityText="150"; }
            Invincible=GUI.Toggle(new Rect(1150,677,390,26),Invincible," Неуязвимость игрока (лаборатория)",textStyle);
            if (Button(1150,718,400,40,"Новый бой / восстановить игрока")) NewFight();
            Text(1150,778,395,79,"Одна сторона — соседство; диагоналей нет. Каждый источник держит один заряд на оружии. FR действует по времени. Перестройка отменяет ожидающие заряды и ECHO.",true);
        }
        private void DrawCombatHud()
        {
            if (BulletTime.IsActive)
            {
                Fill(new Rect(0,0,1600,900),new Color(.08f,.35f,.7f,.1f));
                Fill(new Rect(0,0,1600,6),new Color(.2f,.8f,1)); Fill(new Rect(0,894,1600,6),new Color(.2f,.8f,1));
            }
            Fill(new Rect(20,20,710,80),new Color(.02f,.04f,.07f,.88f));
            GUI.Label(new Rect(36,27,680,31),BulletTime.IsActive?"BULLET TIME   /   SHIFT":"TELEKINETIC LAB   /   свободная конструкция",titleStyle);
            Text(36,65,680,28,$"Враги {Alive}/{EnemyDensity} · залпы {ShotsFired} · импульсы {ModulePulses} · HP {Player.CurrentHealth:F0}",true);
            Fill(new Rect(20,810,1560,70),new Color(.02f,.04f,.07f,.9f));
            Text(36,821,1170,28,"WASD — движение     Q / E — поворот     ПКМ — центр     Shift — Bullet Time     Tab — конструктор");
            Text(36,853,410,24,"BULLET TIME: "+Mathf.RoundToInt(BulletTime.Energy*100)+"%   (отдельный запас)",true);
            Fill(new Rect(452,855,250,12),new Color(.12f,.2f,.25f)); Fill(new Rect(452,855,250*BulletTime.Energy,12),new Color(.25f,.8f,1));
            Text(736,853,270,24,$"Q/E: {RotationEnergy:F0}/{RotationCapacity:F0}   (ENERGY ≠ Q/E)",true);
            Fill(new Rect(1020,855,170,12),new Color(.12f,.2f,.25f)); Fill(new Rect(1020,855,170*RotationEnergy/RotationCapacity,12),new Color(.95f,.74f,.3f));
            if (Button(1250,826,310,35,"Tab   /   Конструктор")) SetEditing(true);
        }
    }
}
#endif
