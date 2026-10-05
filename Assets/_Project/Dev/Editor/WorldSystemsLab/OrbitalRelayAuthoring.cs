#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class OrbitalRelayAuthoring
{
    public const string PrefabPath = "Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab";
    public const string ConfigPath = "Assets/_Project/Data/World/Events/OrbitalRelayConfig.asset";
    private const string Fx = "Assets/_Project/art/FX/OrbitalRelay/";
    static OrbitalRelayAuthoring() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Artifacts/OrbitalRelay/author.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { CreateOrUpdateProductionAssets(); File.WriteAllText("Artifacts/OrbitalRelay/author-result.txt", "SUCCESS"); }
        catch (Exception error) { File.WriteAllText("Artifacts/OrbitalRelay/author-result.txt", error.ToString()); Debug.LogException(error); }
    }
    internal static void Set(UnityEngine.Object target, string fieldName, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value); EditorUtility.SetDirty(target); return;
        }
        throw new InvalidOperationException(target.name + ": missing field " + fieldName);
    }
    private static GameObject Child(Transform parent, string name, bool rect = false)
    {
        var child = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        child.transform.SetParent(parent, false); return child;
    }
    private static AnimationClip Clip(string name, bool loop = false)
    {
        string path = Fx + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        foreach (var binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings); return clip;
    }
    private static void Curve(AnimationClip clip, string path, Type type, string property, params Keyframe[] keys) =>
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), new AnimationCurve(keys));
    private static void Scale(AnimationClip clip, string path, params Keyframe[] keys)
    { Curve(clip, path, typeof(Transform), "m_LocalScale.x", keys); Curve(clip, path, typeof(Transform), "m_LocalScale.y", keys); }
    private static Animator Animate(GameObject root, AnimationClip clip, bool idle)
    {
        string path = Fx + clip.name + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        if (idle) machine.defaultState = machine.AddState("Idle");
        var animationState = machine.AddState(clip.name); animationState.motion = clip;
        if (!idle) machine.defaultState = animationState;
        var animator = root.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
        return animator;
    }
    private static LineRenderer Ring(GameObject root, Material material, float width)
    {
        var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = material;
        line.useWorldSpace = false; line.loop = true; line.positionCount = 64; line.widthMultiplier = width;
        line.sortingOrder = 12; line.startColor = line.endColor = new Color(.25f, 1f, .9f);
        for (int i = 0; i < 64; i++) { float a = i * Mathf.PI * 2 / 64; line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0)); }
        return line;
    }
    private static TextMeshProUGUI Label(Transform parent, string name, float y, TMP_FontAsset font, float size)
    {
        var root = Child(parent, name, true); var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(400, 48);
        var text = root.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.color = Color.white;
        return text;
    }

    [MenuItem("Tools/Subject42/Dev/Orbital Relay/Author Production Prefab")]
    public static void CreateOrUpdateProductionAssets()
    {
        Directory.CreateDirectory(Fx); Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)); AssetDatabase.Refresh();
        var config = AssetDatabase.LoadAssetAtPath<OrbitalRelayConfig>(ConfigPath);
        if (config == null) { config = ScriptableObject.CreateInstance<OrbitalRelayConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
        var material = AssetDatabase.LoadAssetAtPath<Material>(Fx + "RelayGlow.mat");
        if (material == null) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, Fx + "RelayGlow.mat"); }
        var round = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("ceb8225d9461f5a4bbe86004909390d0")).OfType<Sprite>().First();
        var square = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("842791f4876552648b2118a0575f7a6d")).OfType<Sprite>().First();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("c804072f2f9246739b6050598e4bbf0d"));
        var pulse = Clip("NodePulse", true); Scale(pulse, "", new(0, 1), new(.4f, 1.22f), new(.8f, 1));
        var activationClip = Clip("NodeActivation"); Scale(activationClip, "", new(0, .35f), new(.3f, 1.4f), new(.45f, 0));
        var combo = Clip("ComboPunch"); Scale(combo, "", new(0, 1), new(.08f, 1.25f), new(.25f, 1));
        var urgent = Clip("UrgentTimer", true); Scale(urgent, "", new(0, 1), new(.25f, 1.1f), new(.5f, 1));
        Curve(urgent, "", typeof(TextMeshProUGUI), "m_fontColor.g", new(0, .2f), new(.25f, .8f), new(.5f, .2f));
        var transition = Clip("StabilizedTransition");
        Scale(transition, "TransitionFX/Shockwave", new(0, .05f), new(.72f, 9), new(.75f, 0));
        Curve(transition, "PresentationCanvas/StabilizedBanner", typeof(CanvasGroup), "m_Alpha", new(0, 1), new(.5f, 1), new(.75f, 0));
        Scale(transition, "PresentationCanvas/StabilizedBanner", new(0, .8f), new(.12f, 1.15f), new(.75f, 1));
        Curve(transition, "PresentationCanvas/Flash", typeof(CanvasGroup), "m_Alpha", new(0, .35f), new(.2f, .05f), new(.75f, 0));
        var root = new GameObject("Hold Zone - Orbital Relay");
        try
        {
            var relay = root.AddComponent<OrbitalRelayEvent>();
            var presentation = root.AddComponent<OrbitalRelayPresentation>();
            var pressure = root.AddComponent<WorldEventPressureModifier>();
            var bounds = Child(root.transform, "ArenaBounds").AddComponent<CircleCollider2D>(); bounds.radius = 9; bounds.isTrigger = true;
            Transform marker = Child(root.transform, "EventMarkerAnchor").transform;
            Transform reward = Child(root.transform, "RewardAnchor").transform;
            var nodesRoot = Child(root.transform, "Nodes"); var nodes = new OrbitalRelayNode[3];
            Vector2[] positions = { new(4.51f, 0), new(-3.08f, 5.335f), new(-2.255f, -3.906f) };
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = Child(nodesRoot.transform, "Node " + (i + 1)); node.transform.localPosition = positions[i];
                nodes[i] = node.AddComponent<OrbitalRelayNode>();
                var area = Child(node.transform, "ContactArea").AddComponent<CircleCollider2D>(); area.radius = .35f; area.isTrigger = true;
                var core = Child(node.transform, "Core").AddComponent<SpriteRenderer>(); core.sprite = round; core.sortingOrder = 4;
                core.transform.localScale = Vector3.one * (.7f / round.bounds.size.x);
                var glow = Child(node.transform, "ActiveGlow"); var glowVisual = Child(glow.transform, "Ring"); Ring(glowVisual, material, .04f);
                glowVisual.transform.localScale = Vector3.one * .5f; Animate(glow, pulse, false); glow.SetActive(false);
                var fill = Child(node.transform, "ContactProgress"); fill.transform.localPosition = new Vector3(0, -.55f, 0);
                var fillSprite = Child(fill.transform, "Fill").AddComponent<SpriteRenderer>(); fillSprite.sprite = square; fillSprite.color = Color.yellow; fillSprite.sortingOrder = 5;
                fillSprite.transform.localScale = new Vector3(1 / square.bounds.size.x, .1f / square.bounds.size.y, 1);
                fill.transform.localScale = new Vector3(0, 1, 1);
                var fx = Child(node.transform, "ActivationFX"); Ring(fx, material, .06f); fx.transform.localScale = Vector3.zero;
                Set(nodes[i], "contactArea", area); Set(nodes[i], "core", core); Set(nodes[i], "activeGlow", glow);
                Set(nodes[i], "progressFill", fill.transform); Set(nodes[i], "activation", Animate(fx, activationClip, true));
            }
            var transitionRoot = Child(root.transform, "TransitionFX"); var shockwave = Child(transitionRoot.transform, "Shockwave");
            Ring(shockwave, material, .08f); shockwave.transform.localScale = Vector3.zero;
            var canvasRoot = Child(root.transform, "PresentationCanvas", true); var canvas = canvasRoot.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var scaler = canvasRoot.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var panel = Child(canvasRoot.transform, "RelayPanel", true); var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-32, -70); rect.sizeDelta = new Vector2(420, 300);
            var background = panel.AddComponent<Image>(); background.color = new Color(.015f, .06f, .085f, .9f); background.raycastTarget = false;
            var phase = Label(panel.transform, "Phase", -12, font, 27); var time = Label(panel.transform, "Time", -62, font, 36);
            var score = Label(panel.transform, "Activations", -114, font, 24); var gold = Label(panel.transform, "Gold", -160, font, 24); var comboLabel = Label(panel.transform, "Combo", -212, font, 24);
            var flash = Child(canvasRoot.transform, "Flash", true); var flashRect = (RectTransform)flash.transform;
            flashRect.anchorMin = Vector2.zero; flashRect.anchorMax = Vector2.one; flashRect.sizeDelta = Vector2.zero;
            var flashImage = flash.AddComponent<Image>(); flashImage.color = new Color(.3f, 1, .9f); flashImage.raycastTarget = false; flash.AddComponent<CanvasGroup>().alpha = 0;
            var banner = Label(canvasRoot.transform, "StabilizedBanner", -420, font, 64); banner.text = "STABILIZED"; banner.rectTransform.sizeDelta = new Vector2(900, 120); banner.gameObject.AddComponent<CanvasGroup>().alpha = 0;
            var localBanner = banner.gameObject.AddComponent<LocalizedText>(); Set(localBanner, "localizationKey", "event.relay.stabilized");
            Set(presentation, "panel", panel); Set(presentation, "phaseLabel", phase); Set(presentation, "timeLabel", time); Set(presentation, "activationsLabel", score);
            Set(presentation, "goldLabel", gold); Set(presentation, "comboLabel", comboLabel); Set(presentation, "flashOverlay", flashImage);
            Set(presentation, "transitionFx", Animate(root, transition, true)); Set(presentation, "comboFx", Animate(comboLabel.gameObject, combo, true)); Set(presentation, "urgentFx", Animate(time.gameObject, urgent, false));
            Set(relay, "config", config); Set(relay, "arenaBounds", bounds); Set(relay, "nodes", nodes); Set(relay, "presentation", presentation); Set(relay, "pressure", pressure); Set(relay, "markerAnchor", marker); Set(relay, "rewardAnchor", reward);
            Set(relay, "eventId", "orbital_relay"); Set(relay, "eventTag", "hold_zone"); Set(relay, "eventDisplayName", "event.relay.name"); Set(relay, "eventDescription", "event.relay.description"); Set(relay, "allowedInSite", true); Set(relay, "requiresHoldPointFeature", true); Set(relay, "promptText", "hud.interact");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets(); ValidateProductionAssets();
    }
    public static void ValidateProductionAssets()
    {
        var relay = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(PrefabPath);
        string error = null;
        if (relay == null || !relay.TryValidateConfiguration(out error)) throw new InvalidOperationException(relay == null ? "Relay prefab missing." : error);
        if (AssetDatabase.GetDependencies(PrefabPath, true).Any(path => path.StartsWith("Assets/_Project/Dev/"))) throw new InvalidOperationException("Production Relay has a Dev dependency.");
    }
}
#endif
