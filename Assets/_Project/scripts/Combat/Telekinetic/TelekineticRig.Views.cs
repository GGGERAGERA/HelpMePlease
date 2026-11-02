using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace Subject42.Combat.Telekinetic { public sealed partial class TelekineticRig {
        // Local bands inside the rig; authored gun groups retain their own internal ordering.
        private const int ChassisOrder=0, ModuleOrder=1, ChargeOrder=2, WeaponOrder=3;
        private readonly Dictionary<int,ParticleSystem[]> shotParticles=new();
        private readonly Dictionary<int,float> shotEffectEnds=new();
        private void PrepareWeaponEffects(int id,Subject42.Combat.OrbitalStation.OrbitalModuleView view)
        {
            var particles=view.Particles.Where(p=>p!=null).SelectMany(p=>p.GetComponentsInChildren<ParticleSystem>(true)).Distinct().ToArray();
            shotParticles[id]=particles;
            foreach (var particle in particles)
            {
                particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particle.main; main.loop=false; main.playOnAwake=false;
            }
        }
        private void TriggerWeaponEffects(TelekineticItem item)
        {
            if (!shotParticles.TryGetValue(item.Id,out var particles)) return;
            foreach (var particle in particles) { particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear); particle.Play(false); }
            // Same short shot feedback windows as the existing production OrbitalModuleVisual.
            shotEffectEnds[item.Id]=Time.time+(item.Kind==TelekineticPart.Shotgun?.22f:.12f);
        }
        private void TickWeaponEffects()
        {
            foreach (int id in shotEffectEnds.Where(p=>Time.time>=p.Value).Select(p=>p.Key).ToArray())
            {
                if (shotParticles.TryGetValue(id,out var particles))
                    foreach (var particle in particles) particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                shotEffectEnds.Remove(id);
            }
        }
        private static void SetVisualGroup(Transform root,int layer,int order)
        {
            var group=root.GetComponent<SortingGroup>();
            if (group==null) group=root.gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID=layer; group.sortingOrder=order; group.sortAtRoot=false;
        }
        private void ConfigureRigSorting()
        {
            if (Construction.GetComponent<SortingGroup>()!=null) return;
            var owner=station.Owner.Transform;
            var characterGroup=owner.GetComponentInParent<SortingGroup>();
            var visual=owner.GetComponent<CharacterMovement2D>()?.VisualRoot;
            var body=visual!=null ? visual.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r.enabled) : null;
            int layer=body!=null ? body.sortingLayerID : SortingLayer.NameToID("Default");
            int floor=body!=null ? body.sortingOrder : 0;
            foreach (var renderer in owner.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.enabled && !renderer.transform.IsChildOf(station.transform) && renderer.sortingLayerID==layer &&
                    renderer.GetComponentInParent<SortingGroup>()==characterGroup)
                    floor=Mathf.Min(floor,renderer.sortingOrder);
            // The production rig shares the player's outer group. Compare on the body's INTERNAL
            // layer, below its shadow, rather than comparing Player-layer meshes with Default sprites.
            SetVisualGroup(Construction,layer,floor-1);
            SetVisualGroup(Construction.Cast<Transform>().First(t=>t.name=="Telekinetic chassis / visual only"),layer,ChassisOrder);
        }
        private void SyncViews()
        {
            ConfigureRigSorting();
            int visualLayer=Construction.GetComponent<SortingGroup>().sortingLayerID;
            var ids=new HashSet<int>(Build.Items.Select(c=>c.Id));
            foreach (var id in views.Keys.Where(id=>!ids.Contains(id)).ToArray())
            { views[id].SetActive(false); Destroy(views[id]); views.Remove(id); chargeBars.Remove(id); weaponBodies.Remove(id); shotParticles.Remove(id); shotEffectEnds.Remove(id); }
            presentationRadius=CellSpacing*.65f;
            foreach (var c in Build.Items)
            {
                if (c.IsModule)
                {
                    float cooldown=Modules.Cooldown(c,Build);
                    c.Remaining=c.LastCooldown>0 ? cooldown*Mathf.Clamp01(c.Remaining/c.LastCooldown) : cooldown;
                    c.LastCooldown=cooldown;
                }
                if (!views.TryGetValue(c.Id,out var go))
                {
                    go=new GameObject(TelekineticBuild.Label(c.Kind)+" #"+c.Id); go.transform.SetParent(Construction,false); views.Add(c.Id,go);
                    if (c.IsWeapon)
                    {
                        SetVisualGroup(go.transform,visualLayer,WeaponOrder);
                        var template=c.Kind==TelekineticPart.Pistol?PistolVisual:c.Kind==TelekineticPart.Shotgun?ShotgunVisual:c.Kind==TelekineticPart.Sniper?SniperVisual:SawVisual;
                        var art=Instantiate(template,go.transform); art.transform.localPosition=Vector3.zero; art.transform.localRotation=Quaternion.identity;
                        var authoredBody=art.GetComponent<Subject42.Combat.OrbitalStation.OrbitalModuleView>();
                        PrepareWeaponEffects(c.Id,authoredBody); art.SetActive(true);
                        weaponBodies[c.Id]=authoredBody.Body.GetComponentsInChildren<SpriteRenderer>(true);
                    }
                    else
                    {
                        var body=new GameObject("Pixel node").transform; body.SetParent(go.transform,false);
                        // Scale the renderer child; the cell transform remains in metres.
                        var node=body.gameObject.AddComponent<SpriteRenderer>(); ApplyIcon(node,c.Kind);
                        node.sortingLayerID=visualLayer; node.sortingOrder=ModuleOrder;
                        body.localScale=Vector3.one*(c.Kind==TelekineticPart.Frame?.16f:.4f);
                    }
                    if (c.IsModule)
                    {
                        var barGo=new GameObject("Independent cooldown charge"); barGo.transform.SetParent(go.transform,false);
                        barGo.transform.localPosition=new Vector3(0,-.28f,0);
                        var bar=barGo.AddComponent<SpriteRenderer>(); bar.sprite=ModuleSprite; bar.sharedMaterial=SpriteMaterial;
                        bar.sortingLayerID=visualLayer; bar.sortingOrder=ChargeOrder; chargeBars.Add(c.Id,bar);
                    }
                }
                go.transform.localPosition=c.Center*CellSpacing;
                go.transform.localRotation=Quaternion.Euler(0,0,c.IsWeapon ? Mathf.Atan2(c.Center.y,c.Center.x)*Mathf.Rad2Deg : 0);
                if (c.Kind==TelekineticPart.Saw) go.transform.localScale=Vector3.one*station.State.ContactSizeMultiplier;
                float reach=CellSpacing*.75f;
                if (weaponBodies.TryGetValue(c.Id,out var bodies))
                    foreach (var body in bodies)
                    {
                        Vector3 offset=body.transform.position-go.transform.position;
                        reach=Mathf.Max(reach,offset.magnitude+body.bounds.extents.magnitude);
                    }
                presentationRadius=Mathf.Max(presentationRadius,c.Center.magnitude*CellSpacing+reach);
            }
            StructureView.Rebuild();
        }

} }
