using System.IO;
using UnityEditor;
using UnityEngine;

// Small, repeatable asset authoring pass; no texture copies or runtime sprite creation.
[InitializeOnLoad]
public static class ProductionPropsAuthoring
{
    const string Root = "Assets/_Project/Environment/Props";
    const string Request = "Artifacts/EnvironmentProps/author.request";
    static ProductionPropsAuthoring() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Author();
        File.WriteAllText("Artifacts/EnvironmentProps/author.result", "OK");
    }

    [MenuItem("Tools/Subject42/Environment/Author sector props")]
    public static void Author()
    {
        Directory.CreateDirectory(Root + "/Resources/SectorProps");
        AssetDatabase.Refresh();
        var material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/_Project/art/Sprites/Environment/ColdAsh/ColdAshLit.mat");
        var metal = new Color(.38f, .45f, .49f);
        var pale = new Color(.36f, .42f, .46f);
        // Coordinates below use the top-left of existing source sheets, in source pixels.
        Make("Broken metal box", "BunkerCase1", 0, 83, 32, 29, .92f, metal);
        Make("Low crate", "BunkerCase1", 0, 20, 32, 29, .85f, metal);
        Make("Scrap heap", "BunkerCase1", 98, 64, 63, 49, .7f, pale);
        Make("Pipe elbow", "BunkerElements3", 76, 7, 71, 25, .65f, pale);
        Make("Small canister", "BunkerElements3", 256, 0, 32, 32, .9f, pale);
        Make("Fallen service cabinet", "BunkerElements1", 418, 530, 30, 78, .58f, new Color(.55f, .62f, .66f));
        Make("Concrete fragment", "BunkerCase1", 230, 38, 22, 23, 1f, new Color(.46f, .5f, .53f));
        Make("Cable offcut", "BunkerElements3", 192, 140, 89, 49, .55f, pale);
        Make("Bent vent panel", "BunkerElements1", 192, 0, 64, 64, .58f, new Color(.64f, .7f, .73f));
        AssetDatabase.SaveAssets();

        void Make(string name, string sheet, int x, int top, int width, int height, float scale, Color tint)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/art/Sprites/Environment/" + sheet + ".png");
            string spritePath = Root + "/" + name + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var authored = Sprite.Create(texture, new Rect(x, texture.height - top - height, width, height),
                new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            authored.name = name;
            if (sprite == null)
            {
                sprite = authored;
                AssetDatabase.CreateAsset(sprite, spritePath);
            }
            else
            {
                EditorUtility.CopySerialized(authored, sprite);
                EditorUtility.SetDirty(sprite);
                Object.DestroyImmediate(authored);
            }
            var go = new GameObject(name);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * scale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = tint;
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -105;
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Resources/SectorProps/" + name + ".prefab");
            Object.DestroyImmediate(go);
        }
    }
}
