using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Rendering and input adapter only; graph/availability/start validation belong to services.</summary>
public sealed class SurfaceMapView : MonoBehaviour
{
    [SerializeField] private RectTransform graph;
    [SerializeField] private Button nodeTemplate;
    [SerializeField] private Image lineTemplate;
    [SerializeField] private TMP_Text details;
    [SerializeField] private Button startButton;
    [SerializeField] private Button closeButton;
    private readonly Dictionary<string, Button> nodes = new();
    private readonly Dictionary<string, TMP_Text> markerIcons = new();
    private readonly List<Route> routes = new();
    private SurfaceMapService service;
    private SurfaceMapDefinition renderedMap;
    private BunkerRunStarter starter;
    private BunkerPanelManager owner;
    private Transform transitionTarget;
    private TMP_Text selectionLabel;
    private SurfaceMapNavigation navigation;
    private string inspectedId, lastSelectedId;
    private bool centered;
    private static readonly Color Locked = new(.23f,.32f,.39f);
    private static readonly Color Selected = new(.48f,.96f,1f);
    private static readonly Color Ink = new(.025f,.055f,.08f,1f);
    private static readonly Color Cyan = new(.34f,.85f,.94f,1f);
    private static readonly Color Muted = new(.38f,.52f,.6f,1f);
    private static readonly Color Complete = new(.42f,.85f,.71f,1f);
    private sealed class Route { public string From, To; public readonly List<Image> Segments = new(); }

    private void Awake()
    {
        nodeTemplate.gameObject.SetActive(false); lineTemplate.gameObject.SetActive(false);
        var root = (RectTransform)transform; root.sizeDelta = new Vector2(1160,700);
        GetComponent<Image>().color = new Color(.025f,.045f,.065f,.99f);
        var title = transform.Find("Title").GetComponent<TMP_Text>();
        Place(title.rectTransform,new Vector2(-130,302),new Vector2(810,44));
        title.text = "SURFACE EXPEDITION MAP"; title.fontSize = 27; title.alignment = TextAlignmentOptions.Left; title.color = Cyan;
        Label("Uplink",transform,new Vector2(-110,269),new Vector2(850,24),"BUNKER UPLINK  /  SECTOR TELEMETRY  /  042",13,Muted);
        Bar(transform,new Vector2(0,247),new Vector2(1112,2),new Color(.17f,.4f,.49f));
        Bar(transform,new Vector2(-555,301),new Vector2(5,44),Cyan);
        var viewportObject = new GameObject("Map Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(SurfaceMapNavigation));
        var viewport = (RectTransform)viewportObject.transform; viewport.SetParent(graph.parent, false);
        Place(viewport,new Vector2(-185,-22),new Vector2(744,514));
        viewportObject.GetComponent<Image>().color = Ink;
        for (int x=-350;x<=350;x+=35) Bar(viewport,new Vector2(x,0),new Vector2(1,514),new Color(.1f,.25f,.3f,.26f));
        for (int y=-245;y<=245;y+=35) Bar(viewport,new Vector2(0,y),new Vector2(744,1),new Color(.1f,.25f,.3f,.26f));
        for (int y=-252;y<=252;y+=7) Bar(viewport,new Vector2(0,y),new Vector2(744,1),new Color(0,0,0,.09f));
        Label("Coordinates",viewport,new Vector2(-6,235),new Vector2(700,20),"X: 000   Y: 042                                     N / SURFACE",11,Muted);
        Label("Map caption",viewport,new Vector2(-6,-235),new Vector2(700,20),"SECTOR NETWORK                       UPLINK // STABLE",11,Muted);
        graph.SetParent(viewport, false); graph.anchoredPosition = new Vector2(-175,-62);
        navigation = viewportObject.GetComponent<SurfaceMapNavigation>(); navigation.Configure(graph);
        Bar(transform,new Vector2(204,-18),new Vector2(2,510),new Color(.16f,.33f,.4f));
        var card = Bar(transform,new Vector2(391,-2),new Vector2(330,473),new Color(.038f,.074f,.099f));
        card.transform.SetSiblingIndex(title.transform.GetSiblingIndex());
        selectionLabel = Label("Selection",transform,new Vector2(390,211),new Vector2(306,24),"SECTOR INTELLIGENCE",14,Cyan);
        Place(details.rectTransform,new Vector2(391,-5),new Vector2(298,386));
        details.fontSize=16; details.color=new Color(.72f,.83f,.87f); details.lineSpacing=8; details.richText=true;
        details.alignment=TextAlignmentOptions.TopLeft;
        Place(startButton.GetComponent<RectTransform>(),new Vector2(391,-260),new Vector2(330,49));
        StyleButton(startButton,"DEPLOY TO SECTOR",18,Cyan);
        Place(closeButton.GetComponent<RectTransform>(),new Vector2(530,303),new Vector2(44,38));
        StyleButton(closeButton,"X",18,Muted);
        Label("Navigation hint",transform,new Vector2(-230,-329),new Vector2(650,20),"DRAG TO PAN / SCROLL TO ZOOM",10,Muted);
        var legend = Label("Legend",transform,new Vector2(-190,-299),new Vector2(732,24),"<color=#57D9F0>AVAILABLE</color>  <color=#6BD9B5>COMPLETED</color>  <color=#3B5263>LOCKED</color>  <color=#FFCC4D>? UNKNOWN</color>  <color=#FFB247>! MISSION</color>",11,Muted);
        legend.richText=true;
        var reset=Instantiate(closeButton,transform); reset.name="Reset View";
        Place((RectTransform)reset.transform,new Vector2(115,-329),new Vector2(134,23));
        StyleButton(reset,"RESET VIEW",10,Muted); reset.onClick=new Button.ButtonClickedEvent(); reset.onClick.AddListener(() => navigation.ResetView());
        Label("Terminal ID",transform,new Vector2(390,-307),new Vector2(330,25),"SUBJECT 42  //  EXPEDITION CONTROL",10,Muted);
        startButton.onClick.AddListener(StartSelected); closeButton.onClick.AddListener(Close);
    }
    public void Show(SurfaceMapService mapService, BunkerRunStarter runStarter, BunkerPanelManager panelOwner, Transform target)
    {
        if (service != null) service.Changed -= Refresh;
        service = mapService; starter = runStarter; owner = panelOwner; transitionTarget = target;
        if (service == null) { Debug.LogError("Surface map service is not configured.", this); return; }
        gameObject.SetActive(true); // Awake initializes navigation before the first graph build.
        if (renderedMap != service.Definition) BuildGraph();
        service.Changed += Refresh;
        if (!centered) { navigation.ResetView(); centered=true; }
        Refresh();
    }
    private void OnDisable() { if (service != null) service.Changed -= Refresh; }
    private void BuildGraph()
    {
        foreach (Transform child in graph)
            if (child != nodeTemplate.transform && child != lineTemplate.transform) Destroy(child.gameObject);
        nodes.Clear(); markerIcons.Clear(); routes.Clear(); renderedMap = service.Definition; inspectedId=null; centered=false;
        var edges = new HashSet<(string, string)>();
        foreach (var sector in renderedMap.Sectors)
            foreach (string neighbour in sector.Neighbours)
            {
                var key = string.CompareOrdinal(sector.Id, neighbour) < 0 ? (sector.Id, neighbour) : (neighbour, sector.Id);
                if (edges.Add(key)) Line(sector.Id, neighbour, sector.MapPosition, renderedMap.Find(neighbour).MapPosition);
            }
        foreach (string id in renderedMap.StartingSectors) Line(null,id,renderedMap.BunkerPosition,renderedMap.Find(id).MapPosition);
        var bunker = Node(renderedMap.BunkerPosition,"BUNKER",true);
        bunker.GetComponent<SurfaceMapNodeFeedback>().DoubleClick=() => navigation.ResetView();
        foreach (var sector in renderedMap.Sectors)
        {
            string id = sector.Id; var button = Node(sector.MapPosition,id,false);
            button.onClick.AddListener(() => Inspect(id)); nodes.Add(id,button);
            var badge = Plate("Marker badge",button.transform,new Vector2(62,29),new Vector2(29,29),Ink);
            var label = Label("Content Marker",badge.transform,Vector2.zero,new Vector2(28,28),"",19,Cyan);
            label.alignment=TextAlignmentOptions.Center;
            label.gameObject.AddComponent<SurfaceMarkerPulse>(); markerIcons.Add(id,label);
        }
        Vector2 min=renderedMap.BunkerPosition*1.15f, max=min;
        foreach(var sector in renderedMap.Sectors) { min=Vector2.Min(min,sector.MapPosition*1.15f); max=Vector2.Max(max,sector.MapPosition*1.15f); }
        navigation.SetBounds(Rect.MinMaxRect(min.x-85,min.y-50,max.x+85,max.y+50),renderedMap.BunkerPosition*1.15f);
    }
    private Button Node(Vector2 position, string label, bool bunker)
    {
        var button = Instantiate(nodeTemplate, graph); button.gameObject.SetActive(true); button.name=label;
        Place((RectTransform)button.transform,position*1.15f,new Vector2(bunker?158:126,bunker?84:70));
        var image=button.GetComponent<Image>(); image.color=Color.clear;
        var rim=Plate("Rim",button.transform,Vector2.zero,((RectTransform)button.transform).sizeDelta,bunker?Cyan:Muted);
        rim.transform.SetAsFirstSibling();
        Plate("Fill",rim.transform,Vector2.zero,((RectTransform)button.transform).sizeDelta-new Vector2(4,4),Ink);
        var text=button.GetComponentInChildren<TMP_Text>(true); text.transform.SetAsLastSibling();
        Place(text.rectTransform,new Vector2(0,bunker?7:9),new Vector2(bunker?152:120,32));
        text.text=label; text.fontSize=bunker?24:29; text.color=Cyan; text.alignment=TextAlignmentOptions.Center;
        var status=Label("Status",button.transform,new Vector2(0,-21),new Vector2(bunker?150:120,18),bunker?"HOME / UPLINK":"AVAILABLE",10,Muted);
        status.alignment=TextAlignmentOptions.Center; status.font=text.font; status.fontSharedMaterial=text.fontSharedMaterial;
        var selection=Plate("Selection outline",button.transform,Vector2.zero,((RectTransform)button.transform).sizeDelta+new Vector2(9,9),Selected);
        selection.transform.SetAsFirstSibling(); selection.gameObject.SetActive(false);
        var feedback=button.gameObject.AddComponent<SurfaceMapNodeFeedback>(); feedback.Configure(rim);
        button.transition=Selectable.Transition.None;
        button.targetGraphic=rim;
        var colors=button.colors; colors.normalColor=Color.white; colors.disabledColor=Color.white;
        colors.highlightedColor=new Color(1.2f,1.2f,1.2f); colors.selectedColor=Color.white; button.colors=colors;
        if (bunker) { Bar(button.transform,new Vector2(0,33),new Vector2(74,3),Cyan); }
        return button;
    }
    private void Line(string fromId,string toId,Vector2 from, Vector2 to)
    {
        from*=1.15f; to*=1.15f;
        var route=new Route { From=fromId, To=toId }; routes.Add(route);
        var direction=(to-from).normalized; float length=Vector2.Distance(from,to);
        float Inset(bool bunker) => Mathf.Min(Mathf.Abs(direction.x)>.001f?(bunker?82f:67f)/Mathf.Abs(direction.x):float.PositiveInfinity,
            Mathf.Abs(direction.y)>.001f?(bunker?46f:39f)/Mathf.Abs(direction.y):float.PositiveInfinity);
        float begin=Inset(fromId==null), end=length-Inset(false);
        for(float distance=begin;distance<end;distance+=12f)
        {
            float span=Mathf.Min(7,end-distance);
            var line=Instantiate(lineTemplate,graph); line.gameObject.SetActive(true);
            var rect=line.rectTransform; rect.anchoredPosition=from+direction*(distance+span*.5f);
            rect.sizeDelta=new Vector2(span,3); rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            rect.SetAsFirstSibling(); route.Segments.Add(line);
        }
    }
    public void Inspect(string id)
    {
        if(renderedMap.Find(id)==null)return;
        inspectedId=id;
        if(!service.TrySelect(id))Refresh();
    }
    private void Refresh()
    {
        if(inspectedId==null||lastSelectedId!=service.SelectedSectorId)inspectedId=service.SelectedSectorId;
        lastSelectedId=service.SelectedSectorId;
        var path=FindRoute(inspectedId);
        var highlighted=new HashSet<(string,string)>();
        for(int i=0;i<path.Count;i++)highlighted.Add((i==0?null:path[i-1],path[i]));
        foreach (var pair in nodes)
        {
            var state=service.GetStatus(pair.Key); bool selected=pair.Key==inspectedId;
            var icon=markerIcons[pair.Key];
            bool hasMarker=service.Content.TryGetMarker(pair.Key,out var marker)&&marker.Marker!=null;
            icon.transform.parent.gameObject.SetActive(hasMarker);
            if(hasMarker)
            {
                icon.text=marker.State==SurfaceMarkerState.Active?marker.Marker.icon:marker.Marker.revealedIcon;
                icon.color=marker.Marker.color;
                icon.transform.parent.GetComponent<SurfaceMapPlate>().color=Color.Lerp(Ink,marker.Marker.color,.24f);
                icon.GetComponent<SurfaceMarkerPulse>().Pulsing=marker.Marker.pulse&&marker.State==SurfaceMarkerState.Active;
            }
            pair.Value.interactable=true;
            var feedback=pair.Value.GetComponent<SurfaceMapNodeFeedback>(); feedback.Selected=selected; feedback.Available=state!=SurfaceSectorStatus.Locked;
            pair.Value.transform.localScale=Vector3.one*(selected?1.05f:1f);
            pair.Value.transform.Find("Selection outline").gameObject.SetActive(selected);
            Color tint=selected?Selected:state==SurfaceSectorStatus.Completed?Complete:state==SurfaceSectorStatus.Locked?Locked:Cyan;
            var rim=pair.Value.transform.Find("Rim").GetComponent<SurfaceMapPlate>(); rim.color=tint;
            rim.transform.Find("Fill").GetComponent<SurfaceMapPlate>().color=Ink;
            pair.Value.GetComponentInChildren<TMP_Text>(true).color=state==SurfaceSectorStatus.Locked?Muted:Color.Lerp(tint,Color.white,.35f);
            var status=pair.Value.transform.Find("Status").GetComponent<TMP_Text>();
            status.text=selected&&state!=SurfaceSectorStatus.Locked?"SELECTED":state.ToString().ToUpperInvariant(); status.color=state==SurfaceSectorStatus.Locked?Locked:tint;
        }
        foreach(var route in routes)
        {
            var end=service.GetStatus(route.To);
            var from=route.From==null?SurfaceSectorStatus.Completed:service.GetStatus(route.From);
            bool complete=end==SurfaceSectorStatus.Completed&&from==SurfaceSectorStatus.Completed;
            bool locked=end==SurfaceSectorStatus.Locked||from==SurfaceSectorStatus.Locked;
            bool active=highlighted.Contains((route.From,route.To))||highlighted.Contains((route.To,route.From));
            Color tint=active?Selected:complete?Complete:locked?new Color(.13f,.21f,.28f):new Color(.22f,.47f,.55f);
            foreach(var segment in route.Segments) { segment.color=tint; segment.rectTransform.sizeDelta=new Vector2(segment.rectTransform.sizeDelta.x,active?5:complete?4:3); }
        }
        var sector=renderedMap.Find(inspectedId);
        bool ready=sector!=null&&service.GetStatus(inspectedId)!=SurfaceSectorStatus.Locked&&service.SelectedSectorId==inspectedId;
        startButton.interactable=ready&&!SceneTransitionOverlay.IsTransitioning&&starter!=null&&!starter.IsTransitioning;
        var deployLabel=startButton.GetComponentInChildren<TMP_Text>();
        deployLabel.color=startButton.interactable?Cyan:Muted;
        deployLabel.text=ready?"DEPLOY TO SECTOR":sector!=null?"ROUTE LOCKED":"SELECT A SECTOR";
        if(sector==null) { selectionLabel.text="SECTOR INTELLIGENCE"; details.text="<size=24>AWAITING TARGET</size>\n\nSelect a sector to inspect expedition telemetry."; return; }
        var stateSelected=service.GetStatus(inspectedId);
        selectionLabel.text="SECTOR INTELLIGENCE";
        string heading=$"<size=27><color=#B1EDF4>SECTOR {sector.Id}</color></size>\n<size=14>{stateSelected.ToString().ToUpperInvariant()}</size>\n\n";
        string lockedReason=stateSelected==SurfaceSectorStatus.Locked?"<color=#F0C66B>ROUTE LOCKED</color>\n"+LockedReason(inspectedId)+"\n\n":"";
        bool content=service.Content.TryGetMarker(inspectedId,out var selectedMarker);
        if(content&&!string.IsNullOrEmpty(selectedMarker.Status))
        {
            string tint=ColorUtility.ToHtmlStringRGB(selectedMarker.Marker!=null?selectedMarker.Marker.color:Cyan);
            details.text=heading+lockedReason+$"<color=#{tint}>{selectedMarker.Status}</color>\n\n<size=21>{selectedMarker.Title}</size>\n{selectedMarker.Description}\n\n"+
                $"<color=#658B9E>OBJECTIVE</color>\n{selectedMarker.Objective}\n\n<color=#658B9E>REWARD</color>\n<color=#6BD9B5>{selectedMarker.Reward}</color>";
            return;
        }
        if(content&&selectedMarker.Concealed)
        {
            details.text=heading+lockedReason+$"<color=#{ColorUtility.ToHtmlStringRGB(selectedMarker.Marker!=null?selectedMarker.Marker.color:Cyan)}>{selectedMarker.Title}</color>\n{selectedMarker.Description}\n\n"+
                "<color=#658B9E>THREAT</color>       ??\n<color=#658B9E>ACTIVITY</color>     UNKNOWN\n<color=#658B9E>REWARD</color>       ???"; return;
        }
        var config=service.Content.Resolve(sector.BuildRunConfig(renderedMap.Id));
        string layout=config.LayoutProfile!=null?CleanName(config.LayoutProfile.name):"DEFAULT";
        string rule=config.WorldRule!=null?config.WorldRule.DisplayName:"DEFAULT";
        string events=Mathf.Approximately(config.EventFrequency,1)?"NORMAL":$"{config.EventFrequency:0.##}x FREQUENCY";
        details.text=heading+lockedReason+
            $"<color=#658B9E>THREAT</color>      {config.InitialThreat:0} / 100\n<color=#658B9E>LAYOUT</color>       {layout}\n<color=#658B9E>ACTIVITY</color>     {config.SpawnPressure:0.##}x PRESSURE\n<color=#658B9E>EVENTS</color>       {events}\n\n"+
            $"<color=#658B9E>START MODIFIER</color>\n{rule}\n\n<color=#658B9E>REWARD</color>\n<color=#87DABB>{Reward("XP",config.Experience)} / {Reward("GOLD",config.Gold)}</color>"+
            (content?$"\n\n<color=#{ColorUtility.ToHtmlStringRGB(selectedMarker.Marker!=null?selectedMarker.Marker.color:Cyan)}> {selectedMarker.Title}</color>\n<size=14>{selectedMarker.Description}</size>":"");
    }
    private static string Reward(string label,float value) => Mathf.Approximately(value,1)?label+" STANDARD":$"{label} {(value>=1?"+":"")}{(value-1)*100:0}%";
    private static string CleanName(string value) => value.Replace("ExplorationSectorConfig","").Replace("_"," ").Trim().ToUpperInvariant();
    // Presentation traversal: prefer an established route, falling back to a planned route for locked inspection.
    public List<string> FindRoute(string target)
    {
        List<string> Search(bool unlockedOnly)
        {
            var parents=new Dictionary<string,string>(); var queue=new Queue<string>();
            foreach(string id in renderedMap.StartingSectors) { parents[id]=null; queue.Enqueue(id); }
            while(queue.Count>0)
            {
                string id=queue.Dequeue(); if(id==target)
                {
                    var result=new List<string>(); for(string current=id;current!=null;current=parents[current])result.Add(current);
                    result.Reverse(); return result;
                }
                foreach(string next in renderedMap.Find(id).Neighbours)
                    if(!parents.ContainsKey(next)&&(!unlockedOnly||service.GetStatus(next)!=SurfaceSectorStatus.Locked)) { parents[next]=id; queue.Enqueue(next); }
            }
            return new List<string>();
        }
        var route=Search(true); return route.Count>0?route:Search(false);
    }
    public string LockedReason(string id)
    {
        var candidates=new List<string>();
        foreach(string neighbour in renderedMap.Find(id).Neighbours)
            if(service.GetStatus(neighbour)==SurfaceSectorStatus.Available)candidates.Add(neighbour);
        if(candidates.Count>0)return "Complete "+string.Join(" or ",candidates)+" to establish access.";
        var path=FindRoute(id);
        for(int i=path.Count-2;i>=0;i--)
            if(service.GetStatus(path[i])!=SurfaceSectorStatus.Completed)return "Complete "+path[i]+" to establish access.";
        return "Establish access through a connected sector.";
    }
    private TMP_Text Label(string name,Transform parent,Vector2 position,Vector2 size,string value,float fontSize,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var label=go.GetComponent<TMP_Text>(); label.font=details.font; label.fontSharedMaterial=details.fontSharedMaterial;
        Place(label.rectTransform,position,size); label.text=value; label.fontSize=fontSize; label.color=color;
        label.raycastTarget=false; label.alignment=TextAlignmentOptions.Left; return label;
    }
    private static void Place(RectTransform rect,Vector2 position,Vector2 size)
    { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=position; rect.sizeDelta=size; }
    private static Image Bar(Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var go=new GameObject("Terminal detail",typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; Place(image.rectTransform,position,size); return image;
    }
    private static SurfaceMapPlate Plate(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(SurfaceMapPlate)); go.transform.SetParent(parent,false);
        var plate=go.GetComponent<SurfaceMapPlate>(); plate.color=color; plate.raycastTarget=false; Place(plate.rectTransform,position,size); return plate;
    }
    private static void StyleButton(Button button,string text,float size,Color color)
    {
        button.GetComponent<Image>().color=new Color(.08f,.21f,.28f);
        var label=button.GetComponentInChildren<TMP_Text>(); label.text=text; label.fontSize=size; label.color=color;
        Place(label.rectTransform,Vector2.zero,button.GetComponent<RectTransform>().sizeDelta-new Vector2(8,0));
    }
    private void StartSelected() { if (service != null && isActiveAndEnabled && startButton.interactable) starter.StartSurfaceRun(transitionTarget); }
    private void Close() => owner.CloseAll();
}




