from pathlib import Path
root=Path(__file__).resolve().parents[2]
def edit(rel, pairs):
 p=root/rel; s=p.read_text(encoding='utf-8-sig')
 for old,new in pairs:
  assert old in s,(rel,old[:90])
  s=s.replace(old,new,1)
 p.write_text(s,encoding='utf-8')
audio='Assets/_Project/scripts/Audio/'
edit(audio+'AudioCueId.cs', [('    StartRun = 62','''    StartRun = 62,
    EnemyHit = 70,
    EnemyCritical = 71,
    OrbitalSwordHit = 80,
    OrbitalImpulseFire = 81,
    OrbitalArcFire = 82,
    OrbitalCompress = 90,
    OrbitalRelease = 91,
    CorePulse = 92,
    CoreCascade = 93,
    ModuleInstall = 94,
    RewardSelect = 95''')])
edit(audio+'AudioCatalog.cs',[
 ('    public AudioCueId Id => id;','    [SerializeField, Range(0, 100)] private int priority;\n    public int Priority => Mathf.Clamp(priority, 0, 100);\n\n    public AudioCueId Id => id;')])
edit(audio+'AudioService.cs',[
 ('using UnityEngine.Audio;','using UnityEngine.Audio;\nusing UnityEngine.SceneManagement;'),
 ('    internal void Invalidate()', '''    public void SetIntensity(float intensity)
    {
        owner?.SetLoopIntensity(slotIndex, generation, intensity);
    }

    internal void Invalidate()'''),
 ('        public bool ManagedLoop;', '        public bool ManagedLoop;\n        public bool HasFollowTarget;'),
 ('    private void Update()\n    {\n        RefreshPool();','    private void Update()\n    {\n        if (Time.timeScale <= 0f) StopAllManagedLoops();\n        RefreshPool();'),
 ('            if (slot.CueId == AudioCueId.None || slot.FollowTarget == null)', '''            if (slot.ManagedLoop && slot.HasFollowTarget &&
                (slot.FollowTarget == null || !slot.FollowTarget.gameObject.activeInHierarchy))
            {
                slot.Source.Stop();
                ClearSlot(slot);
                continue;
            }

            if (slot.CueId == AudioCueId.None || slot.FollowTarget == null)'''),
 ('    public bool Play(AudioCueId cueId)', '''    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StopAllManagedLoops();
    }
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => StopAllManagedLoops();

    public bool Play(AudioCueId cueId)'''),
 ('        if (!TryPreparePlayback(cueId, out AudioCueDefinition definition, out AudioClip clip))','        if (Time.timeScale <= 0f) return null;\n        if (!TryPreparePlayback(cueId, out AudioCueDefinition definition, out AudioClip clip))'),
 ('        slot.FollowTarget = followTarget;','        slot.FollowTarget = followTarget;\n        slot.HasFollowTarget = followTarget != null;'),
 ('    private bool PlayInternal(AudioCueId cueId, Vector3 position, bool positional)', '''    internal void SetLoopIntensity(int slotIndex, int generation, float intensity)
    {
        if (!IsLoopPlaying(slotIndex, generation)) return;
        PoolSlot slot = pool[slotIndex];
        float t = Mathf.Clamp01(intensity);
        slot.Source.volume = slot.Definition.Volume * GetCategoryVolume(slot.Definition.Category) * Mathf.Lerp(.55f, 1f, t);
        slot.Source.pitch = Mathf.Lerp(.9f, 1.12f, t);
    }

    private bool PlayInternal(AudioCueId cueId, Vector3 position, bool positional)'''),
 ('        return null;\n    }\n\n    private int CountActive', '''        PoolSlot victim = null;
        foreach (PoolSlot candidate in pool)
        {
            if (candidate.Definition == null || candidate.Definition.Priority >= definition.Priority)
                continue;
            if (victim == null || candidate.Definition.Priority < victim.Definition.Priority)
                victim = candidate;
        }
        if (victim == null) return null;
        victim.Source.Stop();
        ClearSlot(victim);
        return victim;
    }

    private int CountActive'''),
 ('        source.pitch = Random.Range(definition.PitchMin, definition.PitchMax);','        source.pitch = Random.Range(definition.PitchMin, definition.PitchMax);\n        source.priority = 128 - definition.Priority;'),
 ('        slot.ManagedLoop = false;\n        slot.Source.clip = null;', '        slot.ManagedLoop = false;\n        slot.HasFollowTarget = false;\n        slot.Source.clip = null;'),
 ('    private void StopAllManagedLoops()', '    public void StopAllManagedLoops()')])
orb='Assets/_Project/scripts/Combat/OrbitalStation/'
edit(orb+'OrbitalModuleRuntime.cs',[
 ('BaseDamage * Power, OrbitalRewardIconResolver.ModuleColor(OrbitalModuleKind.Pistol));','BaseDamage * Power, OrbitalRewardIconResolver.ModuleColor(OrbitalModuleKind.Pistol));\n            AudioService.Instance?.PlayAt(AudioCueId.PistolShot, Visual.transform.position);'),
 ('                    Visual.transform.position);\n                TriggerPresentation();','                    Visual.transform.position);\n                AudioService.Instance?.PlayAt(AudioCueId.OrbitalSwordHit, Visual.transform.position);\n                TriggerPresentation();'),
 ('                target.transform.position);\n            TriggerPresentation();','                target.transform.position);\n            AudioService.Instance?.PlayAt(AudioCueId.OrbitalImpulseFire, Visual.transform.position);\n            TriggerPresentation();'),
 ('            if (targets.Count > 0)\n                Cooldown = 1.15f;', '''            if (targets.Count > 0)
            {
                AudioService.Instance?.PlayAt(AudioCueId.OrbitalArcFire, Visual.transform.position);
                Cooldown = 1.15f;
            }''')])
edit(orb+'OrbitalStationRuntime.cs',[
 ('        private bool compressionAnimating;', '        private bool compressionAnimating;\n        private AudioLoopHandle compressionAudio;'),
 ('        private float UpdateCompression(float deltaTime, bool held)\n        {', '''        private float UpdateCompression(float deltaTime, bool held)
        {
            bool audioAllowed = Time.timeScale > 0f && isActiveAndEnabled &&
                Owner != null && !Owner.IsDead && Owner.CanControl &&
                (RunStateManager.Instance == null || !RunStateManager.Instance.IsRunEnded);'''),
 ('            if (held != compressionHeld)\n            {', '''            if (held != compressionHeld)
            {
                StopCompressionAudio();
                if (audioAllowed)
                {
                    if (held)
                        compressionAudio = AudioService.Instance?.StartLoop(AudioCueId.OrbitalCompress, transform);
                    else
                        AudioService.Instance?.PlayAt(AudioCueId.OrbitalRelease, transform.position);
                }'''),
 ('            if (!compressionAnimating) return compressionRadius;', '''            if (!audioAllowed) StopCompressionAudio();
            compressionAudio?.SetIntensity(Mathf.InverseLerp(1f, compressedRadiusMultiplier, compressionRadius));
            if (!compressionAnimating) return compressionRadius;'''),
 ('        private void OnCoreWave(int level, int wave)\n        {','        private void OnCoreWave(int level, int wave)\n        {\n            AudioService.Instance?.PlayAt(level >= 2 ? AudioCueId.CoreCascade : AudioCueId.CorePulse, transform.position);'),
 ('        public void Teardown()\n        {','        public void Teardown()\n        {\n            StopCompressionAudio();'),
 ('        private void OnDestroy()', '''        private void OnDisable() => StopCompressionAudio();

        private void StopCompressionAudio()
        {
            compressionAudio?.Stop();
            compressionAudio = null;
        }

        private void OnDestroy()''')])
edit(orb+'OrbitalRewardFlowController.cs',[
 ('                    station.Rings.FirstOrDefault(r => r.RingId == targetRingId)?.Pulse();','                    AudioService.Instance?.PlayAt(AudioCueId.ModuleInstall,\n                        reservedMount?.Transform != null ? reservedMount.Transform.position : station.transform.position);\n                    station.Rings.FirstOrDefault(r => r.RingId == targetRingId)?.Pulse();')])
edit('Assets/_Project/scripts/Run/Flow/RunFlowController.cs',[
 ('    public void StopRunGameplay()\n    {','    public void StopRunGameplay()\n    {\n        AudioService.Instance?.StopAllManagedLoops();')])
edit('Assets/_Project/scripts/Progression/RunUpgrades/UpgradeManager.cs',[
 ('        CompleteGrantedReward(upgrade);','        AudioService.Instance?.Play(AudioCueId.RewardSelect);\n        CompleteGrantedReward(upgrade);'),
 ('                    out ItemGrantResult bodyResult))\n                CompleteGrantedReward(reward);', '''                    out ItemGrantResult bodyResult))
            {
                AudioService.Instance?.Play(AudioCueId.RewardSelect);
                CompleteGrantedReward(reward);
            }'''),
 ('        if (!started && isChoosingUpgrade)', '        if (started) AudioService.Instance?.Play(AudioCueId.RewardSelect);\n        if (!started && isChoosingUpgrade)')])
print('Production scripts updated')
