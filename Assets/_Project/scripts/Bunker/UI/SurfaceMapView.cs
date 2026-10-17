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
    [SerializeField] private ExpeditionTaskPicker taskPicker;
    private readonly Dictionary<string, Button> nodes = new();
    private readonly Dictionary<string, SurfaceMapGlyph> markerIcons = new();
    private readonly List<Route> routes = new();
    private SurfaceMapService service;
    private SurfaceMapDefinition renderedMap;
    private BunkerRunStarter starter;
    private BunkerPanelManager owner;
    private Transform transitionTarget;
    [SerializeField] private TMP_Text selectionLabel;
    [SerializeField] private SurfaceMapNavigation navigation;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button scanButton;
    [System.Serializable] public sealed class Landmark
    {
        public string Id;
        public Vector2 Position;
        public SurfaceLocationFamily Family;
        public Sprite Sprite;
        public Image Fog;
    }
    [System.Serializable] public sealed class Trail
    {
        public string From, To;
        public Sprite Sprite, HintFrom, HintTo;
    }
    [System.Serializable] public sealed class LocationIcon
    {
        public SurfaceLocationFamily Family;
        public Sprite Sprite;
    }
    [SerializeField] private CharacterData defaultCharacter;
    [SerializeField] private LocationIcon[] locationIcons = System.Array.Empty<LocationIcon>();
    private RunSelectionManager characterSelection;
    private Image hero;
    [SerializeField] private SurfaceMapFog fog;
    private readonly Dictionary<string, Dictionary<SurfaceMapMeaning, SurfaceMapGlyph>> indicators = new();
    private readonly List<FactRow> facts = new();
    private sealed class FactRow
    {
        public GameObject Root;
        public SurfaceMapGlyph Icon;
        public TMP_Text Label;
        public LayoutElement Layout;
    }
    [SerializeField] private RectTransform world;
    [SerializeField] private RectTransform card;
    [SerializeField] private Landmark[] landmarks = System.Array.Empty<Landmark>();
    [SerializeField] private Trail[] trails = System.Array.Empty<Trail>();
    [SerializeField] private Sprite selectedSprite;
    private LocalizationService localization;
    private string inspectedId, lastSelectedId;
    private bool centered;
    private static readonly Color Selected = new(.78f,.67f,.41f);
    private static readonly Color Muted = new(.51f,.55f,.54f);
    private static readonly Color Complete = new(.57f,.66f,.57f);
    private static string L(string key) => SurfaceMapBriefing.Text(key);
    private sealed class Route
    {
        public string From, To;
        public Image Image;
        public Trail Artwork;
        public bool Reversed;
    }

    private void Awake()
    {
        nodeTemplate.gameObject.SetActive(false); lineTemplate.gameObject.SetActive(false);
        navigation.Configure(graph);
        if(fog!=null)fog.Configure(graph);
        if (resetButton != null) resetButton.onClick.AddListener(ResetView);
        startButton.onClick.AddListener(StartSelected); closeButton.onClick.AddListener(Close);
        scanButton.onClick.AddListener(ScanInspected);
    }
    private void OnEnable()
    {
        localization = LocalizationService.Instance;
        if (localization != null) localization.LanguageChanged += HandleLanguageChanged;
        BindCharacterSelection();
    }
    private void HandleLanguageChanged(GameLanguage language) { if (service != null) Refresh(); }
    private void ResetView() => navigation.ResetView();
    private void OnDestroy()
    {
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetView);
        if (startButton != null) startButton.onClick.RemoveListener(StartSelected);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (scanButton != null) scanButton.onClick.RemoveListener(ScanInspected);
    }
    public void Show(SurfaceMapService mapService, BunkerRunStarter runStarter, BunkerPanelManager panelOwner, Transform target)
    {
        if (service != null) service.Changed -= Refresh;
        service = mapService; starter = runStarter; owner = panelOwner; transitionTarget = target;
        if (service == null) { Debug.LogError("Surface map service is not configured.", this); return; }
        gameObject.SetActive(true); // Awake initializes navigation before the first graph build.
        if (renderedMap != service.Definition) BuildGraph();
        if (service.SelectedSectorId == null)
            foreach (string id in renderedMap.StartingSectors)
                if (service.TrySelect(id)) break;
        service.Changed += Refresh;
        if (!centered) { navigation.ResetView(); centered=true; }
        BindCharacterSelection();
        RefreshHero(characterSelection != null ? characterSelection.SelectedCharacter : null);
        Refresh();
    }
    private void OnDisable()
    {
        if (taskPicker != null) taskPicker.gameObject.SetActive(false);
        if (service != null) service.Changed -= Refresh;
        if (localization != null) localization.LanguageChanged -= HandleLanguageChanged;
        if (characterSelection != null) characterSelection.CharacterSelected -= RefreshHero;
        characterSelection = null;
    }
    private void BindCharacterSelection()
    {
        if (characterSelection == RunSelectionManager.Instance) return;
        if (characterSelection != null) characterSelection.CharacterSelected -= RefreshHero;
        characterSelection = RunSelectionManager.Instance;
        if (characterSelection != null) characterSelection.CharacterSelected += RefreshHero;
    }
    private void RefreshHero(CharacterData character)
    {
        if (hero == null) return;
        var current = character != null ? character : defaultCharacter;
        hero.sprite = current != null ? current.GameplayIcon : null;
        hero.gameObject.SetActive(hero.sprite != null);
    }
    private Sprite LocationSprite(string id)
    {
        var art = Art(id);
        var family=renderedMap.Find(id).LocationFamily;
        if (art?.Sprite != null && art.Family==family) return art.Sprite;
        var icon = System.Array.Find(locationIcons, item => item.Family == family);
        return icon?.Sprite;
    }
    private Landmark Art(string id) => System.Array.Find(landmarks, item => item.Id == id);
    private Vector2 Position(string id) => Art(id)?.Position ??
        (id == "BUNKER" ? renderedMap.BunkerPosition : renderedMap.Find(id).MapPosition);

    private void BuildGraph()
    {
        foreach (Transform child in graph)
            if (child != nodeTemplate.transform && child != lineTemplate.transform && child != world) Destroy(child.gameObject);
        nodes.Clear(); markerIcons.Clear(); indicators.Clear(); routes.Clear(); renderedMap = service.Definition; inspectedId=null; centered=false;
        var edges = new HashSet<(string, string)>();
        foreach (var sector in renderedMap.Sectors)
            foreach (string neighbour in sector.Neighbours)
            {
                var key = string.CompareOrdinal(sector.Id, neighbour) < 0 ? (sector.Id, neighbour) : (neighbour, sector.Id);
                if (edges.Add(key)) Line(sector.Id, neighbour);
            }
        foreach (string id in renderedMap.StartingSectors) Line(null,id);
        foreach (var sector in renderedMap.Sectors) if (sector.IsEscapeFinal) Line(null,sector.Id);
        var bunker = Node("BUNKER",true);
        bunker.GetComponent<SurfaceMapNodeFeedback>().DoubleClick=ResetView;
        foreach (var sector in renderedMap.Sectors)
        {
            string id = sector.Id; var button = Node(id,false);
            button.onClick.AddListener(() => Inspect(id)); nodes.Add(id,button);
        }
        // Bounds describe presentation art only. Sector identities, links and progression remain in Definition.
        Vector2 min=new(-534,-280), max=new(534,280);
        foreach(var sector in renderedMap.Sectors) { min=Vector2.Min(min,Position(sector.Id)); max=Vector2.Max(max,Position(sector.Id)); }
        navigation.SetBounds(Rect.MinMaxRect(min.x-86,min.y-100,max.x+86,max.y+100),Vector2.zero);
    }
    private Button Node(string id, bool bunker)
    {
        var button = Instantiate(nodeTemplate, graph); button.gameObject.SetActive(true); button.name=id;
        Place((RectTransform)button.transform,Position(id),new Vector2(208,bunker?242:224));
        button.GetComponent<Image>().color=Color.clear;
        var art=Art(id);
        Graphic image;
        if(bunker)image=Picture("Landmark",button.transform,Vector2.zero,new Vector2(224,196),art?.Sprite,Color.white);
        else
        {
            // A neutral selectable pad. Conditions use the same symbols as the briefing below it.
            var go=new GameObject("Landmark",typeof(RectTransform),typeof(SurfaceMapPlate));
            go.layer=button.gameObject.layer; go.transform.SetParent(button.transform,false);
            Place((RectTransform)go.transform,Vector2.zero,new Vector2(88,54));
            var pad=go.GetComponent<SurfaceMapPlate>(); pad.Cut=8; pad.color=new Color(.34f,.4f,.38f); pad.raycastTarget=false;
            var fillObject=new GameObject("Pad fill",typeof(RectTransform),typeof(SurfaceMapPlate));
            fillObject.layer=go.layer; fillObject.transform.SetParent(go.transform,false);
            var fillRect=(RectTransform)fillObject.transform; fillRect.anchorMin=Vector2.zero; fillRect.anchorMax=Vector2.one;
            fillRect.offsetMin=Vector2.one*3; fillRect.offsetMax=-Vector2.one*3;
            var fill=fillObject.GetComponent<SurfaceMapPlate>(); fill.Cut=6; fill.color=new Color(.13f,.18f,.18f); fill.raycastTarget=false;
            image=pad;
        }
        if (bunker)
        {
            hero=Picture("Hero",button.transform,new Vector2(0,-64),new Vector2(56,56),null,Color.white);
            hero.preserveAspect=true;
        }
        else
        {
            var symbols=new Dictionary<SurfaceMapMeaning,SurfaceMapGlyph>();
            foreach(SurfaceMapMeaning meaning in System.Enum.GetValues(typeof(SurfaceMapMeaning)))
            {
                if(meaning==SurfaceMapMeaning.Expedition || meaning==SurfaceMapMeaning.Danger)continue;
                var go=new GameObject(meaning.ToString(),typeof(RectTransform),typeof(SurfaceMapGlyph));
                go.layer=button.gameObject.layer; go.transform.SetParent(button.transform,false);
                Place((RectTransform)go.transform,Vector2.zero,new Vector2(18,18));
                var symbol=go.GetComponent<SurfaceMapGlyph>(); symbol.Show(meaning); symbols.Add(meaning,symbol);
                go.SetActive(false);
            }
            indicators.Add(id,symbols); markerIcons.Add(id,symbols[SurfaceMapMeaning.Event]);
        }
        var text=button.GetComponentInChildren<TMP_Text>(true);
        Place(text.rectTransform,new Vector2(0,bunker?-122:0),new Vector2(164,22));
        text.text=bunker?L("map.home"):renderedMap.Find(id).IsEscapeFinal?L("escape.final.node"):id; text.fontSize=16; text.color=bunker?Muted:new Color(.73f,.77f,.71f); text.alignment=TextAlignmentOptions.Center;
        text.textWrappingMode=TextWrappingModes.NoWrap; text.transform.SetAsLastSibling();
        var state=Label("State",button.transform,new Vector2(-48,-30),new Vector2(18,20),"V",14,Complete);
        state.alignment=TextAlignmentOptions.Center; state.gameObject.SetActive(false);
        var selection=Picture("Selection",button.transform,new Vector2(0,bunker?98:-32),bunker?new Vector2(18,10):new Vector2(48,2),bunker?selectedSprite:null,new Color(.81f,.84f,.76f));
        selection.gameObject.SetActive(false);
        var feedback=button.gameObject.AddComponent<SurfaceMapNodeFeedback>(); feedback.Configure(image,selection.rectTransform);
        button.transition=Selectable.Transition.None; button.targetGraphic=button.GetComponent<Image>();
        return button;
    }
    private void Line(string fromId,string toId)
    {
        var art=System.Array.Find(trails,item => ((item.From??"")== (fromId??"")&&item.To==toId)||(item.From==toId&&(item.To??"")== (fromId??"")));
        var route=new Route { From=fromId, To=toId, Artwork=art, Reversed=art!=null && (art.From??"")!=(fromId??"") }; routes.Add(route);
        if(art!=null)
        {
            var image=Picture("Trail "+(fromId??"BUNKER")+"-"+toId,graph,Vector2.zero,new Vector2(1280,800),art.Sprite,Muted);
            image.transform.SetSiblingIndex(world.GetSiblingIndex()+1); route.Image=image;
        }
        else
        {
            // Preserve connectivity if new authored sectors precede their artwork.
            Vector2 from=Position(fromId??"BUNKER"),to=Position(toId),delta=to-from;
            var image=Instantiate(lineTemplate,graph); image.gameObject.SetActive(true);
            Place(image.rectTransform,(from+to)*.5f,new Vector2(delta.magnitude,2));
            image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            image.transform.SetSiblingIndex(world.GetSiblingIndex()+1); route.Image=image;
        }
    }
    private static Image Picture(string name,Transform parent,Vector2 position,Vector2 size,Sprite sprite,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.layer=parent.gameObject.layer; go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.sprite=sprite; image.color=color; image.raycastTarget=false;
        Place(image.rectTransform,position,size); return image;
    }
    public void Inspect(string id)
    {
        if(service==null || renderedMap.Find(id)==null || service.GetStatus(id)==SurfaceSectorStatus.Locked)return;
        inspectedId=id;
        if(!service.TrySelect(id))Refresh();
    }
    public void ScanInspected()
    {
        if (isActiveAndEnabled && service != null && !SceneTransitionOverlay.IsTransitioning)
            service.TryScan(inspectedId);
    }
    private void Refresh()
    {
        if(inspectedId==null||lastSelectedId!=service.SelectedSectorId)inspectedId=service.SelectedSectorId;
        lastSelectedId=service.SelectedSectorId;
        graph.Find("BUNKER").GetComponentInChildren<TMP_Text>(true).text=L("map.home");
        var path=FindRoute(inspectedId);
        var highlighted=new HashSet<(string,string)>();
        for(int i=0;i<path.Count;i++)highlighted.Add((i==0?null:path[i-1],path[i]));
        foreach (var pair in nodes)
        {
            var state=service.GetStatus(pair.Key); bool selected=pair.Key==inspectedId;
            bool locked=state==SurfaceSectorStatus.Locked;
            // Locked coordinates have neither a visible landmark, a label, content intel nor a hit target.
            pair.Value.gameObject.SetActive(!locked);
            pair.Value.interactable=!locked;
            var feedback=pair.Value.GetComponent<SurfaceMapNodeFeedback>(); feedback.Selected=selected; feedback.Available=!locked;
            pair.Value.transform.Find("Selection").gameObject.SetActive(selected);
            pair.Value.GetComponentInChildren<TMP_Text>(true).color=selected?new Color(.88f,.89f,.81f):new Color(.73f,.77f,.71f);
            pair.Value.transform.Find("Landmark").GetComponent<SurfaceMapPlate>().color=selected?new Color(.64f,.71f,.65f):new Color(.34f,.4f,.38f);
            pair.Value.transform.Find("State").gameObject.SetActive(state==SurfaceSectorStatus.Completed);
            var config=service.Content.Resolve(renderedMap.Find(pair.Key).BuildRunConfig(renderedMap.Id));
            bool hasMarker=service.Content.TryGetMarker(pair.Key,out var marker)&&marker.Marker!=null;
            RefreshIndicators(pair.Key,config,hasMarker,locked);
            var art=Art(pair.Key);
            if(art?.Fog!=null)art.Fog.gameObject.SetActive(fog==null && locked);
        }
        foreach(var route in routes)
        {
            var end=service.GetStatus(route.To);
            var from=route.From==null?SurfaceSectorStatus.Completed:service.GetStatus(route.From);
            bool complete=end==SurfaceSectorStatus.Completed&&from==SurfaceSectorStatus.Completed;
            bool fromKnown=from!=SurfaceSectorStatus.Locked, toKnown=end!=SurfaceSectorStatus.Locked;
            bool full=fromKnown&&toKnown;
            bool active=full && (highlighted.Contains((route.From,route.To))||highlighted.Contains((route.To,route.From)));
            if(route.Artwork!=null)
            {
                route.Image.sprite=full?route.Artwork.Sprite:
                    fromKnown?(route.Reversed?route.Artwork.HintTo:route.Artwork.HintFrom):
                    toKnown?(route.Reversed?route.Artwork.HintFrom:route.Artwork.HintTo):null;
                route.Image.gameObject.SetActive(route.Image.sprite!=null);
            }
            else route.Image.gameObject.SetActive(full); // No fallback line may expose an unknown route.
            route.Image.color=active?new Color(.38f,.365f,.3f):complete?new Color(.27f,.31f,.28f):new Color(.227f,.259f,.255f);
        }
        if(fog!=null)
        {
            var known=new List<Vector2>{Position("BUNKER")};
            var knownRoads=new List<(Vector2,Vector2)>();
            foreach(var pair in nodes)if(pair.Value.gameObject.activeSelf)known.Add(Position(pair.Key));
            foreach(var route in routes)
                if(service.GetStatus(route.To)!=SurfaceSectorStatus.Locked &&
                    (route.From==null || service.GetStatus(route.From)!=SurfaceSectorStatus.Locked))
                    knownRoads.Add((Position(route.From??"BUNKER"),Position(route.To)));
            fog.Reveal(known,knownRoads);
        }
        RefreshCard();
    }
    private void RefreshIndicators(string id,RunConfig config,bool hasMarker,bool locked)
    {
        var visible=new List<SurfaceMapGlyph>();
        foreach(var pair in indicators[id])
        {
            bool show=!locked && (pair.Key switch
            {
                SurfaceMapMeaning.Reward => SurfaceMapBriefing.HasRewardBonus(config),
                SurfaceMapMeaning.Anomaly => SurfaceMapBriefing.AnomalyBias(config).Count>0,
                SurfaceMapMeaning.Event => hasMarker,
                _ => config.WorldRule!=null && config.WorldRule.RuleType!=WorldRuleType.None
            });
            pair.Value.gameObject.SetActive(show);
            if(show)visible.Add(pair.Value);
        }
        for(int i=0;i<visible.Count;i++)
            visible[i].rectTransform.anchoredPosition=new Vector2((i-(visible.Count-1)*.5f)*28,-54);
    }
    private void RefreshCard()
    {
        var sector=renderedMap.Find(inspectedId);
        bool locked=sector==null||service.GetStatus(inspectedId)==SurfaceSectorStatus.Locked;
        bool ready=!locked&&service.SelectedSectorId==inspectedId;
        ready &= sector != null && sector.FinalChallengeReady;
        startButton.interactable=ready&&!SceneTransitionOverlay.IsTransitioning&&starter!=null&&!starter.IsTransitioning;
        var deployLabel=startButton.GetComponentInChildren<TMP_Text>();
        deployLabel.color=startButton.interactable?Selected:Muted;
        deployLabel.text=L(locked?"map.route_locked":"map.launch");
        scanButton.gameObject.SetActive(sector!=null&&service.HasConcealedIntel(inspectedId));
        scanButton.interactable=service.CanScan(inspectedId)&&!SceneTransitionOverlay.IsTransitioning;
        scanButton.GetComponentInChildren<TMP_Text>().text=L("map.scan");
        if(sector==null) { selectionLabel.text=L("map.choose"); details.text=""; return; }
        selectionLabel.text=L(sector.DisplayName);
        var config=service.Content.Resolve(sector.BuildRunConfig(renderedMap.Id));
        int row=0;
        string drivers=SurfaceMapBriefing.RiskDrivers(config);
        AddFact(ref row,SurfaceMapMeaning.Reward,SurfaceMapBriefing.Rewards(config));
        if (sector.IsEscapeFinal)
            AddFact(ref row,SurfaceMapMeaning.Event,L(sector.FinalChallengeReady ? "escape.final.briefing" : "escape.final.unavailable"));
        if(!string.IsNullOrEmpty(drivers))AddFact(ref row,SurfaceMapMeaning.Danger,drivers);
        foreach(string bias in SurfaceMapBriefing.AnomalyBias(config))
            AddFact(ref row,SurfaceMapMeaning.Anomaly,bias);
        string text=SurfaceMapBriefing.Rewards(config);
        if(service.HasConcealedIntel(inspectedId))
        {
            AddFact(ref row,SurfaceMapMeaning.Event,L("map.scan_hint")); text+="\n"+L("map.scan_hint");
        }
        else if(service.Content.TryGetMarker(inspectedId,out var marker))
        {
            string title=L(!string.IsNullOrEmpty(marker.Status)?marker.Status:marker.Title);
            AddFact(ref row,SurfaceMapMeaning.Event,L("map.event")+": "+title); text+="\n"+title;
            if(!string.IsNullOrEmpty(marker.Reward))
            {
                AddFact(ref row,SurfaceMapMeaning.Reward,L(marker.Reward)); text+="\n"+L(marker.Reward);
            }
        }
        if(config.WorldRule!=null && config.WorldRule.RuleType!=WorldRuleType.None)
        {
            AddFact(ref row,SurfaceMapMeaning.Rule,SurfaceMapBriefing.Format("map.rule",Compact(config.WorldRule.DisplayName)));
            text+="\n"+SurfaceMapBriefing.Format("map.initial_condition",config.WorldRule.DisplayName);
        }
        for(int i=row;i<facts.Count;i++)facts[i].Root.SetActive(false);
        details.text=text;
        details.gameObject.SetActive(false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        PositionCard();
    }
    private static string Compact(string title)
    {
        int colon=title.IndexOf(':'); string value=(colon>=0?title.Substring(colon+1):title).Trim();
        return value.Length>1 && value==value.ToUpperInvariant()
            ? char.ToUpperInvariant(value[0])+value.Substring(1).ToLowerInvariant() : value;
    }
    private void AddFact(ref int index,SurfaceMapMeaning meaning,string text)
    {
        if(index==facts.Count)
        {
            var root=new GameObject("Fact "+index,typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(LayoutElement));
            root.layer=card.gameObject.layer; root.transform.SetParent(card,false);
            root.transform.SetSiblingIndex(selectionLabel.transform.GetSiblingIndex()+1+index);
            var layout=root.GetComponent<HorizontalLayoutGroup>(); layout.spacing=10; layout.childAlignment=TextAnchor.UpperLeft;
            layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            var iconObject=new GameObject("Symbol",typeof(RectTransform),typeof(SurfaceMapGlyph),typeof(LayoutElement));
            iconObject.layer=root.layer; iconObject.transform.SetParent(root.transform,false);
            var iconLayout=iconObject.GetComponent<LayoutElement>();
            iconLayout.minWidth=iconLayout.minHeight=iconLayout.preferredWidth=iconLayout.preferredHeight=18;
            var label=Label("Value",root.transform,Vector2.zero,Vector2.zero,"",13,Muted);
            label.textWrappingMode=TextWrappingModes.Normal; label.lineSpacing=0;
            var labelLayout=label.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth=labelLayout.preferredWidth=0; labelLayout.flexibleWidth=1;
            facts.Add(new FactRow{Root=root,Icon=iconObject.GetComponent<SurfaceMapGlyph>(),Label=label,Layout=root.GetComponent<LayoutElement>()});
        }
        var row=facts[index++]; row.Root.SetActive(true); row.Icon.Show(meaning);
        row.Label.text=text; row.Label.color=SurfaceMapGlyph.Tint(meaning);
        row.Layout.preferredHeight=Mathf.Max(20,row.Label.GetPreferredValues(text,card.rect.width-60,0).y);
    }
    private void LateUpdate() { if(service!=null) PositionCard(); }
    private void PositionCard()
    {
        if(card==null || inspectedId==null || !nodes.TryGetValue(inspectedId,out var node))return;
        var parent=(RectTransform)card.parent;
        Vector2 point=parent.InverseTransformPoint(node.transform.position);
        float scale=Mathf.Min(parent.rect.width/1280f,parent.rect.height/800f);
        card.localScale=Vector3.one*scale;
        float width=card.rect.width*scale,height=card.rect.height*scale;
        Rect Bounds(Transform target)
        {
            var value=RectTransformUtility.CalculateRelativeRectTransformBounds(parent,target);
            return Rect.MinMaxRect(value.min.x,value.min.y,value.max.x,value.max.y);
        }
        var focus=Bounds(node.transform); float gap=18*scale;
        Vector2[] candidates={new(focus.xMax+gap,point.y+height*.5f),new(focus.xMin-width-gap,point.y+height*.5f),
            new(point.x-width*.5f,focus.yMax+height+gap),new(point.x-width*.5f,focus.yMin-gap-36*scale)};
        float best=float.PositiveInfinity; Vector2 chosen=Vector2.zero;
        foreach(var candidate in candidates)
        {
            var p=new Vector2(Mathf.Clamp(candidate.x,parent.rect.xMin+20,parent.rect.xMax-width-20),
                Mathf.Clamp(candidate.y,parent.rect.yMin+height+20,parent.rect.yMax-20));
            var area=new Rect(p.x,p.y-height,width,height); float score=Vector2.Distance(area.center,point)*.01f;
            foreach(Transform child in graph)
            {
                if(!child.gameObject.activeInHierarchy || child.Find("Landmark")==null)continue;
                var occupied=Bounds(child);
                score+=Mathf.Max(0,Mathf.Min(area.xMax,occupied.xMax)-Mathf.Max(area.xMin,occupied.xMin))*
                    Mathf.Max(0,Mathf.Min(area.yMax,occupied.yMax)-Mathf.Max(area.yMin,occupied.yMin));
            }
            if(score<best){best=score;chosen=p;}
        }
        card.anchoredPosition=chosen;
    }
    // Presentation traversal: prefer an established route, falling back to a planned route for locked inspection.
    public List<string> FindRoute(string target)
    {
        if (renderedMap.Find(target)?.IsEscapeFinal == true) return new List<string> { target };
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
        if(candidates.Count>0)return SurfaceMapBriefing.Format("map.unlock_after",string.Join(" / ",candidates));
        var path=FindRoute(id);
        for(int i=path.Count-2;i>=0;i--)
            if(service.GetStatus(path[i])!=SurfaceSectorStatus.Completed)return SurfaceMapBriefing.Format("map.unlock_after",path[i]);
        return L("map.unlock_connected");
    }
    private TMP_Text Label(string name,Transform parent,Vector2 position,Vector2 size,string value,float fontSize,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.layer=parent.gameObject.layer; go.transform.SetParent(parent,false);
        var label=go.GetComponent<TMP_Text>(); label.font=details.font; label.fontSharedMaterial=details.fontSharedMaterial;
        Place(label.rectTransform,position,size); label.text=value; label.fontSize=fontSize; label.color=color;
        label.raycastTarget=false; label.alignment=TextAlignmentOptions.Left; return label;
    }
    private static void Place(RectTransform rect,Vector2 position,Vector2 size)
    { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=position; rect.sizeDelta=size; }
    private void StartSelected()
    {
        if (service != null && isActiveAndEnabled && startButton.interactable)
            taskPicker.Show(service, () => starter.StartSurfaceRun(transitionTarget));
    }
    private void Close() { if(owner!=null)owner.CloseAll(); else gameObject.SetActive(false); }
}




