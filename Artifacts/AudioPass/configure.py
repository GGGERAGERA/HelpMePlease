from pathlib import Path
import re,json
root=Path(__file__).resolve().parents[2]
p=root/'Assets/_Project/scripts/Combat/Enemies/EnemyHealth.cs'
s=p.read_text(encoding='utf-8-sig')
start=s.index('        if (hitSound == null)\n            return;',s.index('    private void Awake()'))
end=s.index('\n    }',start)
s=s[:start]+s[end:]
s=s.replace('PlayHitSound();','PlayHitSound(isCritical);')
start=s.index('            if (critSound != null)')
end=s.index('            var main = blood.main;',start)
s=s[:start]+s[end:]
start=s.index('    private void PlayHitSound()')
end=s.index('\n    private void Death()',start)
s=s[:start]+'''    private void PlayHitSound(bool isCritical)
    {
        // One bounded cue per damage event; critical replaces the ordinary hit.
        AudioService.Instance?.PlayAt(
            isCritical ? AudioCueId.EnemyCritical : AudioCueId.EnemyHit,
            transform.position);
    }
'''+s[end:]
# Retain serialized legacy fields for asset compatibility, but no per-enemy sources are created.
s=s.replace('    private static float lastCritSoundTime;\n','')
p.write_text(s,encoding='utf-8')
base='Assets/_Project/AudioEffects/'
impact='packs/kenney_impact-sounds/'
scifi='packs/kenney_sci-fi-sounds/'
interface='packs/kenney_interface-sounds/'
# id, clips, volume, pitch min/max, cooldown, cap, category, loop, priority
rows=[
(20,['SFX/Game/Pistol_shot_SFX.mp3'],.22,.96,1.04,.085,3,1,0,20),
(70,[impact+f'impactSoft_medium_00{i}.mp3' for i in range(3)],.09,.92,1.08,.065,2,1,0,0),
(71,['crit.mp3'],.10,.98,1.04,.12,1,1,0,5),
(40,[impact+f'impactPunch_heavy_00{i}.mp3' for i in range(3)],.16,.92,1.08,.10,2,1,0,10),
(30,['Зомби, получение урона.mp3'],.65,.97,1.03,.18,1,1,0,100),
(80,['Sword_Laser.mp3'],.12,.95,1.05,.12,2,1,0,20),
(81,[scifi+'lowFrequency_explosion_001.mp3'],.28,.93,1.03,.15,2,1,0,25),
(82,['Assets/Epic Toon FX/Sound/etfx_shoot_lightning2.wav'],.20,.96,1.04,.15,2,1,0,25),
(90,[scifi+'engineCircular_000.mp3'],.10,1,1,0,1,1,1,30),
(91,[scifi+'laserLarge_003.mp3'],.34,.97,1.03,.12,1,1,0,60),
(92,[scifi+'forceField_000.mp3'],.22,.99,1.01,.12,1,1,0,40),
(93,[scifi+'explosionCrunch_000.mp3'],.28,.98,1.02,.15,1,1,0,45),
(94,[interface+'confirmation_001.mp3'],.32,.98,1.02,.08,2,1,0,60),
(50,['pickupCoin.wav'],.045,.98,1.10,.08,1,1,0,0),
(51,['new/Level_up_test.mp3'],.70,1,1,.5,1,1,0,80),
(95,['SFX/UI/Menu/Button_confirm.wav'],.38,.98,1.02,.08,1,2,0,80)]
path=root/'Assets/_Project/Resources/Audio/VerticalSliceAudioCatalog.asset'
catalog=path.read_text()
summary=[]
for id,clips,vol,pmin,pmax,cd,cap,cat,loop,pri in rows:
 paths=[c if c.startswith('Assets/') else base+c for c in clips]
 guids=[re.search(r'^guid: (\w+)',(root/(c+'.meta')).read_text(),re.M)[1] for c in paths]
 block=f'  - id: {id}\n    clips:\n'+''.join(f'    - {{fileID: 8300000, guid: {g}, type: 3}}\n' for g in guids)
 block+=f'    volume: {vol}\n    pitchMin: {pmin}\n    pitchMax: {pmax}\n    spatialBlend: 0\n    cooldown: {cd}\n    maxSimultaneous: {cap}\n    loop: {loop}\n    category: {cat}\n    priority: {pri}\n'
 pattern=rf'  - id: {id}\n.*?(?=  - id: |\Z)'
 if re.search(pattern,catalog,re.S): catalog=re.sub(pattern,lambda m:block,catalog,flags=re.S)
 else: catalog+=block
 summary.append(dict(id=id,clips=paths,volume=vol,pitch=[pmin,pmax],cooldown=cd,maxSimultaneous=cap,loop=loop,priority=pri))
path.write_text(catalog)
(root/'Artifacts/AudioPass/cues.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print('16 production cues configured; existing clip assets unchanged')
