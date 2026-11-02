#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Subject42.DebugLabs.Telekinetic;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;

public static partial class TelekineticConstructorAuthoring
{
    private static bool ConfigureIcons(TelekineticConstructorLab lab)
    {
        const string folder="Assets/_Project/Dev/Labs/TelekineticConstructorLab/Icons";
        if (!Directory.Exists(folder)) { Directory.CreateDirectory(folder); AssetDatabase.Refresh(); }
        var icons=new Sprite[Enum.GetValues(typeof(TelekineticPart)).Length];
        foreach (TelekineticPart kind in Enum.GetValues(typeof(TelekineticPart)))
        {
            if (kind>=TelekineticPart.Pistol && kind<=TelekineticPart.Saw)
            {
                string name=kind==TelekineticPart.Pistol?"Pistol":kind==TelekineticPart.Shotgun?"Shotgun":"Saw";
                icons[(int)kind]=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/art/UI/StationModules/"+name+".png");
                continue;
            }
            // These four keep the existing shared pixel glyphs; no transient sprite is serialized.
            if (kind>=TelekineticPart.DMG && kind<=TelekineticPart.RIC) continue;
            string path=folder+"/"+TelekineticBuild.Label(kind)+".png";
            if (!File.Exists(path))
            {
                string[] mask=kind switch
                {
                    TelekineticPart.Frame=>new[]{"............","..########..","..#......#..","..#..##..#..","..#..##..#..","..########..","..########..","..#..##..#..","..#..##..#..","..#......#..","..########..","............"},
                    TelekineticPart.Energy=>new[]{"....####....","..########..","..#....#.#..","..#...##.#..","..#..##..#..","..#.####.#..","..#..##..#..","..#.##...#..","..#.#....#..","..#......#..","..########..","............"},
                    TelekineticPart.Pierce=>new[]{"............",".....#..#...",".....#..#...","...#.#..#...","...###..#...","###########.","###########.","...###..#...","...#.#..#...",".....#..#...",".....#..#...","............"},
                    TelekineticPart.Splash=>new[]{".....##.....","..#..##..#..","...######...","..##....##..","..#..##..#..","###.####.###","###.####.###","..#..##..#..","..##....##..","...######...","..#..##..#..",".....##....."},
                    TelekineticPart.Chain=>new[]{"............","......####..",".....##..##.",".....#....#.","..#####...#.",".##..###.##.",".#...#####..",".#....#.....",".##..##.....","..####......","............","............"},
                    TelekineticPart.Crit=>new[]{".....##.....",".###.##.###.",".#........#.",".#...##...#.","....####....","##.######.##","##.######.##","....####....",".#...##...#.",".#........#.",".###.##.###.",".....##....."},
                    TelekineticPart.Slow=>new[]{".....##.....","..#..##..#..","..##.##.##..","...######...","....####....","############","############","....####....","...######...","..##.##.##..","..#..##..#..",".....##....."},
                    TelekineticPart.Shred=>new[]{"..########..",".##......##.",".#.....##.#.",".#....##..#.",".#...##...#.",".##.##...##.","..####..##..","...##..##...","..##..##....",".##.###.....","....##......","............"},
                    TelekineticPart.Knock=>new[]{".......##...","...##..###..","...###.####.","....#######.",".###########",".###########",".###########","....#######.","...###.####.","...##..###..",".......##...","............"},
                    TelekineticPart.Echo=>new[]{"...####.....",".##....##...","##..##..##..","#..#..#..#..","#.#....#.#..","#.#....#.#..","#..#..#..#..","##..##..##..",".##....##...","...####.....",".......##...","........##.."},
                    _=>throw new ArgumentOutOfRangeException(nameof(kind))
                };
                var pixels=new Color32[16*16];
                for (int y=0;y<12;y++) for (int x=0;x<12;x++) if (mask[y][x]=='#')
                    pixels[(13-y)*16+x+2]=new Color32(255,255,255,255);
                var texture=new Texture2D(16,16,TextureFormat.RGBA32,false); texture.SetPixels32(pixels); texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=16; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true; importer.SaveAndReimport();
            }
            icons[(int)kind]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        bool changed=lab.ItemIcons==null || !lab.ItemIcons.SequenceEqual(icons);
        if (changed) lab.ItemIcons=icons;
        var shield=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/ShieldBearer.prefab");
        if (lab.ShieldEnemyPrefab!=shield) { lab.ShieldEnemyPrefab=shield; changed=true; }
        return changed;
    }
}
#endif
