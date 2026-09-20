using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Static authoring utility only. No runtime scripts or character selection logic.
[InitializeOnLoad]
public static class CharacterSelectionPrototype
{
    const string Root = "Assets/Subject42UIPrototype";
    static readonly Color Cyan = Hex("75CFE5"), Muted = Hex("65879E"), White = Hex("D5EAF1"), Purple = Hex("B28AEA");
    static Sprite frame, portrait, lockIcon;
    static Font font;
    static Transform canvas;
    static CharacterSelectionPrototype() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild()
    {
        if (File.Exists(Root + "/GENERATE") && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Root + "/GENERATE");
            Build();
        }
    }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out var c); return c; }

    [MenuItem("Tools/Subject42/Build isolated character UI prototype")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Sprites");
        frame = MakeSprite("Frame9Slice", 24, 24, (x,y) => {
            int d = Mathf.Min(x,y,23-x,23-y);
            if (x+y<6 || x+23-y<6 || 23-x+y<6 || 46-x-y<6) return Color.clear;
            if (d<2 || x+y<8 || x+23-y<8 || 23-x+y<8 || 46-x-y<8) return Color.white;
            return Hex("182D43");
        }, true);
        portrait = MakeSprite("GeraPlaceholder", 48, 48, (x,y) => {
            Color c = Hex("101E33");
            if (x%12==0 || y%12==0) c=Hex("192E44");
            if (x>5 && x<43 && y<15) c=Hex("314C66");
            if (x>9 && x<39 && y<12) c=Hex("24364E");
            if (x>19 && x<29 && y>10 && y<24) c=Hex("9C7980");
            if (x>14 && x<34 && y>20 && y<38) c=Hex("CEAA9A");
            if (x>29 && x<34 && y>20 && y<34) c=Hex("A17C80");
            if (x>12 && x<35 && y>35 && y<42 || x>12 && x<18 && y>29 && y<40) c=Hex("303047");
            if (x>17 && x<33 && y>38 && y<42) c=Hex("55435B");
            if (x>16 && x<22 && y==30) c=Hex("30445B");
            if (x>26 && x<32 && y>28 && y<32) c=Hex("BD89F2");
            if (x>28 && x<31 && y==30) c=Hex("E9D8FF");
            if (x>21 && x<27 && y==23) c=Hex("735761");
            if (x>9 && x<17 && y>4 && y<13 || x>31 && x<39 && y>4 && y<13) c=Hex("4A748A");
            if (x>21 && x<27 && y>3 && y<9) c=Hex("72CEDB");
            return c;
        });
        lockIcon = MakeSprite("LockPlaceholder", 16, 16, (x,y) =>
            (x>=3 && x<=12 && y>=2 && y<=8 && !(x>=7 && x<=8 && y>=4 && y<=6)) ||
            (x>=5 && x<=10 && y>=9 && y<=12 && (x==5 || x==10 || y==12)) ? Color.white : Color.clear);
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/PressStart2P-vaV7.ttf");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var cameraObject = new GameObject("Prototype Camera", typeof(Camera));
        var cam = cameraObject.GetComponent<Camera>();
        cam.transform.position = new Vector3(0,0,-10); cam.orthographic=true;
        cam.orthographicSize=540; cam.clearFlags=CameraClearFlags.SolidColor;
        cam.backgroundColor=Hex("080F1E"); cam.cullingMask=1<<31;
        cam.allowHDR=false; cam.allowMSAA=false;
        var go = new GameObject("Character Selection — VISUAL ONLY", typeof(Canvas), typeof(CanvasScaler));
        canvas=go.transform;
        var cv=go.GetComponent<Canvas>(); cv.renderMode=RenderMode.ScreenSpaceCamera;
        cv.worldCamera=cam; cv.planeDistance=1; cv.pixelPerfect=true;
        var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        Rect("Backdrop",0,0,1920,1080,Hex("080F1E"));
        Label("SUBJECT#42",240,105,500,30,24,Cyan);
        Label("БИОАРХИВ / 03",1240,110,440,24,16,Muted,TextAnchor.MiddleRight);
        Panel("Main metal shell",220,165,1480,770,Hex("51869C"));
        Rect("Header",240,185,1440,100,Hex("14253A"));
        Label("ВЫБОР ПЕРСОНАЖА",390,210,1140,48,32,White,TextAnchor.MiddleCenter);
        Rect("Header rule",260,286,1400,2,Hex("33566F"));
        Label("01 / ОБЪЕКТЫ",270,320,690,24,16,Muted);
        Label("АКТИВНОЕ ДОСЬЕ",1100,320,540,24,16,Muted);
        Rect("Divider",1050,322,2,450,Hex("33566F"));
        Card("ГЕРА",270,true,"01"); Card("ДИМАГ",525,false,"02"); Card("ВИКА",780,false,"03");
        Rect("Roster foot rule",270,678,735,2,Hex("33566F"));
        Label("ДОСТУПЕН 1 / 3",270,704,735,24,16,Cyan);
        Label("ОБРАЗЕЦ СТАБИЛЕН",270,742,735,24,16,Muted);
        Panel("Portrait housing",1100,367,216,216,Cyan);
        Pic("Selected portrait 192 x 192",portrait,1112,379,192,192,Color.white);
        Label("S42—01",1340,376,280,24,16,Cyan);
        Label("ГЕРА",1340,421,280,36,32,White);
        Label("АНОМАЛИЯ",1340,485,280,24,16,Purple);
        Label("ТЕЛЕКИНЕЗ",1340,519,280,24,16,White);
        Label("ГЕРА / ОБЪЕКТ 01",1100,604,540,26,20,White);
        Stat("ЗДОРОВЬЕ", "120",650,5,Cyan);
        Stat("СКОРОСТЬ", "4.5",692,3,Cyan);
        Stat("ПОТЕНЦИАЛ", "85",734,4,Purple);
        Panel("Maximum level strip",270,817,780,68,Hex("6089AE"));
        for(int i=0;i<5;i++) Rect("Max rank tick",290+i*12,839,6,24,Purple);
        Label("МАКСИМАЛЬНЫЙ УРОВЕНЬ",374,833,650,34,20,White);
        Panel("Choose button visual",1100,807,540,88,Cyan);
        Rect("Button inner",1112,819,516,64,Hex("22445B"));
        Label("ВЫБРАТЬ",1112,832,516,38,24,White,TextAnchor.MiddleCenter);
        Label("ЛАБОРАТОРИЯ / SUBJECT#42",240,961,950,24,16,Muted);
        Label("UI STUDY  /  01",1240,961,440,24,16,Muted,TextAnchor.MiddleRight);
        foreach(var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer=31;
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.SaveScene(scene, Root + "/CharacterSelection.unity");
        var rt = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32) { antiAliasing=1 };
        cam.targetTexture=rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var old=RenderTexture.active; RenderTexture.active=rt;
        var shot=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        shot.ReadPixels(new Rect(0,0,1920,1080),0,0); shot.Apply();
        Directory.CreateDirectory("Artifacts/Subject42UIPrototype");
        File.WriteAllBytes("Artifacts/Subject42UIPrototype/CharacterSelection.png",shot.EncodeToPNG());
        RenderTexture.active=old; cam.targetTexture=null;
        Object.DestroyImmediate(shot); rt.Release(); Object.DestroyImmediate(rt);
        EditorSceneManager.CloseScene(scene,true);
        if(previous.IsValid()) SceneManager.SetActiveScene(previous);
        Debug.Log("Subject42 UI prototype: scene and 1920x1080 screenshot generated.");
    }
    static void Stat(string title,string value,int y,int level,Color color)
    {
        Label(title,1100,y,310,24,16,Muted);
        for(int i=0;i<5;i++) Rect(title+" meter",1400+i*24,y+5,16,12,i<level?color:Hex("253A50"));
        Label(value,1530,y,110,24,16,White,TextAnchor.MiddleRight);
    }
    static void Card(string name,int x,bool selected,string number)
    {
        Panel(name+" card",x,367,225,278,selected?Cyan:Hex("3B586F"));
        Label(number,x+18,385,120,20,16,selected?Cyan:Muted);
        if(selected) {
            Pic(name+" portrait",portrait,x+32,417,160,160,Color.white);
            Rect("Selection marker",x+177,386,24,6,Purple);
        } else {
            Pic(name+" silhouette",portrait,x+32,417,160,160,Hex("263A50"));
            Pic(name+" lock",lockIcon,x+88,470,48,48,Muted);
        }
        Label(name,x+12,590,201,28,24,selected?White:Muted,TextAnchor.MiddleCenter);
        Label(selected?"ВЫБРАНА":"ЗАБЛОКИРОВАН",x,654,225,18,selected?12:10,selected?Purple:Muted,TextAnchor.MiddleCenter);
    }
    static RectTransform Box(string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(canvas,false);
        var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=new Vector2(0,1);
        r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
    }
    static void Rect(string n,float x,float y,float w,float h,Color c) { var i=Box(n,x,y,w,h).gameObject.AddComponent<Image>(); i.color=c; i.raycastTarget=false; }
    static void Pic(string n,Sprite s,float x,float y,float w,float h,Color c) { var i=Box(n,x,y,w,h).gameObject.AddComponent<Image>(); i.sprite=s; i.color=c; i.raycastTarget=false; }
    static void Panel(string n,float x,float y,float w,float h,Color c) { var i=Box(n,x,y,w,h).gameObject.AddComponent<Image>(); i.sprite=frame; i.type=Image.Type.Sliced; i.color=c; i.raycastTarget=false; }
    static void Label(string s,float x,float y,float w,float h,int size,Color c,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var t=Box(s,x,y,w,h).gameObject.AddComponent<Text>(); t.text=s; t.font=font; t.fontSize=size;
        t.color=c; t.alignment=align; t.raycastTarget=false; t.horizontalOverflow=HorizontalWrapMode.Overflow;
        t.verticalOverflow=VerticalWrapMode.Overflow;
    }
    static Sprite MakeSprite(string name,int w,int h,System.Func<int,int,Color> paint,bool sliced=false)
    {
        string path=Root+"/Sprites/"+name+".png";
        var t=new Texture2D(w,h,TextureFormat.RGBA32,false);
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) t.SetPixel(x,y,paint(x,y));
        t.Apply(); File.WriteAllBytes(path,t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType=TextureImporterType.Sprite; imp.spriteImportMode=SpriteImportMode.Single; imp.spritePixelsPerUnit=100;
        imp.filterMode=FilterMode.Point; imp.mipmapEnabled=false; imp.textureCompression=TextureImporterCompression.Uncompressed;
        imp.spriteBorder=sliced?new Vector4(8,8,8,8):Vector4.zero;
        var settings=new TextureImporterSettings(); imp.ReadTextureSettings(settings); settings.spriteMeshType=SpriteMeshType.FullRect; imp.SetTextureSettings(settings);
        imp.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
