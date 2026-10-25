using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

// A temporary offscreen calibration sprite tests the actual existing flash shader.
// No asset is saved and the probe never enters the player's viewport or physics.
public static class EnemyFlashTintSmoke
{
    public static IEnumerator Verify(string directory)
    {
        Directory.CreateDirectory(directory);
        var failures = new List<string>();
        var trace = new List<string>();
        var cameraObject = new GameObject("Flash tint calibration camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 1f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(10000f, 10000f, -10f);
        var target = new RenderTexture(16, 16, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        target.Create(); camera.targetTexture = target;
        var material = new Material(Shader.Find("Custom/SpriteWhiteFlash"));
        material.SetFloat("_FlashBlend", 0f);
        var sprite = Sprite.Create(Texture2D.whiteTexture,
            new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), 1f);
        var actor = new GameObject("Flash tint calibration sprite");
        actor.layer = 31; actor.transform.position = new Vector3(10000f, 10000f, 0f);
        var renderer = actor.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.sharedMaterial = material;
        var block = new MaterialPropertyBlock();
        Mesh mesh = null;
        GameObject meshObject = null;
        var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        try
        {
            renderer.color = new Color(.2f, .6f, 1f);
            yield return null; yield return null;
            Color normal = Sample();
            trace.Add("SpriteRenderer.color=" + normal);
            Check(Mathf.Abs(normal.r - .2f) < .04f && Mathf.Abs(normal.g - .6f) < .04f && normal.b > .95f,
                "Rendered flash lost or double-multiplied SpriteRenderer.color.");
            bool standardInputs = material.HasProperty("_Color") && material.HasProperty("_RendererColor");
            Check(standardInputs, "Partial flash must honor the standard material and non-batched renderer tint inputs.");
            if (standardInputs)
            {
                renderer.color = Color.white;
                renderer.enabled = false;
                // SpriteRenderer owns _RendererColor and overwrites external values.
                // A plain quad isolates the shader's explicit per-draw input contract.
                mesh = new Mesh
                {
                    vertices = new[] { new Vector3(-1,-1), new Vector3(1,-1), new Vector3(1,1), new Vector3(-1,1) },
                    uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                    colors = new[] { Color.white, Color.white, Color.white, Color.white },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 }
                };
                meshObject = new GameObject("Flash per-draw calibration quad", typeof(MeshFilter), typeof(MeshRenderer));
                meshObject.layer = 31; meshObject.transform.position = actor.transform.position;
                meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                var draw = meshObject.GetComponent<MeshRenderer>();
                draw.sharedMaterial = material;
                block.SetColor("_RendererColor", new Color(.2f, .6f, 1f));
                draw.SetPropertyBlock(block);
                yield return null; yield return null;
                Color perRenderer = Sample();
                trace.Add("Renderer tint=" + perRenderer);
                Check(Mathf.Abs(perRenderer.r - .2f) < .04f && Mathf.Abs(perRenderer.g - .6f) < .04f && perRenderer.b > .95f,
                    "Rendered flash lost non-batched renderer tint.");
                block.SetColor("_RendererColor", Color.white);
                draw.SetPropertyBlock(block);
                material.SetColor("_Color", new Color(1f, .2f, .1f));
                yield return null; yield return null;
                Color perMaterial = Sample();
                trace.Add("Material tint=" + perMaterial);
                Check(perMaterial.r > .95f && Mathf.Abs(perMaterial.g - .2f) < .04f && Mathf.Abs(perMaterial.b - .1f) < .04f,
                    "Rendered flash lost material tint.");
                // Full blend remains the legacy/player result, independent of source tint.
                material.SetFloat("_FlashBlend", 1f);
                material.SetColor("_FlashColor", new Color(1f, .26f, .18f));
                yield return null; yield return null;
                Color full = Sample();
                trace.Add("Legacy/player full blend=" + full);
                Check(full.r > .95f && Mathf.Abs(full.g - .26f) < .04f && Mathf.Abs(full.b - .18f) < .04f,
                    "Default full flash must preserve warm player feedback.");
            }
        }
        finally
        {
            camera.enabled = false;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target); Object.Destroy(pixel); Object.Destroy(material);
            Object.Destroy(actor); Object.Destroy(sprite); Object.Destroy(cameraObject);
            if (meshObject != null) Object.Destroy(meshObject);
            if (mesh != null) Object.Destroy(mesh);
        }
        trace.Add(failures.Count == 0 ? "PASS" : string.Join("\n", failures));
        File.WriteAllLines(directory + "/shader-tint.txt", trace);
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        void Check(bool value, string message) { if (!value) failures.Add(message); }
        Color Sample()
        {
            var active = RenderTexture.active;
            RenderTexture.active = target;
            pixel.ReadPixels(new Rect(8, 8, 1, 1), 0, 0); pixel.Apply();
            RenderTexture.active = active;
            return pixel.GetPixel(0, 0);
        }
    }
}
