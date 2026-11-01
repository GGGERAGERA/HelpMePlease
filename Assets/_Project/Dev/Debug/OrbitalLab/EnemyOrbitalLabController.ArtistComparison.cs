#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class EnemyOrbitalLabController
{
    [Header("Artist references — EnemyOrbitalLab only")]
    public bool ArtistComparisonEnabled;
    public GameObject[] ArtistSpiderPrefabs;
    public bool ArtistCleanView = true;
    public bool HasArtistComparison => HasInterceptionExperiment && ArtistComparisonEnabled;
    [Serializable] public sealed class TurretArtCandidate
    {
        public string Name, Source;
        public GameObject Visual, CombatPrefab, LaserVisual;
    }
    public TurretArtCandidate[] ArtistTurrets;
    public int LaserTurretArtistIndex = -1;
    readonly List<GameObject> turretArtGallery = new();
    public bool TurretGalleryVisible => turretArtGallery.Count > 0;

    internal bool CreateArtistLaserTurretVisual(Transform parent)
    {
        if(!HasArtistComparison || ArtistTurrets==null || LaserTurretArtistIndex<0 || LaserTurretArtistIndex>=ArtistTurrets.Length) return false;
        var prefab=ArtistTurrets[LaserTurretArtistIndex].LaserVisual;
        if(prefab==null) return false;
        Instantiate(prefab,parent,false);
        return true;
    }
    public void ShowTurretArtGallery()
    {
        if(!HasArtistComparison || !Ready || ArtistTurrets==null) return;
        HideTurretArtGallery(); SetPaused(true);
        Vector2[] positions={new(-10,16),new(0,16),new(10,16),new(-10,9),new(0,9),new(10,9),new(-10,-6),new(0,-6),new(10,-6)};
        for(int i=0;i<ArtistTurrets.Length;i++)
        {
            GameObject visual;
            if(ArtistTurrets[i].Visual!=null) visual=Instantiate(ArtistTurrets[i].Visual,transform,false);
            else
            {
                visual=new GameObject("Current interception turret reference"); visual.transform.SetParent(transform,false);
                var sprite=visual.AddComponent<SpriteRenderer>(); sprite.sprite=LaserTurretSprite();
                visual.transform.localScale=Vector3.one*2.2f;
            }
            visual.name="Turret reference "+(char)('A'+i)+" · "+ArtistTurrets[i].Name;
            visual.transform.position=Player.transform.position+(Vector3)positions[i%positions.Length];
            foreach(var sprite in visual.GetComponentsInChildren<SpriteRenderer>())
            { sprite.sortingLayerName="Player"; sprite.sortingOrder=140; }
            turretArtGallery.Add(visual);
        }
        CameraRig.BeginWorldBoundsFocus(this,Player.transform.position+Vector3.up*5,15f);
    }
    public void HideTurretArtGallery()
    {
        foreach(var item in turretArtGallery) if(item!=null) Destroy(item);
        turretArtGallery.Clear();
        if(CameraRig!=null) CameraRig.EndWorldBoundsFocus(this);
    }
    public EnemyHealth SpawnArtistTurret(int index)
    {
        if(!HasArtistComparison || !Ready || ArtistTurrets==null || index<0 || index>=ArtistTurrets.Length) return null;
        var prefab=ArtistTurrets[index].CombatPrefab;
        if(prefab==null || !TryManualSpawnPosition(ManualSpawnRadius(prefab),out var position)) return null;
        var instance=Instantiate(prefab,position,Quaternion.identity,transform);
        instance.name=prefab.name+" (Lab turret)";
        ConfigureLabEnemyProjectilePool(instance);
        var health=instance.GetComponent<EnemyHealth>();
        var dummy=instance.AddComponent<LabEnemyDummy>(); dummy.Initialize(); dummy.Invulnerable=EnemiesInvincible;
        health.NotifySpawnConfigured(); enemies.Add(health); manualEnemies.Add(health,ManualSpawnRadius(prefab));
        return health;
    }
    // Retained spider demonstration; no orbital visual or construction changes.
    public void PrepareArtistComparisonField()
    {
        if(!HasArtistComparison || !Ready) return;
        SetPaused(true); ClearDensityEnemies(); SetAutomaticEnemySpawning(false);
        AutomaticInterceptions=false; CancelInterception();
        for(int i=0;ArtistSpiderPrefabs!=null && i<ArtistSpiderPrefabs.Length;i++) SpawnManualEnemies(-100-i,1);
        SetPlayerInvincible(true);
    }
    internal void DrawArtistComparison(GUIStyle heading,GUIStyle small,GUIStyle button)
    {
        if(!HasArtistComparison) return;
        GUILayout.Label("СТАЦИОНАРНЫЕ ТУРЕЛИ · LAB ONLY",heading);
        if(GUILayout.Button(TurretGalleryVisible?"Скрыть витрину турелей":"Показать все найденные турели",button))
        { if(TurretGalleryVisible) HideTurretArtGallery(); else ShowTurretArtGallery(); }
        GUILayout.Label("Витрина: высота 2,2 м для сравнения арта. Исходные имена подписаны на поле.",small);
        if(ArtistTurrets!=null) for(int i=0;i<ArtistTurrets.Length;i++)
        {
            var candidate=ArtistTurrets[i];
            if(candidate.CombatPrefab==null) continue;
            GUILayout.Label(new GUIContent($"{(char)('A'+i)} · {candidate.Name}",candidate.Source),small);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Вызвать prefab",button)) SpawnArtistTurret(i);
            if(GUILayout.Button("Визуал Перехвата",button)) LaserTurretArtistIndex=i;
            GUILayout.EndHorizontal();
        }
        if(GUILayout.Button("Перехват: текущий визуал",button)) LaserTurretArtistIndex=-1;
        GUILayout.Label("Перехват: "+(LaserTurretArtistIndex<0?"текущий":ArtistTurrets[LaserTurretArtistIndex].Name),small);
        ArtistCleanView=GUILayout.Toggle(ArtistCleanView,"Чистый вид · убрать маркеры конструктора");
        if(GUILayout.Button("Показать прежних пять пауков",button)) PrepareArtistComparisonField();
        GUILayout.Space(4);
    }
    internal void DrawArtistComparisonLabels(Camera camera,GUIStyle style)
    {
        if(!HasArtistComparison || !Paused || camera==null) return;
        for(int i=0;i<turretArtGallery.Count;i++)
        {
            if(turretArtGallery[i]==null) continue;
            var point=camera.WorldToScreenPoint(turretArtGallery[i].transform.position);
            float left=Mathf.Clamp(point.x-100,camera.pixelRect.x+6,Screen.width-212);
            GUI.Label(new Rect(left,Screen.height-point.y+13,210,62),$"{(char)('A'+i)} · {ArtistTurrets[i].Name}\n{ArtistTurrets[i].Source}",style);
        }
        foreach(var actor in ManualEnemies)
        {
            if(actor==null || actor.IsDead) continue;
            int index=Array.FindIndex(ArtistSpiderPrefabs,p=>p!=null && actor.name==p.name+" (Lab)");
            if(index<0) continue;
            var sprite=actor.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(s=>s.enabled);
            var point=camera.WorldToScreenPoint(sprite!=null?sprite.bounds.center+Vector3.up*(sprite.bounds.extents.y+.2f):actor.transform.position);
            GUI.Label(new Rect(Mathf.Clamp(point.x-40,camera.pixelRect.x+8,Screen.width-188),Screen.height-point.y-20,180,24),ManualEnemyName(-100-index),style);
        }
    }
}
#endif
