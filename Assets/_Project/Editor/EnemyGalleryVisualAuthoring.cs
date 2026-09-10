using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Reviewed visual selections, authored into the gallery. Never runs during gameplay.</summary>
public static class EnemyGalleryVisualAuthoring
{
    const string VisualAssetsPath = "Assets/_Project/art/EnemyGalleryVisuals.asset";
    sealed class Spec
    {
        public readonly string Id, Name, Path;
        public readonly Rect Crop;
        public Spec(string id, string name, string path, Rect crop = default)
        { Id = id; Name = name; Path = path; Crop = crop; }
    }
    // One entry per reviewed design, not per frame. A crop is normalized from the TOP LEFT.
    // Extend this list after visually reviewing new sheets; guessing identity from filenames loses designs.
    static readonly Spec[] Selections = {
        new Spec("cloud-blob", "Cloud blob (ambiguous)", "Assets/_Project/art/cloud_tileset/rpgmaker_1/cloudcity_chars_1.png", crop: new Rect(0,.5f,1f/12f,1f/16f)),
        new Spec("hood-gera", "Gera hooded chibi", "Assets/_Project/art/Sprites/Aseprite/newCharacters2/Gera2.aseprite"),
        new Spec("demon-girl", "Horned demon girl", "Assets/_Project/art/Sprites/testSprites/TestGirl1.psb"),
        new Spec("art-31", "Schoolgirl prototype", "Assets/_Project/art/Sprites/Aseprite/Girl2Ase1.png", crop: new Rect(0.25000000f,0.00000000f,0.25000000f,0.25000000f)),
        new Spec("art-32", "Red-haired scout", "Assets/_Project/art/Sprites/Aseprite/Girl3Ase1.png", crop: new Rect(0.00000000f,0.00000000f,0.12500000f,0.12500000f)),
        new Spec("art-35", "Gera early coat", "Assets/_Project/art/Sprites/Aseprite/newCharacter3/Gera3.png", crop: new Rect(0.00000000f,0.00000000f,0.14285714f,1.00000000f)),
        new Spec("art-36", "Gera angular coat", "Assets/_Project/art/Sprites/Aseprite/newCharacter3/Gera4.png", crop: new Rect(0.00000000f,0.00000000f,0.25000000f,0.25000000f)),
        new Spec("art-38", "Anatomy mannequin", "Assets/_Project/art/Sprites/Aseprite/newCharacter3/testGirl.png", crop: new Rect(0.00000000f,0.00000000f,0.25000000f,0.25000000f)),
        new Spec("art-39", "Dima hooded mini", "Assets/_Project/art/Sprites/Aseprite/newCharacter4/Dima4.png"),
        new Spec("art-41", "Vika mini", "Assets/_Project/art/Sprites/Aseprite/newCharacter4/Vika4.png"),
        new Spec("art-42", "Dima robe chibi", "Assets/_Project/art/Sprites/Aseprite/newCharacters/Dima1.png"),
        new Spec("art-45", "Dummy girl", "Assets/_Project/art/Sprites/Aseprite/newCharacters/DummyGirl1.png"),
        new Spec("art-47", "Dummy man", "Assets/_Project/art/Sprites/Aseprite/newCharacters/DummyMan1.png"),
        new Spec("art-49", "Gera coat chibi", "Assets/_Project/art/Sprites/Aseprite/newCharacters/Gera1.png"),
        new Spec("art-52", "Vika chibi", "Assets/_Project/art/Sprites/Aseprite/newCharacters/Vika1.png"),
        new Spec("art-55", "Round-head mannequin", "Assets/_Project/art/Sprites/Aseprite/newCharacters2/BoyDummy2(2).png", crop: new Rect(0.00000000f,0.00000000f,0.25000000f,0.25000000f)),
        new Spec("art-57", "Blank mini zombie", "Assets/_Project/art/Sprites/Aseprite/newEnemyes1/MiniZombie1_2.png"),
        new Spec("art-60", "Skull mini zombie", "Assets/_Project/art/Sprites/Aseprite/newEnemyes1/MiniZombie2.png"),
        new Spec("art-67", "Armored claw mutant", "Assets/_Project/art/Sprites/Boss1.png"),
        new Spec("art-125", "Pink-haired chibi", "Assets/_Project/art/Sprites/Girl3.png"),
        new Spec("art-132", "Dima long robe", "Assets/_Project/art/Sprites/newPlayers/Dima1.png"),
        new Spec("art-134", "Gera long coat", "Assets/_Project/art/Sprites/newPlayers/Gera_3to4Mine1_02.png"),
        new Spec("art-137", "Vika long coat", "Assets/_Project/art/Sprites/newPlayers/ViIka_3to4Mine1_02.png"),
        new Spec("art-143", "Blond hooded subject", "Assets/_Project/art/Sprites/Player1.png"),
        new Spec("art-145", "Bearded subject", "Assets/_Project/art/Sprites/Player2.png"),
        new Spec("art-149", "Dark hooded pixel human", "Assets/_Project/art/Sprites/testSprites/Char_1.png"),
        new Spec("art-150", "Human base sprite", "Assets/_Project/art/Sprites/testSprites/char_a_p1_0bas_humn_v00.png"),
        new Spec("art-153", "Girl from Mos prefab", "Assets/_Project/art/Sprites/testSprites/Girl1_01.png"),
        new Spec("art-157", "Red turret", "Assets/_Project/art/Sprites/Turret2.png"),
        new Spec("art-215", "Blond shirt subject", "Assets/_Project/art/test/Player2/Character1Aseprite1.png", crop: new Rect(0.00000000f,0.00000000f,0.25000000f,0.25000000f)),
        new Spec("art-219", "Orb robot", "Assets/_Project/art/test/robot/Robot1.png"),
        new Spec("art-243", "Female subject concept", "Assets/_Project/Documentation/FemaleSubject_ConceptOnly.png", crop: new Rect(0.00000000f,0.00000000f,0.12500000f,0.50000000f)),
        new Spec("mosquito", "Mosquito", "Assets/_Project/art/Sprites/Enemy2Aseprite1.aseprite"),
        new Spec("layered-zombie", "Layered red-eye zombie", "Assets/_Project/art/Sprites/Enemy1Aseprite1.aseprite"),
        new Spec("early-zombie", "Early pink-eye zombie", "Assets/_Project/art/test/testPlayers/Enemy1Aseprite1.aseprite"),
        new Spec("blue-jacket", "Blond blue-jacket subject", "Assets/_Project/art/test/testPlayers/char2.aseprite"),
        new Spec("man-model", "Man model prototype", "Assets/_Project/art/test/mesh/Man1.fbx"),
        new Spec("minigirl-model", "Mini girl model", "Assets/_Project/art/test/mesh/MiniGirl1.fbx"),
        new Spec("chibigirl-model", "Chibi girl model", "Assets/_Project/art/test/mesh/ChibiGirl3Main1.fbx"),
        new Spec("bat", "Purple bat", "Assets/_Project/art/Sprites/testSprites/bat SpriteSheet.png"),
        new Spec("poly-0-0", "Polymorph Cyclops", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.000000000f,0.000000000f,0.008403361f,0.027027027f)),
        new Spec("poly-12-0", "Polymorph Horned quadruped", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.100840336f,0.000000000f,0.008403361f,0.027027027f)),
        new Spec("poly-36-0", "Polymorph Slug", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.302521008f,0.000000000f,0.008403361f,0.027027027f)),
        new Spec("poly-66-0", "Polymorph Round slime", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.554621849f,0.000000000f,0.008403361f,0.027027027f)),
        new Spec("poly-72-0", "Polymorph Jagged slime", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.605042017f,0.000000000f,0.008403361f,0.027027027f)),
        new Spec("poly-66-5", "Polymorph Winged mouth", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.554621849f,0.135135135f,0.008403361f,0.027027027f)),
        new Spec("poly-90-0", "Polymorph Long-snout creature", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.756302521f,0.000000000f,0.008403361f,0.027027027f)),
        new Spec("poly-71-11", "Polymorph Cobra", "Assets/_Project/art/Sprites/testSprites/All-polymorph.png", crop: new Rect(0.596638655f,0.297297297f,0.008403361f,0.027027027f)),
    };

    [MenuItem("Tools/Subject42/Refresh Enemy Visual Candidates")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var scene = SceneManager.GetSceneByPath(EnemyGalleryAuthoring.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(EnemyGalleryAuthoring.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var gallery = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyGalleryController>(true)).Single();
        var zone = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Visual Candidates") ?? new GameObject("Visual Candidates");
        if (AssetDatabase.LoadAssetAtPath<EnemyGalleryVisualAssets>(VisualAssetsPath) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<EnemyGalleryVisualAssets>(), VisualAssetsPath);
        var existing = zone.GetComponentsInChildren<EnemyGalleryCandidateMarker>(true).ToList();
        foreach (var spec in Selections)
        {
            var marker = existing.SingleOrDefault(m => m.DesignId == spec.Id);
            // Object references survive source moves/renames. A missing definition is not deletion authority.
            var source = marker != null && marker.SourceAsset != null ? marker.SourceAsset : AssetDatabase.LoadMainAssetAtPath(spec.Path);
            if (source == null) continue;
            if (marker == null)
            {
                marker = new GameObject(spec.Name).AddComponent<EnemyGalleryCandidateMarker>();
                marker.transform.SetParent(zone.transform, false);
                int slot = 0;
                Vector3 pos;
                do { pos = new Vector3(36 + slot % 7 * 8, slot / 7 * 8, 0); slot++; }
                while (existing.Any(m => Vector3.Distance(m.transform.position, pos) < 3));
                marker.transform.position = pos; marker.DesignId = spec.Id; existing.Add(marker);
            }
            marker.DisplayName = spec.Name; marker.SourceAsset = source; marker.SourcePath = AssetDatabase.GetAssetPath(source);
            marker.CandidateType = spec.Crop.width > 0 ? "SpriteSheet selection" : source is GameObject ? "Prefab Visual" : "Sprite / SpriteSheet";
            marker.Description = spec.Name + "; static visual only, no gameplay. Preview scale is recorded below.";
            marker.FoundIn = Provenance(spec);
            Rebuild(marker, spec.Crop);
        }
        foreach (var marker in existing.ToArray())
        {
            if (marker.SourceAsset == null && AssetDatabase.LoadMainAssetAtPath(marker.SourcePath) == null)
            { Object.DestroyImmediate(marker.gameObject); existing.Remove(marker); }
        }
        var so = new SerializedObject(gallery); var labels = so.FindProperty("candidateLabels"); labels.arraySize = existing.Count;
        for (int i = 0; i < existing.Count; i++) labels.GetArrayElementAtIndex(i).objectReferenceValue = existing[i].Label;
        so.ApplyModifiedPropertiesWithoutUndo();
        Layout(scene, zone.transform, existing.Count);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Enemy Visual Candidates refreshed: {existing.Count} reviewed unique designs.");
    }

    static string Provenance(Spec spec)
    {
        if (spec.Id == "cloud-blob") return "cloudcity_chars_1 bottom rows: animated droplet/blob silhouette, ambiguous creature/item; included for artist review. Gold/silver palettes and RPG Maker resolution copies consolidated; doors excluded.";
        if (spec.Path.EndsWith(".fbx")) return spec.Path + "; static source mesh with gallery-only unlit material copies (source base colors/textures preserved); preview rotated to face gameplay camera.";
        if (spec.Id == "demon-girl") return "Layered TestGirl1.psb: complete horned demon girl, no enemy AI required.";
        if (spec.Id == "hood-gera") return "newCharacters2/Gera2.aseprite and Gera2.psd: hooded costume, consolidated layered copies.";
        if (spec.Id == "art-49") return "Gera1.png / Gera1.aseprite / TestBoy1.aseprite: same spiky blue-haired coat character.";
        if (spec.Id == "art-153") return "p_Enemy1 Variant Mos / Graphic: actually a girl; same Girl1_01 sheet and controller.";
        if (spec.Id == "art-219") return "p_robot1 (Robot1.fbx) and p_robot2 (Robot1.png) share the orb-robot design; fog, eyes and shadow are parts.";
        if (spec.Id == "art-157") return "p_EnemyTurret1 / Graphic / Turret2_0 (inactive); locationElements variants, old/Turret2 and gray Turret1 render are the same mechanical design. Current AI pivots target Turret3, so this is static only.";
        if (spec.Id == "art-67") return "Boss1.png and Boss1Aseprite1.aseprite; distinct from production Boss2.";
        if (spec.Id == "blue-jacket") return "char2 / char3 / char5parsed / Character1(2).png / TestBoy1 model: same blond blue-jacket subject.";
        if (spec.Id == "early-zombie") return "testPlayers Enemy1Aseprite1, old copies, Enemy1AsepriteTest1 and TestZombie1 model.";
        if (spec.Id.StartsWith("poly-")) return "All-polymorph atlas: reviewed complete character region; other frames, palette swaps, heads, hands and items are not separate designs.";
        return spec.Path + "; whole character selected by visual audit; animation frames and corresponding ASE/atlas copies consolidated.";
    }

    static void Rebuild(EnemyGalleryCandidateMarker marker, Rect crop)
    {
        if (marker.Display != null) Object.DestroyImmediate(marker.Display.gameObject);
        if (marker.Label != null) Object.DestroyImmediate(marker.Label);
        var display = new GameObject("Static Visual"); display.transform.SetParent(marker.transform, false); marker.Display = display.transform;
        if (marker.SourceAsset is GameObject prefab)
        {
            var visual = Object.Instantiate(prefab, display.transform); visual.name = "Source Visual"; visual.SetActive(true);
            foreach (var a in visual.GetComponentsInChildren<Animator>(true))
            {
                if (a.runtimeAnimatorController != null) a.runtimeAnimatorController.animationClips.FirstOrDefault()?.SampleAnimation(a.gameObject, .2f);
                a.enabled = false; a.fireEvents = false;
            }
            foreach (var component in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(component);
            foreach (var component in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(component);
            foreach (var component in visual.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(component);
            foreach (var component in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(component);
            foreach (var component in visual.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(component);
            foreach (var component in visual.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(component);
            // Models face +Z in the source assets; the gameplay camera looks from -Z.
            if (marker.SourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                visual.transform.localRotation *= Quaternion.Euler(0,180,0);
                // Source toon shaders depend on a 3D lighting setup absent from the gameplay 2D renderer.
                // Gallery-only unlit copies retain their actual base colors/textures; originals are untouched.
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(PreviewMaterial).ToArray();
            }
        }
        else
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(marker.SourcePath).OfType<Sprite>().OrderBy(s=>s.name, StringComparer.Ordinal).ToArray();
            Sprite sprite;
            if (crop.width > 0)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(marker.SourcePath);
                int x = Mathf.RoundToInt(crop.x * texture.width), y = Mathf.RoundToInt((1-crop.y-crop.height)*texture.height);
                var rect = new Rect(x, y, Mathf.Min(Mathf.RoundToInt(crop.width*texture.width),texture.width-x), Mathf.Min(Mathf.RoundToInt(crop.height*texture.height),texture.height-y));
                string name = marker.DesignId;
                sprite = AssetDatabase.LoadAllAssetsAtPath(VisualAssetsPath).OfType<Sprite>().SingleOrDefault(s => s.name == name);
                if (sprite == null) { sprite = Sprite.Create(texture, rect, new Vector2(.5f,.5f), 100); sprite.name=name; AssetDatabase.AddObjectToAsset(sprite,VisualAssetsPath); }
                else if (sprite.rect != rect || sprite.texture != texture)
                { var replacement = Sprite.Create(texture, rect, new Vector2(.5f,.5f),100); replacement.name=name; EditorUtility.CopySerialized(replacement,sprite); Object.DestroyImmediate(replacement); EditorUtility.SetDirty(sprite); }
            }
            else sprite = marker.SourceAsset as Sprite ?? sprites.FirstOrDefault(s => s.name == "Turret2_6") ?? sprites.FirstOrDefault(s => s.name.EndsWith("_0")) ?? sprites.FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("No representative sprite: " + marker.SourcePath);
            display.AddComponent<SpriteRenderer>().sprite = sprite;
        }
        var renderers = display.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && r is not ParticleSystemRenderer).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("Empty candidate: " + marker.SourcePath);
        var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
        marker.PreviewScale = 2.7f / Mathf.Max(bounds.size.x, bounds.size.y);
        display.transform.position += marker.transform.position - bounds.center;
        display.transform.localPosition *= marker.PreviewScale;
        display.transform.localScale = Vector3.one * marker.PreviewScale;
        marker.Label = Label(marker.DisplayName + "\nSTATIC CANDIDATE\n" + Path.GetFileName(marker.SourcePath), marker.transform, marker.transform.position + Vector3.down*2.2f, 1.65f);
    }

    static Material PreviewMaterial(Material source)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
        string name="ModelPreview_"+guid+"_"+localId;
        var preview=AssetDatabase.LoadAllAssetsAtPath(VisualAssetsPath).OfType<Material>().SingleOrDefault(m => m.name == name);
        if(preview==null) {preview=new Material(Shader.Find("Universal Render Pipeline/Unlit"));preview.name=name;AssetDatabase.AddObjectToAsset(preview,VisualAssetsPath);}
        preview.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);
        preview.SetTexture("_BaseMap",source.mainTexture);
        preview.SetFloat("_AlphaClip",1); preview.SetFloat("_Cutoff",.1f); preview.EnableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(preview);return preview;
    }

    static GameObject Label(string text, Transform parent, Vector3 pos, float size)
    {
        var go = new GameObject("Label"); go.transform.SetParent(parent,false); go.transform.position=pos;
        var tmp=go.AddComponent<TextMeshPro>(); tmp.text=text;tmp.fontSize=size;tmp.alignment=TextAlignmentOptions.Center;
        tmp.color=new Color(.76f,.83f,.86f);tmp.rectTransform.sizeDelta=new Vector2(7.5f,1.5f); tmp.GetComponent<MeshRenderer>().sortingOrder=100;
        return go;
    }
    static void Layout(Scene scene, Transform zone, int count)
    {
        var heading = zone.Find("Zone Heading"); if(heading != null) Object.DestroyImmediate(heading.gameObject);
        Label("B / VISUAL CANDIDATES\nStatic art studies - sources in Inspector",zone,new Vector3(60,-6,0),3).name="Zone Heading";
        var room=scene.GetRootGameObjects().Single(g=>g.name=="Room Boundary");
        var title=room.GetComponentsInChildren<TextMeshPro>().First(t=>t.text.Contains("ENEMY GALLERY") || t.text.Contains("PRODUCTION /"));
        title.text="A / PRODUCTION / PREPARED PREFABS";title.fontSize=2.6f;title.rectTransform.sizeDelta=new Vector2(31,1);
        float top=Mathf.Max(25,Mathf.Ceil(count/7f)*8); float right=89;
        var walls=room.GetComponentsInChildren<BoxCollider2D>();
        walls[0].transform.position=new Vector3(-20,(top-13)/2);walls[0].size=new Vector2(1,top+13);
        walls[1].transform.position=new Vector3(right,(top-13)/2);walls[1].size=new Vector2(1,top+13);
        walls[2].transform.position=new Vector3((right-20)/2,-13);walls[2].size=new Vector2(right+21,1);
        walls[3].transform.position=new Vector3((right-20)/2,top);walls[3].size=new Vector2(right+21,1);
    }
}
