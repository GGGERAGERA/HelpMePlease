using System;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

namespace Subject42.Visuals
{
    // Only explicit presentation fields are admitted. BodySize, radii, cadence and progression stay outside this adapter.
    public static class OrbitalVisualAdapter
    {
        public static VisualTarget CreateTarget()
        {
            if (!OrbitalPresentationConfig.IsConfigured) return null;
            var config = OrbitalPresentationConfig.Active;
            var target = new VisualTarget("config:orbital", "config:orbital", "ORBITAL", "ORBITAL · визуальная конфигурация", null);
            target.Alive = () => config != null && OrbitalPresentationConfig.IsConfigured && OrbitalPresentationConfig.Active == config;
            target.Unsupported.Add("Размер тела снаряда BodySize связан со swept hit shape: изменять через визуальный ползунок небезопасно.");
            target.Unsupported.Add("Геометрия/радиусы орбит, hit radius, отдача и тайминги атак относятся к gameplay и не включены.");
            target.Unsupported.Add("FlashLink: настройки семейства действуют на активные вспышки; уже завершившаяся вспышка не возрождается при увеличении времени.");
            const string rings = "ORBITAL · Кольца";
            Add(target, "config.RingSurface.Saturation", "Насыщенность", rings,
                () => config.RingSurface.Saturation, x => config.RingSurface.Saturation = x, 0, 8);
            Add(target, "config.RingSurface.Brightness", "Яркость", rings,
                () => config.RingSurface.Brightness, x => config.RingSurface.Brightness = x, 0, 32);
            Add(target, "config.RingSurface.Glow", "Свечение", rings,
                () => config.RingSurface.Glow, x => config.RingSurface.Glow = x, 0, 16);
            Add(target, "config.RingLineWidth", "Толщина", rings,
                () => config.RingLineWidth, x => config.RingLineWidth = x, 0, 2);
            Add(target, "config.RingLineAlpha", "Прозрачность", rings,
                () => config.RingLineAlpha, x => config.RingLineAlpha = x, 0, 1);
            Add(target, "config.RingSurface.Pattern", "Узор: 0 цельный / 1 штрих / 2 энергия", rings,
                () => (int)config.RingSurface.Pattern, x => config.RingSurface.Pattern = (OrbitalPresentationConfig.RingPattern)Mathf.RoundToInt(x), 0, 2);
            Add(target, "config.RingSurface.DashCount", "Число штрихов", rings,
                () => config.RingSurface.DashCount, x => config.RingSurface.DashCount = Mathf.RoundToInt(x), 1, 512);
            Add(target, "config.RingSurface.DashFill", "Заполнение штриха", rings,
                () => config.RingSurface.DashFill, x => config.RingSurface.DashFill = x, 0, 1);
            Add(target, "config.RingSurface.PatternSpeed", "Скорость узора", rings,
                () => config.RingSurface.PatternSpeed, x => config.RingSurface.PatternSpeed = x, -10, 10);
            Add(target, "config.RingUpgradeBrightness", "Яркость улучшения", rings,
                () => config.RingUpgradeBrightness, x => config.RingUpgradeBrightness = x, 0, 16);
            Add(target, "config.RingUpgradeGlow", "Свечение улучшения", rings,
                () => config.RingUpgradeGlow, x => config.RingUpgradeGlow = x, 0, 16);
            Add(target, "config.RingHighlightBrightness", "Яркость выделения", rings,
                () => config.RingHighlightBrightness, x => config.RingHighlightBrightness = x, 0, 16);
            Add(target, "config.RingDimmedAlpha", "Alpha затемнённых колец", rings,
                () => config.RingDimmedAlpha, x => config.RingDimmedAlpha = x, 0, 1);
            for (int tier = 1; tier <= 4; tier++)
            {
                int level = tier;
                string id = "config.Tier" + new[] { "", "I", "II", "III", "IV" }[level];
                string category = "ORBITAL · Уровень кольца " + level;
                VisualProperties.Color(target.Properties, id + ".BaseColor", "Цвет", category,
                    () => config.GetRingTier(level).BaseColor, color => ChangeTier(config, level, s => { s.BaseColor = color; return s; }), 16);
                AddTier(target, config, level, id, "Alpha", "Прозрачность", category, s => s.Alpha, (s, x) => { s.Alpha = x; return s; }, 1);
                AddTier(target, config, level, id, "Emission", "Интенсивность", category, s => s.Emission, (s, x) => { s.Emission = x; return s; }, 32);
                AddTier(target, config, level, id, "Glow", "Свечение", category, s => s.Glow, (s, x) => { s.Glow = x; return s; }, 16);
                AddTier(target, config, level, id, "CoreWhitening", "Белая сердцевина", category, s => s.CoreWhitening, (s, x) => { s.CoreWhitening = x; return s; }, 1);
                AddTier(target, config, level, id, "PulseStrength", "Сила пульсации", category, s => s.PulseStrength, (s, x) => { s.PulseStrength = x; return s; }, 1);
                AddTier(target, config, level, id, "PulseSpeed", "Частота пульсации", category, s => s.PulseSpeed, (s, x) => { s.PulseSpeed = x; return s; }, 20);
            }

            const string cannon = "ORBITAL · Оружейные эффекты";
            Add(target, "config.ShotHaloDuration", "Время ореола выстрела", cannon, () => config.ShotHaloDuration, x => config.ShotHaloDuration = x, 0, 5);
            Add(target, "config.ShotHaloAlpha", "Alpha ореола выстрела", cannon, () => config.ShotHaloAlpha, x => config.ShotHaloAlpha = x, 0, 1);
            Add(target, "config.ShotHaloScale", "Размер ореола выстрела", cannon, () => config.ShotHaloScale, x => config.ShotHaloScale = x, 0, 10);
            Add(target, "config.ProjectileMuzzleDuration", "Время вспышки ствола", cannon, () => config.ProjectileMuzzleDuration, x => config.ProjectileMuzzleDuration = x, 0, 5);
            Add(target, "config.ProjectileImpactDuration", "Время попадания", cannon, () => config.ProjectileImpactDuration, x => config.ProjectileImpactDuration = x, 0, 5);
            Add(target, "config.ProjectileImpactSize", "Размер попадания", cannon, () => config.ProjectileImpactSize, x => config.ProjectileImpactSize = x, 0, 10);
            Add(target, "config.ProjectileImpactWhitening", "Белая примесь попадания", cannon, () => config.ProjectileImpactWhitening, x => config.ProjectileImpactWhitening = x, 0, 1);
            Add(target, "config.FlashTailFade", "Затухание FlashLink (0 — старое)", cannon, () => config.FlashTailFade, x => config.FlashTailFade = x, 0, 1);
            AddProjectile(target, "PistolProjectile", "Пистолет", config.PistolProjectile);
            AddProjectile(target, "ShotgunProjectile", "Дробовик", config.ShotgunProjectile);
            AddProjectile(target, "SniperProjectile", "Снайпер", config.SniperProjectile);
            foreach (OrbitalFlashFamily family in Enum.GetValues(typeof(OrbitalFlashFamily)))
                AddFlashFamily(target, config, family);
            foreach (var property in target.Properties)
            {
                if (property.Id is "config.RingSurface.Pattern" or "config.RingSurface.DashCount") property.IsInteger = true;
                if (property.Id is "config.ProjectileMuzzleDuration" or "config.ProjectileImpactDuration")
                    property.Note = "Базовое время для следующих вспышек. Активные вспышки регулирует множитель времени соответствующего семейства FlashLink.";
                if (property.Id == "config.ShotHaloAlpha")
                    property.Note = "Пиковая alpha выстрела; существующая подсветка выбора сохраняет authored alpha как нижний предел.";
            }
            return target;
        }

        private static void AddProjectile(VisualTarget target, string id, string label, OrbitalWeaponSettings.ProjectileStyle style)
        {
            string category = "ORBITAL · Снаряды · " + label;
            VisualProperties.Color(target.Properties, "config." + id + ".Color", "Цвет снаряда", category, () => style.Color, x => style.Color = x, 16);
            Add(target, "config." + id + ".TrailSeconds", "Время следа", category, () => style.TrailSeconds, x => style.TrailSeconds = x, 0, 5);
            Add(target, "config." + id + ".TrailWidth", "Толщина следа", category, () => style.TrailWidth, x => style.TrailWidth = x, 0, 2);
            Add(target, "config." + id + ".TrailOpacity", "Alpha следа", category, () => style.TrailOpacity, x => style.TrailOpacity = x, 0, 1);
        }

        private static void AddFlashFamily(VisualTarget target, OrbitalPresentationConfig config, OrbitalFlashFamily family)
        {
            var style = config.GetFlashStyle(family);
            string id = "flash." + family;
            string category = "FlashLink · " + (family switch
            {
                OrbitalFlashFamily.MuzzlePistol => "Выстрел пистолета",
                OrbitalFlashFamily.MuzzleShotgun => "Выстрел дробовика",
                OrbitalFlashFamily.MuzzleSniper => "Выстрел снайпера",
                OrbitalFlashFamily.ImpactPistol => "Попадание пистолета",
                OrbitalFlashFamily.ImpactShotgun => "Попадание дробовика",
                OrbitalFlashFamily.ImpactSniper => "Попадание снайпера",
                OrbitalFlashFamily.Impact => "Прочие попадания",
                OrbitalFlashFamily.Arc => "Электрическая дуга",
                OrbitalFlashFamily.Relocation => "Подсказка переноса",
                _ => "Прочие и лабораторные"
            });
            VisualProperties.Color(target.Properties, id + ".Tint", "Цвет", category, () => style.Tint, x => style.Tint = x, 16);
            Add(target, id + ".TintAmount", "Примесь цвета", category, () => style.TintAmount, x => style.TintAmount = x, 0, 1);
            Add(target, id + ".Brightness", "Яркость", category, () => style.Brightness, x => style.Brightness = x, 0, 32);
            Add(target, id + ".Opacity", "Alpha", category, () => style.Opacity, x => style.Opacity = x, 0, 1);
            Add(target, id + ".Duration", "Множитель времени", category, () => style.Duration, x => style.Duration = x, 0, 20);
            Add(target, id + ".Width", "Множитель толщины", category, () => style.Width, x => style.Width = x, 0, 20);
        }

        private static void Add(VisualTarget target, string id, string label, string category, Func<float> read, Action<float> write, float min, float max)
        {
            target.Properties.Add(new VisualProperty(id, label, category, read, x =>
            {
                if (!float.IsNaN(x) && !float.IsInfinity(x)) write(Mathf.Clamp(x, min, max));
            }, min, max));
        }

        private static void AddTier(VisualTarget target, OrbitalPresentationConfig config, int tier, string id,
            string property, string label, string category, Func<OrbitalPresentationConfig.RingTierStyle, float> read,
            Func<OrbitalPresentationConfig.RingTierStyle, float, OrbitalPresentationConfig.RingTierStyle> write, float max)
        {
            Add(target, id + "." + property, label, category, () => read(config.GetRingTier(tier)),
                x => ChangeTier(config, tier, style => write(style, x)), 0, max);
        }

        private static void ChangeTier(OrbitalPresentationConfig config, int tier,
            Func<OrbitalPresentationConfig.RingTierStyle, OrbitalPresentationConfig.RingTierStyle> change)
        {
            var style = change(config.GetRingTier(tier));
            switch (tier)
            {
                case 1: config.TierI = style; break;
                case 2: config.TierII = style; break;
                case 3: config.TierIII = style; break;
                case 4: config.TierIV = style; break;
            }
        }
    }
}
