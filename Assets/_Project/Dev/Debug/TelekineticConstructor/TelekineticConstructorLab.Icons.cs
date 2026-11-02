using Subject42.Combat.Telekinetic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Subject42.Combat.OrbitalStation;

namespace Subject42.DebugLabs.Telekinetic
{
    public sealed partial class TelekineticConstructorLab
    {
        public static bool WeaponKind(TelekineticPart kind) => kind>=TelekineticPart.Pistol && kind<=TelekineticPart.Saw;
        public static Color FrameTint(TelekineticPart kind) => WeaponKind(kind)?new Color(.28f,.92f,1,1):new Color(.4f,.43f,.48f,.62f);
        public static Color PlateTint(TelekineticPart kind) => WeaponKind(kind)?new Color(.045f,.19f,.25f,.96f):new Color(.12f,.13f,.16f,.32f);
        public Sprite Icon(TelekineticPart kind) => kind switch
        {
            TelekineticPart.DMG=>SegmentedModulePresentation.Icon(SegmentedLinkKind.Damage),
            TelekineticPart.FR=>SegmentedModulePresentation.Icon(SegmentedLinkKind.FireRate),
            TelekineticPart.Double=>SegmentedModulePresentation.Icon(SegmentedLinkKind.ExtraShot),
            TelekineticPart.RIC=>SegmentedModulePresentation.Icon(SegmentedLinkKind.Ricochet), _=>ItemIcons[(int)kind]
        };
        private void ApplyIcon(SpriteRenderer renderer,TelekineticPart kind)
        {
            renderer.sprite=Icon(kind); renderer.sharedMaterial=SpriteMaterial; renderer.color=PartColor(kind);
            var texture=new MaterialPropertyBlock(); texture.SetTexture("_MainTex",renderer.sprite.texture);
            renderer.SetPropertyBlock(texture);
        }
        private string Description(TelekineticPart kind,float strength=1,int level=1) => kind switch
        {
            TelekineticPart.DMG=>$"Следующая атака: +{(Modules.DamageMultiplier-1)*strength:P0} урона.",
            TelekineticPart.FR=>$"Частота: +{(Modules.RapidMultiplier-1)*strength:P0} на {Modules.RapidDuration:F1}s.",
            TelekineticPart.Double=>"Дополнительный залп / контакт на следующей атаке.",
            TelekineticPart.RIC=>$"Следующий залп: +{level} рикошет(а). Для снарядов.",
            TelekineticPart.Pierce=>$"Следующий залп: +{Mathf.RoundToInt(Modules.PierceBodies*strength)} пробития. Для снарядов.",
            TelekineticPart.Splash=>$"Первое попадание залпа: область {Modules.SplashRadius:F1} м, {Modules.SplashDamageFraction*strength:P0} урона соседним целям.",
            TelekineticPart.Chain=>$"Первое попадание: одна дополнительная цель до {Modules.ChainRadius:F1} м, {Modules.ChainDamageFraction*strength:P0} урона. Без рекурсии.",
            TelekineticPart.Crit=>$"Следующая атака: ×{1+(Modules.CritMultiplier-1)*strength:F2} урона.",
            TelekineticPart.Slow=>$"Следующие попадания: скорость ×{Mathf.Max(.1f,1-(1-Modules.SlowMultiplier)*strength):F2} на {Modules.SlowSeconds:F1}s.",
            TelekineticPart.Shred=>"Следующая атака обходит щиты. Не изменяет их постоянную прочность.",
            TelekineticPart.Knock=>$"Следующая атака: +{Modules.KnockBonus*strength:F1} к отталкиванию.",
            TelekineticPart.Echo=>$"Повтор одного залпа / контакта через {Modules.EchoDelay:F2}s. Без рекурсии.",
            TelekineticPart.Energy=>"Сокращает КД только соседних активных модулей.",
            TelekineticPart.Frame=>"Связность конструкции. Боевые эффекты не передаёт.",
            _=>"Ствол направлен наружу. Q/E поворачивает всю конструкцию."
        };
        private void DrawIcon(Rect rect,TelekineticPart kind)
        {
            var sprite=Icon(kind); var uv=sprite.textureRect; var texture=sprite.texture;
            float fit=Mathf.Min(rect.width/uv.width,rect.height/uv.height);
            var target=new Rect(rect.center.x-uv.width*fit*.5f,rect.center.y-uv.height*fit*.5f,uv.width*fit,uv.height*fit);
            var old=GUI.color; GUI.color=WeaponKind(kind)?Color.white:PartColor(kind);
            GUI.DrawTextureWithTexCoords(target,texture,new Rect(uv.x/texture.width,uv.y/texture.height,uv.width/texture.width,uv.height/texture.height)); GUI.color=old;
            GUI.Label(rect,new GUIContent("",TelekineticBuild.Label(kind)+" · "+ShapeLabel(kind)+"\n"+Description(kind)),GUIStyle.none);
        }
    }
}
#endif
