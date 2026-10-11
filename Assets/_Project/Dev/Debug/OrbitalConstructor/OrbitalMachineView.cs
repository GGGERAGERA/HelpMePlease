#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace Subject42.DebugLabs.Constructor
{
    public sealed class OrbitalMachineView : System.IDisposable
    {
        public readonly Transform Root;
        private readonly Sprite block, blade, saw;
        private readonly Texture2D blockTexture, bladeTexture, sawTexture;
        private readonly Material guideMaterial;
        private Transform parts, preview, mounts, guides, effects;
        private readonly List<Transform> saws=new();
        private readonly List<(Transform root,float end)> sparks=new();
        private SpriteRenderer bridge;
        private readonly Dictionary<int,List<SpriteRenderer>> renderers = new();
        public OrbitalMachineView(Transform owner)
        {
            Root = new GameObject("Rigid orbital machine").transform;
            Root.SetParent(owner,false);
            blockTexture = Texture(false); bladeTexture = Texture(true);
            block = Sprite.Create(blockTexture,new Rect(0,0,32,8),new Vector2(.5f,.5f),32);
            blade = Sprite.Create(bladeTexture,new Rect(0,0,32,8),new Vector2(.5f,.5f),32);
            sawTexture=SawTexture(); saw=Sprite.Create(sawTexture,new Rect(0,0,64,64),new Vector2(.5f,.5f),64);
            guideMaterial=new Material(Shader.Find("Sprites/Default"));
            parts = Child("Mechanical tree",Root); mounts = Child("Free compatible mounts",Root);
            preview = Child("Placement preview",Root);
            guides=Child("Visual trajectories only (no colliders)",Root);
            effects=Child("Short contact sparks",owner);
            bridge = Bar("Energy bridge",Root,Vector2.zero,Vector2.right,.1f,new Color(.2f,1,.85f));
            bridge.enabled = false;
            Bar("Core anchor",Root,Vector2.zero,new Vector2(.8f,0),.13f,new Color(.48f,.6f,.65f));
            Joint(Root,Vector2.zero,new Color(.2f,1,.8f),.24f);
        }
        private static Transform Child(string name,Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent,false); return t; }
        private static Texture2D Texture(bool sharp)
        {
            var t = new Texture2D(32,8,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int x=0;x<32;x++) for (int y=0;y<8;y++)
            {
                bool empty = sharp && (y < 1+(x>24 ? (x-24)/3 : 0) || y > 6-(x>24 ? (x-24)/3 : 0));
                bool rim = y==0 || y==7 || x==0 || x==31;
                bool bolt = !sharp && (x==4 || x==27) && (y==3 || y==4);
                t.SetPixel(x,y,empty ? Color.clear : rim ? new Color(.21f,.25f,.31f) : bolt ? new Color(1,1,1) : y>4 ? new Color(.8f,.85f,.92f) : Color.white);
            }
            t.Apply(); return t;
        }
        private static Texture2D SawTexture()
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            for (int x=0;x<64;x++) for (int y=0;y<64;y++)
            {
                Vector2 p=new Vector2(x-31.5f,y-31.5f)/31.5f;
                float radius=p.magnitude;
                float angle=Mathf.Atan2(p.y,p.x);
                float tooth=Mathf.Repeat(angle/(Mathf.PI*2)*16,1);
                float edge=tooth<.56f ? 1 : .88f;
                Color color=Color.clear;
                if (radius<=edge)
                {
                    color=radius>edge-.055f ? new Color(.15f,.22f,.29f) : new Color(.8f,.89f,.96f);
                    if (radius<.73f) color=new Color(.43f,.56f,.67f);
                    if (radius>.25f && radius<.72f && Mathf.Abs(Mathf.Sin(angle*4))<.17f) color=new Color(.24f,.33f,.43f);
                    if (radius<.23f) color=new Color(1,.68f,.24f);
                    if (radius<.09f) color=new Color(.2f,.28f,.35f);
                }
                texture.SetPixel(x,y,color);
            }
            texture.Apply(); return texture;
        }
        private SpriteRenderer Bar(string name,Transform parent,Vector2 a,Vector2 b,float width,Color color,bool sharp=false)
        {
            var renderer = Child(name,parent).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sharp ? blade : block;
            renderer.sortingLayerName = "Player"; renderer.sortingOrder = 60;
            renderer.color = color; SetBar(renderer,a,b,width); return renderer;
        }
        private static void SetBar(SpriteRenderer renderer,Vector2 a,Vector2 b,float width)
        {
            renderer.transform.localPosition = (a+b)*.5f;
            renderer.transform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            renderer.transform.localScale = new Vector3(Vector2.Distance(a,b),width*4,1);
        }
        private SpriteRenderer Joint(Transform parent,Vector2 position,Color color,float size=.18f) =>
            Bar("Riveted mount",parent,position-new Vector2(size*.5f,0),position+new Vector2(size*.5f,0),size,color);
        private static void Clear(Transform parent)
        {
            for (int i=parent.childCount-1;i>=0;i--)
            { var go=parent.GetChild(i).gameObject; go.SetActive(false); Object.Destroy(go); }
        }
        private List<SpriteRenderer> DrawNode(Transform parent,AssemblyNode node,Color tint)
        {
            var def=OrbitalPartCatalog.Get(node.Kind);
            var list=new List<SpriteRenderer>();
            foreach (var segment in def.Segments)
                list.Add(Bar(node.Kind+" #"+node.Id,parent,node.Point(segment.A),node.Point(segment.B),segment.Width,tint,segment.Role==SegmentRole.Contact));
            if (def.DiskRadius>0)
            {
                var disk=Child("Saw disk / working radius "+def.DiskRadius,parent).gameObject.AddComponent<SpriteRenderer>();
                disk.sprite=saw; disk.color=tint; disk.sortingLayerName="Player"; disk.sortingOrder=65;
                disk.transform.localPosition=node.Point(def.WorkingCenter); disk.transform.localScale=Vector3.one*(def.DiskRadius*2);
                if (parent==parts) saws.Add(disk.transform);
                list.Add(disk);
            }
            list.Add(Joint(parent,node.Position,new Color(.75f,.81f,.86f,tint.a)));
            foreach (var output in def.Outputs) list.Add(Joint(parent,node.Point(output.Position),tint));
            if (node.Kind==PartKind.Electrode) list.Add(Joint(parent,node.Point(def.Terminal),tint,.29f));
            if (node.Kind==PartKind.Hook) list.Add(Joint(parent,node.Point(def.Terminal),tint,.13f));
            return list;
        }
        public void Rebuild(OrbitalAssembly assembly)
        {
            Clear(parts); Clear(guides); Clear(effects); sparks.Clear(); saws.Clear(); renderers.Clear();
            foreach (var node in assembly.Nodes)
            {
                renderers[node.Id]=DrawNode(parts,node,OrbitalPartCatalog.Get(node.Kind).Color);
                AddTrajectory(guides,node,new Color(.3f,.75f,1,.3f));
            }
            Bridge(assembly,false);
        }
        private void Circle(Transform parent,float radius,float width,Color color)
        {
            if (radius<=.01f) return;
            var line=Child("Trajectory radius "+radius,parent).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial=guideMaterial; line.useWorldSpace=false; line.loop=true; line.widthMultiplier=width;
            line.startColor=line.endColor=color; line.sortingLayerName="Player"; line.sortingOrder=15; line.positionCount=128;
            for (int i=0;i<128;i++) { float angle=i*Mathf.PI*2/128; line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0)); }
        }
        private void AddTrajectory(Transform parent,AssemblyNode node,Color color)
        {
            var definition=OrbitalPartCatalog.Get(node.Kind);
            if (definition.DiskRadius<=0) return;
            float radius=node.Point(definition.WorkingCenter).magnitude;
            Circle(parent,radius,.019f,color);
            color.a*=.6f;
            Circle(parent,radius-definition.DiskRadius,.011f,color);
            Circle(parent,radius+definition.DiskRadius,.011f,color);
        }
        public void Animate(bool showGuides)
        {
            guides.gameObject.SetActive(showGuides);
            foreach (var disk in saws) if (disk!=null) disk.Rotate(0,0,-700*Time.deltaTime);
            for (int i=sparks.Count-1;i>=0;i--)
                if (Time.time>=sparks[i].end) { Object.Destroy(sparks[i].root.gameObject); sparks.RemoveAt(i); }
        }
        public void Impact(Vector2 position)
        {
            var burst=Child("Actual saw contact / 0.12 s",effects);
            burst.position=position;
            for (int i=0;i<4;i++)
            {
                Vector2 direction=OrbitalPartCatalog.Rotate(Vector2.right,25+i*90);
                Bar("Spark",burst,direction*.04f,direction*.23f,.035f,new Color(1,.85f,.4f));
            }
            sparks.Add((burst,Time.time+.12f));
        }
        public void ClearImpacts() { Clear(effects); sparks.Clear(); }
        public void Highlight(OrbitalAssembly assembly,int selected)
        {
            foreach (var node in assembly.Nodes)
                if (renderers.TryGetValue(node.Id,out var list))
                    foreach (var renderer in list) renderer.color = node.Id==selected ? new Color(1,.8f,.25f) : OrbitalPartCatalog.Get(node.Kind).Color;
        }
        public void ShowMounts(OrbitalAssembly assembly,PartKind kind,float orientation,bool editing)
        {
            Clear(mounts);
            if (!editing) return;
            foreach (var mount in assembly.FreeMounts())
            {
                bool valid=assembly.Preview(kind,mount.parent,mount.port,orientation,out _,out _);
                Joint(mounts,mount.mount.Position,valid ? new Color(.3f,1,.5f) : new Color(.65f,.32f,.32f),.25f);
            }
        }
        public void Preview(AssemblyNode node,bool valid)
        {
            Clear(preview);
            if (node!=null)
            {
                Color color=valid ? new Color(.35f,1,.55f,.6f) : new Color(1,.25f,.3f,.55f);
                DrawNode(preview,node,color); AddTrajectory(preview,node,color);
            }
        }
        public void Bridge(OrbitalAssembly assembly,bool running)
        {
            bridge.enabled=assembly.Bridge(out var a,out var b);
            if (!bridge.enabled) return;
            SetBar(bridge,a,b,running ? .10f+.035f*Mathf.Sin(Time.time*23) : .045f);
            bridge.color=running ? new Color(.3f,1,.88f) : new Color(.3f,.7f,.65f,.5f);
        }
        public void Dispose()
        {
            if (Root!=null) Object.Destroy(Root.gameObject);
            if (effects!=null) Object.Destroy(effects.gameObject);
            Object.Destroy(block); Object.Destroy(blade); Object.Destroy(saw); Object.Destroy(blockTexture); Object.Destroy(bladeTexture); Object.Destroy(sawTexture); Object.Destroy(guideMaterial);
        }
    }
}
#endif
