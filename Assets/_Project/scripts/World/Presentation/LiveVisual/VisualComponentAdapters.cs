using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Subject42.Visuals
{
    // Explicit component contracts. No reflection over arbitrary gameplay fields.
    public static class VisualComponentAdapters
    {
        public static void Populate(VisualTarget target, VisualTuningPreset preset, VisualProfile baseline)
        {
            var properties = target.Properties;
            void Add(string id, string label, string category, Func<float> read, Action<float> write, float min, float max, bool integer = false)
                => properties.Add(new VisualProperty(id, label, category, read, write, min, max) { IsInteger = integer });
            switch (target.Component)
            {
                case SpriteRenderer sprite:
                    VisualProperties.Color(properties, "sprite.tint", "Tint", "Цвет", () => sprite.color, c => sprite.color = c);
                    Add("sprite.order", "Sorting order", "Представление", () => sprite.sortingOrder, v => sprite.sortingOrder = (int)v, -32768, 32767, true);
                    Add("sprite.flipX", "Flip X", "Представление", () => sprite.flipX ? 1 : 0, v => sprite.flipX = v >= .5f, 0, 1, true);
                    Add("sprite.flipY", "Flip Y", "Представление", () => sprite.flipY ? 1 : 0, v => sprite.flipY = v >= .5f, 0, 1, true);
                    AddMaterial(target, sprite, preset, baseline);
                    // A presentation-only leaf is safe; never scale an actor/physics hierarchy.
                    if (sprite.transform.parent != null && sprite.GetComponentInParent<Subject42.Combat.OrbitalStation.OrbitalModuleView>() == null &&
                        sprite.GetComponentsInChildren<Collider2D>(true).Length == 0 &&
                        sprite.GetComponents<MonoBehaviour>().Length == 0 && sprite.GetComponent<Rigidbody2D>() == null)
                    {
                        Add("visual.scaleX", "Масштаб визуала X", "Представление", () => sprite.transform.localScale.x,
                            v => { var s = sprite.transform.localScale; s.x = v; sprite.transform.localScale = s; }, .01f, 20);
                        Add("visual.scaleY", "Масштаб визуала Y", "Представление", () => sprite.transform.localScale.y,
                            v => { var s = sprite.transform.localScale; s.y = v; sprite.transform.localScale = s; }, .01f, 20);
                    }
                    else target.Unsupported.Add("Scale/rotation корня не доступны: возможная связь с физикой или игровым поведением.");
                    break;
                case TrailRenderer trail:
                    Add("trail.time", "Длина следа, секунды", "След", () => trail.time, v => trail.time = v, 0, 30);
                    Add("trail.width", "Толщина следа", "След", () => trail.widthMultiplier, v => trail.widthMultiplier = v, 0, 20);
                    VisualProperties.Color(properties, "trail.start", "Начало", "След", () => trail.startColor, v => trail.startColor = v);
                    VisualProperties.Color(properties, "trail.end", "Конец", "След", () => trail.endColor, v => trail.endColor = v);
                    AddMaterial(target, trail, preset, baseline);
                    break;
                case LineRenderer line:
                    Add("line.width", "Толщина", "Линия", () => line.widthMultiplier, v => line.widthMultiplier = v, 0, 20);
                    VisualProperties.Color(properties, "line.start", "Начало", "Линия", () => line.startColor, v => line.startColor = v);
                    VisualProperties.Color(properties, "line.end", "Конец", "Линия", () => line.endColor, v => line.endColor = v);
                    AddMaterial(target, line, preset, baseline);
                    target.Unsupported.Add("Позиции, радиусы и число вершин линии не редактируются: они могут обозначать игровую геометрию.");
                    break;
                case Renderer renderer:
                    AddMaterial(target, renderer, preset, baseline);
                    break;
                case Light2D light:
                    VisualProperties.Color(properties, "light.color", "Цвет", "Свет 2D", () => light.color, v => light.color = v, includeAlpha: false);
                    Add("light.intensity", "Интенсивность", "Свет 2D", () => light.intensity, v => light.intensity = v, 0, 100);
                    if (light.lightType == Light2D.LightType.Point)
                    {
                        float originalInner = light.pointLightInnerRadius, originalOuter = light.pointLightOuterRadius;
                        target.SetComparison = _ =>
                        {
                            light.pointLightOuterRadius = Mathf.Max(originalOuter, light.pointLightInnerRadius);
                            light.pointLightInnerRadius = originalInner;
                            light.pointLightOuterRadius = originalOuter;
                        };
                        Add("light.innerRadius", "Внутренний радиус света", "Свет 2D", () => light.pointLightInnerRadius,
                            v => light.pointLightInnerRadius = Mathf.Min(v, light.pointLightOuterRadius), 0, 100);
                        Add("light.outerRadius", "Внешний радиус света", "Свет 2D", () => light.pointLightOuterRadius,
                            v => light.pointLightOuterRadius = Mathf.Max(v, light.pointLightInnerRadius), 0, 100);
                        Action restoreRadii = () =>
                        {
                            light.pointLightOuterRadius = Mathf.Max(originalOuter, light.pointLightInnerRadius);
                            light.pointLightInnerRadius = originalInner;
                            light.pointLightOuterRadius = originalOuter;
                        };
                        properties.Find(p => p.Id == "light.innerRadius").Restore = restoreRadii;
                        properties.Find(p => p.Id == "light.outerRadius").Restore = restoreRadii;
                    }
                    if (light.lightType != Light2D.LightType.Global)
                    {
                        Add("light.falloff", "Falloff", "Свет 2D", () => light.falloffIntensity, v => light.falloffIntensity = v, 0, 1);
                        Add("light.shadowIntensity", "Тени", "Свет 2D", () => light.shadowIntensity, v => light.shadowIntensity = v, 0, 1);
                    }
                    break;
                case Light light3D:
                    VisualProperties.Color(properties, "light.color", "Цвет", "Свет", () => light3D.color, v => light3D.color = v, includeAlpha: false);
                    Add("light.intensity", "Интенсивность", "Свет", () => light3D.intensity, v => light3D.intensity = v, 0, 100);
                    Add("light.range", "Радиус света", "Свет", () => light3D.range, v => light3D.range = v, 0, 100);
                    break;
                case ParticleSystem particle:
                    Add("particles.size", "Размер, множитель", "Частицы", () => particle.main.startSizeMultiplier,
                        v => { var m = particle.main; m.startSizeMultiplier = v; }, 0, 30);
                    Add("particles.lifetime", "Lifetime, множитель", "Частицы", () => particle.main.startLifetimeMultiplier,
                        v => { var m = particle.main; m.startLifetimeMultiplier = v; }, 0, 30);
                    Add("particles.speed", "Скорость, множитель", "Частицы", () => particle.main.startSpeedMultiplier,
                        v => { var m = particle.main; m.startSpeedMultiplier = v; }, 0, 100);
                    // Weather and pooled effects may enable this module after registration.
                    Add("particles.emission", "Плотность / сек", "Частицы", () => particle.emission.rateOverTimeMultiplier,
                        v => { var m = particle.emission; m.rateOverTimeMultiplier = v; }, 0, 2000);
                    properties[^1].Available = () => particle.emission.enabled;
                    properties[^1].Note = "Доступно при включённом emission. Добавляет rate over time; не изменяет число частиц прямого C# Emit или burst.";
                    Add("particles.maxParticles", "Лимит частиц", "Частицы", () => particle.main.maxParticles,
                        v => { var m = particle.main; m.maxParticles = (int)v; }, 0, 20000, true);
                    var authoredLifetime = particle.colorOverLifetime;
                    bool lifetimeEnabled = authoredLifetime.enabled;
                    var lifetimeGradient = authoredLifetime.color;
                    var tintBasis = lifetimeEnabled ? lifetimeGradient : new ParticleSystem.MinMaxGradient(Color.white);
                    Color tint = Color.white;
                    float lifetimeAlpha = 1;
                    void ApplyParticleTint()
                    {
                        var module = particle.colorOverLifetime;
                        bool changed = tint != Color.white || lifetimeAlpha != 1;
                        module.enabled = lifetimeEnabled || changed;
                        module.color = !changed ? lifetimeGradient : Multiply(tintBasis, new Color(tint.r, tint.g, tint.b, tint.a * lifetimeAlpha));
                    }
                    VisualProperties.Color(properties, "particles.tint", "Tint частиц после Emit", "Частицы", () => tint,
                        c => { tint = c; ApplyParticleTint(); });
                    Add("particles.alphaOverLifetime", "Alpha over lifetime, множитель", "Частицы", () => lifetimeAlpha,
                        v => { lifetimeAlpha = v; ApplyParticleTint(); }, 0, 1);
                    target.Unsupported.Add("Tint/Alpha используют colorOverLifetime, поэтому работают и с EmitParams из C#. Reset восстанавливает исходный градиент и enabled.");
                    target.Unsupported.Add("Изменение модулей влияет на новые частицы. Существующие доживают свой lifetime. Burst/shape/sub-emitters не меняются без специализированного контракта.");
                    if (particle.collision.enabled || particle.trigger.enabled)
                        target.Unsupported.Add("Preview отключён: у ParticleSystem есть collision/trigger.");
                    break;
                case Camera camera when camera.orthographic:
                    Add("camera.size", "Ортографический размер", "Камера", () => camera.orthographicSize, v => camera.orthographicSize = v, 1, 80);
                    target.Unsupported.Add("Камера остаётся игровой. Система camera-follow может управлять масштабом при переходах.");
                    break;
                case Volume volume:
                    AddVolume(target, volume);
                    break;
            }
        }

        private static void AddMaterial(VisualTarget target, Renderer renderer, VisualTuningPreset preset, VisualProfile baseline)
        {
            Material current = renderer.sharedMaterial;
            if (current == null) { target.Unsupported.Add("У Renderer нет материала."); return; }
            Material original = null;
            if (baseline?.MaterialSources != null)
                foreach (var source in baseline.MaterialSources)
                    if (source.Target == target.TypeKey)
                    {
                        original = preset.ResolveLiveMaterial(source.AssetPath);
                        if (original == null) target.Unsupported.Add("Original material недоступен: " + source.AssetPath + ". Сохраняется Current material.");
                        else if (!string.IsNullOrEmpty(source.ShaderName) && original.shader.name != source.ShaderName)
                            target.Unsupported.Add("Git shader был «" + source.ShaderName + "», текущий material использует «" + original.shader.name + "». Полное восстановление shader-кода требует отдельной сборки Git-снимка.");
                        break;
                    }
            Material clone = null;
            var block = new MaterialPropertyBlock();
            bool studioNeeded = false;
            VisualComparisonMode mode = VisualComparisonMode.Current;
            Material BaseMaterial() => mode == VisualComparisonMode.Original && original != null ? original : current;
            void EnsureStudio()
            {
                // EnemyWhiteFlash temporarily owns this renderer's material.
                // Keep its short hit feedback; the property block remains ready for the body material.
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_FlashBlend")) return;
                if (clone == null)
                {
                    clone = new Material(current) { name = current.name + " [Live Visual]", hideFlags = HideFlags.DontSave };
                    if (!clone.HasProperty("_StudioSaturation") && preset.LiveSpriteShader != null &&
                        current.shader.name == "Universal Render Pipeline/2D/Sprite-Lit-Default")
                        clone.shader = preset.LiveSpriteShader;
                }
                if (renderer.sharedMaterial != clone) renderer.sharedMaterial = clone;
            }
            bool SupportsStudio = renderer is SpriteRenderer && (current.HasProperty("_StudioSaturation") ||
                current.shader.name == "Universal Render Pipeline/2D/Sprite-Lit-Default" && preset.LiveSpriteShader != null);
            float Read(string name, float neutral)
            {
                renderer.GetPropertyBlock(block);
                int id = Shader.PropertyToID(name);
                return block.HasFloat(id) ? block.GetFloat(id) : current.HasProperty(name) ? current.GetFloat(name) : neutral;
            }
            void Write(string name, float value, bool studio = false)
            {
                if (studio && mode != VisualComparisonMode.Original) { studioNeeded = true; EnsureStudio(); }
                else if (studioNeeded && mode == VisualComparisonMode.Custom) EnsureStudio();
                renderer.GetPropertyBlock(block); block.SetFloat(name, value); renderer.SetPropertyBlock(block);
            }
            target.SetComparison = next =>
            {
                mode = next;
                if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty("_FlashBlend")) renderer.sharedMaterial = BaseMaterial();
                studioNeeded = false;
            };
            target.Release = () =>
            {
                if (renderer != null) renderer.sharedMaterial = current;
                if (clone != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(clone);
                    else UnityEngine.Object.DestroyImmediate(clone);
                }
            };
            target.Maintain = () =>
            {
                // Preserve the short combat flash, then return to the chosen comparison material.
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_FlashBlend")) return;
                if (mode == VisualComparisonMode.Original && original != null) renderer.sharedMaterial = original;
                else if (mode == VisualComparisonMode.Custom && studioNeeded) EnsureStudio();
            };
            if (SupportsStudio)
            {
                void Studio(string id, string label, float neutral, float min, float max) => target.Properties.Add(
                    new VisualProperty("shader." + id, label, "Коррекция пикселей · Lit 2D", () => Read(id, neutral), v => Write(id, v, true), min, max));
                Studio("_StudioHue", "Hue, градусы", 0, 0, 360);
                Studio("_StudioSaturation", "Насыщенность пикселей", 1, 0, 6);
                Studio("_StudioBrightness", "Яркость / HDR", 1, 0, 32);
                Studio("_StudioContrast", "Контраст", 1, 0, 8);
                Studio("_StudioGamma", "Gamma", 1, .05f, 8);
            }
            else if (renderer is SpriteRenderer)
                target.Unsupported.Add("Независимая коррекция пикселей недоступна этому shader: нужен сохраняющий его поведение адаптер. Tint не является насыщенностью текстуры.");
            var shader = current.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string name = shader.GetPropertyName(i);
                if (name.StartsWith("_Studio", StringComparison.Ordinal) ||
                    (shader.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) != 0 && name != "_Color" || name == "_ZWrite") continue;
                var type = shader.GetPropertyType(i);
                if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range)
                {
                    string property = name;
                    float min = 0, max = 32;
                    if (type == ShaderPropertyType.Range) { var limits = shader.GetPropertyRangeLimits(i); min = limits.x; max = Mathf.Max(limits.y, 1); }
                    // These are shader arithmetic parameters, not allocators; permit wider visual experiments.
                    if (name.Contains("Saturation")) max = 6;
                    if (name.Contains("Brightness") || name.Contains("Intensity")) max = 32;
                    target.Properties.Add(new VisualProperty("shader." + name, shader.GetPropertyDescription(i), "Материал · " + shader.name,
                        () => Read(property, 0), v => Write(property, v), min, max));
                }
                else if (type == ShaderPropertyType.Color)
                {
                    string property = name;
                    Color ReadColor() { renderer.GetPropertyBlock(block); return block.HasColor(property) ? block.GetColor(property) : current.GetColor(property); }
                    VisualProperties.Color(target.Properties, "shader." + name, shader.GetPropertyDescription(i), "Материал · " + shader.name, ReadColor,
                        c => { renderer.GetPropertyBlock(block); block.SetColor(property, c); renderer.SetPropertyBlock(block); });
                }
                else if (type == ShaderPropertyType.Vector)
                {
                    string property = name;
                    for (int c = 0; c < 4; c++)
                    {
                        int channel = c;
                        Vector4 ReadVector() { renderer.GetPropertyBlock(block); return block.HasVector(property) ? block.GetVector(property) : current.GetVector(property); }
                        target.Properties.Add(new VisualProperty("shader." + name + "." + "xyzw"[c], name + " " + "xyzw"[c], "Материал · вектор",
                            () => ReadVector()[channel], v => { var vector = ReadVector(); vector[channel] = v; renderer.GetPropertyBlock(block); block.SetVector(property, vector); renderer.SetPropertyBlock(block); }, -1000, 1000));
                    }
                }
                else if (type == ShaderPropertyType.Texture) target.Unsupported.Add(name + ": замена текстуры не подключена; исходная текстура сохраняется.");
            }
            if (renderer.sharedMaterials.Length > 1) target.Unsupported.Add("Редактируется только первый material slot; остальные сохранены.");
        }

        private static void AddVolume(VisualTarget target, Volume volume)
        {
            var profile = volume.profile; // Unity creates a private runtime clone.
            if (profile == null) return;
            void Add(string id, string label, FloatParameter parameter, float min, float max)
            {
                bool originalOverride = parameter.overrideState;
                float original = parameter.value;
                target.Properties.Add(new VisualProperty(id, label, "Post-processing", () => parameter.value,
                    v => parameter.Override(v), min, max) { Restore = () => { parameter.value = original; parameter.overrideState = originalOverride; } });
            }
            if (profile.TryGet<Bloom>(out var bloom))
            {
                Add("post.bloom", "Bloom intensity", bloom.intensity, 0, 50);
                Add("post.bloomThreshold", "Bloom threshold", bloom.threshold, 0, 20);
            }
            if (profile.TryGet<ColorAdjustments>(out var color))
            {
                Add("post.exposure", "Exposure", color.postExposure, -10, 10);
                Add("post.saturation", "Насыщенность", color.saturation, -100, 100);
                Add("post.contrast", "Контраст", color.contrast, -100, 100);
                Add("post.hue", "Hue", color.hueShift, -180, 180);
            }
            target.Unsupported.Add("Post-processing влияет на изображение только при включённом post-processing игровой камеры и активном Volume.");
        }

        private static ParticleSystem.MinMaxGradient Multiply(ParticleSystem.MinMaxGradient source, Color tint)
        {
            var result = source;
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color: result.color = source.color * tint; break;
                case ParticleSystemGradientMode.TwoColors: result.colorMin = source.colorMin * tint; result.colorMax = source.colorMax * tint; break;
                case ParticleSystemGradientMode.TwoGradients: result.gradientMin = Multiply(source.gradientMin, tint); result.gradientMax = Multiply(source.gradientMax, tint); break;
                default: result.gradient = Multiply(source.gradient, tint); break;
            }
            return result;
        }
        private static Gradient Multiply(Gradient source, Color tint)
        {
            if (source == null) return new Gradient();
            var colors = source.colorKeys; var alphas = source.alphaKeys;
            for (int i = 0; i < colors.Length; i++) colors[i].color *= tint;
            for (int i = 0; i < alphas.Length; i++) alphas[i].alpha *= tint.a;
            var result = new Gradient { mode = source.mode }; result.SetKeys(colors, alphas); return result;
        }
    }
}
