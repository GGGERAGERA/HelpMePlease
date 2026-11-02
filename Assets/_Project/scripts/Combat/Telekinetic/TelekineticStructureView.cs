using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Subject42.Combat.Telekinetic
{
    // Presentation only. Geometry is a derived snapshot of Build, never a second placement model.
    // Two batched meshes share the authored sprite material; no per-cell physics or Update components.
    [DisallowMultipleComponent]
    public sealed class TelekineticStructureView : MonoBehaviour
    {
        [Header("Segmented psi chassis")]
        [Min(.08f)] public float SegmentPitch=.17f;
        [Range(.1f,.35f)] public float SpineWidth=.21f;
        [Range(0,.4f)] public float PulseStrength=.18f;
        [Min(.1f)] public float PulseSpeed=2.4f;
        public Color Metal=new(.075f,.12f,.16f,1);
        public Color Bevel=new(.19f,.28f,.34f,1);
        public Color Cyan=new(.2f,.9f,1,1);
        public Color Teal=new(.16f,.76f,.67f,1);
        public Color Violet=new(.65f,.4f,1,1);
        public int RebuildVersion { get; private set; }
        public int EdgeCount { get; private set; }
        public int SocketCount { get; private set; }
        public bool CombatVisible => root!=null && root.activeSelf;

        private sealed class Geometry
        {
            internal readonly List<Vector3> Vertices=new();
            internal readonly List<int> Triangles=new();
            internal readonly List<Color> Colors=new();
            internal readonly List<Vector2> Uvs=new();
            internal void Clear() { Vertices.Clear(); Triangles.Clear(); Colors.Clear(); Uvs.Clear(); }
            internal void Quad(Vector2 a,Vector2 b,float width,Color color)
            {
                Vector2 side=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
                int start=Vertices.Count;
                Vertices.Add(a-side); Vertices.Add(a+side); Vertices.Add(b+side); Vertices.Add(b-side);
                for (int i=0;i<4;i++) { Colors.Add(color); Uvs.Add(Vector2.zero); }
                Triangles.Add(start); Triangles.Add(start+1); Triangles.Add(start+2);
                Triangles.Add(start); Triangles.Add(start+2); Triangles.Add(start+3);
            }
            internal void Disk(Vector2 center,float radius,Color color,int sides=8)
            {
                int start=Vertices.Count; Vertices.Add(center); Colors.Add(color); Uvs.Add(Vector2.zero);
                for (int i=0;i<=sides;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2/sides;
                    Vertices.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius); Colors.Add(color); Uvs.Add(Vector2.zero);
                    if (i>0) { Triangles.Add(start); Triangles.Add(start+i); Triangles.Add(start+i+1); }
                }
            }
            internal void Upload(Mesh mesh)
            {
                mesh.Clear(); mesh.SetVertices(Vertices); mesh.SetUVs(0,Uvs); mesh.SetColors(Colors);
                mesh.SetTriangles(Triangles,0); mesh.RecalculateBounds();
            }
        }
        private struct Pulse
        {
            internal int First,Count;
            internal float Phase;
            internal TelekineticItem Cell;
        }
        private readonly Geometry metal=new(),energy=new();
        private readonly List<Pulse> pulses=new();
        private readonly HashSet<Vector2Int> occupied=new();
        private ITelekineticStructureOwner lab;
        private GameObject root;
        private Mesh chassisMesh,energyMesh;
        private Color[] animatedColors;

        public void Bind(ITelekineticStructureOwner owner,Transform construction)
        {
            lab=owner;
            root=new GameObject("Telekinetic chassis / visual only"); root.transform.SetParent(construction,false);
            chassisMesh=CreateLayer("Dark segmented metal",-8);
            energyMesh=CreateLayer("Psi channels and socket glow",-7); energyMesh.MarkDynamic();
            SetCombatVisible(!owner.Editing);
        }
        private Mesh CreateLayer(string label,int order)
        {
            var go=new GameObject(label); go.transform.SetParent(root.transform,false);
            var mesh=new Mesh { name=label, indexFormat=IndexFormat.UInt32 };
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=lab.SpriteMaterial;
            renderer.sortingLayerName="Player"; renderer.sortingOrder=order;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return mesh;
        }
        public void SetCombatVisible(bool value) { if (root!=null) root.SetActive(value && enabled); }
        public bool HasEdge(Vector2Int a,Vector2Int b) => occupied.Contains(a) && occupied.Contains(b) &&
            TelekineticBuild.Adjacent(a,b) && lab.Build.At(a)!=lab.Build.At(b);

        public void Rebuild()
        {
            metal.Clear(); energy.Clear(); pulses.Clear(); occupied.Clear();
            EdgeCount=SocketCount=0; occupied.Add(Vector2Int.zero);
            foreach (var item in lab.Build.Items) for (int i=0;i<item.CellCount;i++) occupied.Add(item.Cell(i));
            foreach (var p in occupied)
            {
                // Generate each undirected edge once, including all cycles and core connections.
                foreach (var direction in new[]{Vector2Int.right,Vector2Int.up})
                    if (HasEdge(p,p+direction)) { Branch(p,p+direction); EdgeCount++; }
            }
            Core();
            foreach (var item in lab.Build.Items)
            {
                int mask=0;
                for (int i=0;i<item.CellCount;i++) for (int side=0;side<4;side++)
                    if (HasEdge(item.Cell(i),item.Cell(i)+TelekineticBuild.Sides[side])) mask|=1<<side;
                if (item.CellCount>1) FootprintPlate(item);
                Socket(item,mask); SocketCount++;
            }
            metal.Upload(chassisMesh); energy.Upload(energyMesh);
            animatedColors=energy.Colors.ToArray(); RebuildVersion++;
            SetCombatVisible(!lab.Editing);
        }
        private void FootprintPlate(TelekineticItem item)
        {
            float half=lab.CellSpacing*.5f,padding=.07f;
            Color tint=TelekineticRig.FrameTint(item.Kind);
            int first=energy.Vertices.Count;
            for (int i=0;i<item.CellCount;i++)
            {
                var cell=item.Cell(i); Vector2 p=(Vector2)cell*lab.CellSpacing;
                bool left=item.Contains(cell+Vector2Int.left),right=item.Contains(cell+Vector2Int.right),
                    up=item.Contains(cell+Vector2Int.up),down=item.Contains(cell+Vector2Int.down);
                Vector2 min=p-new Vector2(half,half)+new Vector2(left?0:padding,down?0:padding);
                Vector2 max=p+new Vector2(half,half)-new Vector2(right?0:padding,up?0:padding);
                metal.Quad(new Vector2(min.x,(min.y+max.y)*.5f),new Vector2(max.x,(min.y+max.y)*.5f),max.y-min.y,TelekineticRig.PlateTint(item.Kind));
                void Border(Vector2 a,Vector2 b)
                {
                    metal.Quad(a,b,item.IsWeapon?.085f:.045f,tint); Glow(a,b,item.IsWeapon?.023f:.012f,tint);
                }
                if (!left) Border(min,new Vector2(min.x,max.y));
                if (!right) Border(new Vector2(max.x,min.y),max);
                if (!up) Border(new Vector2(min.x,max.y),max);
                if (!down) Border(min,new Vector2(max.x,min.y));
            }
            PulseRange(first,item.Center.magnitude*1.5f,item);
        }
        private void Branch(Vector2Int from,Vector2Int to)
        {
            Vector2 a=(Vector2)from*lab.CellSpacing,b=(Vector2)to*lab.CellSpacing;
            bool armed=lab.Build.At(from)?.IsWeapon==true || lab.Build.At(to)?.IsWeapon==true;
            Color channel=armed?Cyan:new Color(.42f,.48f,.55f,.38f);
            Vector2 axis=(b-a).normalized,side=new(-axis.y,axis.x);
            metal.Quad(a,b,SpineWidth*.72f,Metal);
            int count=Mathf.Max(2,Mathf.CeilToInt(lab.CellSpacing/SegmentPitch));
            for (int i=0;i<count;i++)
            {
                Vector2 point=Vector2.Lerp(a,b,(i+.5f)/count);
                float length=lab.CellSpacing/count*.7f;
                metal.Quad(point-axis*length*.5f,point+axis*length*.5f,SpineWidth,Bevel);
                metal.Quad(point-axis*length*.36f,point+axis*length*.36f,SpineWidth*.65f,Metal);
                Color tint=channel;
                int first=energy.Vertices.Count;
                Glow(point-axis*length*.45f,point+axis*length*.45f,.027f,tint);
                energy.Quad(point+side*SpineWidth*.38f-axis*.013f,point+side*SpineWidth*.38f+axis*.013f,.042f,channel);
                energy.Quad(point-side*SpineWidth*.38f-axis*.013f,point-side*SpineWidth*.38f+axis*.013f,.042f,channel);
                PulseRange(first,point.magnitude*1.5f);
            }
        }
        private void Core()
        {
            Vector2 p=Vector2.zero;
            metal.Disk(p,.65f,Bevel); metal.Disk(p,.58f,Metal);
            Ring(metal,p,.49f,.045f,new Color(.26f,.39f,.43f),8,0,.98f);
            int first=energy.Vertices.Count;
            Ring(energy,p,.61f,.18f,Alpha(Cyan,.045f),32);
            Ring(energy,p,.61f,.095f,Alpha(Cyan,.09f),32);
            Ring(energy,p,.61f,.023f,Cyan,16,0,.8f);
            Ring(energy,p,.44f,.016f,Teal,8,22.5f,.65f);
            for (int i=0;i<4;i++)
            {
                Vector2 axis=Quaternion.Euler(0,0,i*90)*Vector2.right;
                Glow(axis*.54f,axis*.71f,.035f,i%2==0?Cyan:Violet);
            }
            PulseRange(first,0);
        }
        private void Socket(TelekineticItem cell,int mask)
        {
            Vector2 p=cell.Center*lab.CellSpacing;
            int degree=0; for (int i=0;i<4;i++) if ((mask&(1<<i))!=0) degree++;
            bool frame=cell.Kind==TelekineticPart.Frame, power=cell.Kind==TelekineticPart.Energy;
            float radius=frame ? (degree>=3?.21f:.15f) : cell.IsWeapon ? .43f : power ? .34f : .3f;
            Color tint=TelekineticRig.FrameTint(cell.Kind);
            metal.Disk(p,radius+.045f,tint); metal.Disk(p,radius,TelekineticRig.PlateTint(cell.Kind));
            int first=energy.Vertices.Count;
            Ring(energy,p,radius+.02f,.13f,Alpha(tint,cell.IsWeapon?.065f:.015f),16);
            Ring(energy,p,radius+.02f,.06f,Alpha(tint,cell.IsWeapon?.16f:.035f),16);
            Ring(energy,p,radius,.018f,tint,8,22.5f,.68f);
            for (int i=0;i<4;i++)
            {
                Vector2 axis=TelekineticBuild.Sides[i],side=new(-axis.y,axis.x);
                if ((mask&(1<<i))!=0)
                {
                    // Reinforced ports reveal the actual junction beneath the socket, rather than fake radial spokes.
                    metal.Quad(p+axis*(radius-.06f),p+axis*(radius+.09f),.11f,tint);
                    Glow(p+axis*(radius-.03f),p+axis*(radius+.12f),.028f,tint);
                    if (frame) Glow(p,p+axis*radius,.02f,tint);
                }
                if (cell.IsWeapon)
                {
                    Vector2 bracket=p+axis*(radius-.035f);
                    metal.Quad(bracket-side*.12f,bracket+side*.12f,.095f,Bevel);
                    energy.Quad(bracket-side*.085f,bracket+side*.085f,.017f,tint);
                }
            }
            if (degree==1)
            {
                int side=0; while ((mask&(1<<side))==0) side++;
                Vector2 outward=-(Vector2)TelekineticBuild.Sides[side];
                metal.Quad(p+outward*(radius-.02f),p+outward*(radius+.13f),.15f,tint);
                Glow(p+outward*(radius+.07f),p+outward*(radius+.13f),.045f,tint);
            }
            if (power) Ring(energy,p,radius-.06f,.015f,Violet,4,0,.55f);
            PulseRange(first,p.magnitude*1.5f,cell);
        }
        private static Color Alpha(Color color,float alpha) { color.a=alpha; return color; }
        private void Glow(Vector2 a,Vector2 b,float width,Color color)
        {
            energy.Quad(a,b,width*5,Alpha(color,color.a*.045f)); energy.Quad(a,b,width*2.5f,Alpha(color,color.a*.12f)); energy.Quad(a,b,width,color);
        }
        private static void Ring(Geometry target,Vector2 center,float radius,float width,Color color,int sides,float rotation=0,float fill=1)
        {
            for (int i=0;i<sides;i++)
            {
                float a=(i+(.5f-fill*.5f))*Mathf.PI*2/sides+rotation*Mathf.Deg2Rad;
                float b=(i+(.5f+fill*.5f))*Mathf.PI*2/sides+rotation*Mathf.Deg2Rad;
                target.Quad(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,
                    center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color);
            }
        }
        private void PulseRange(int first,float phase,TelekineticItem cell=null) =>
            pulses.Add(new Pulse { First=first,Count=energy.Vertices.Count-first,Phase=phase,Cell=cell });
        private void LateUpdate()
        {
            if (root==null || !root.activeSelf || animatedColors==null) return;
            foreach (var pulse in pulses)
            {
                float wave=1-PulseStrength*.5f+PulseStrength*.5f*Mathf.Sin(Time.time*PulseSpeed-pulse.Phase);
                // Cooldown is read only. An active module gains a restrained socket shimmer on discharge.
                if (pulse.Cell!=null && pulse.Cell.IsModule)
                    wave+=.1f*Mathf.Clamp01(1-(pulse.Cell.LastCooldown-pulse.Cell.Remaining)/.35f);
                for (int i=pulse.First;i<pulse.First+pulse.Count;i++)
                {
                    Color c=energy.Colors[i]; c.a*=wave; animatedColors[i]=c;
                }
            }
            energyMesh.colors=animatedColors;
        }
        private void OnDisable() { if (root!=null) root.SetActive(false); }
        private void OnEnable() { if (lab!=null) SetCombatVisible(!lab.Editing); }
        private void OnDestroy()
        {
            if (chassisMesh!=null) Destroy(chassisMesh);
            if (energyMesh!=null) Destroy(energyMesh);
            if (root!=null) Destroy(root);
        }
    }
}
