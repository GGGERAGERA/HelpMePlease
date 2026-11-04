using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/World Hazards/Rocket", fileName = "RocketHazard")]
public sealed class RocketHazardDefinition : WorldHazardDefinition
{
    [SerializeField] private GameObject rocketPrefab;
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private ParticleSystem explosionPrefab;
    [SerializeField, Min(1.5f)] private float warningDelay = 1.75f;
    [SerializeField, Min(.3f)] private float fallDuration = .5f;
    [SerializeField, Min(1f)] private float spawnHeight = 14f;
    [SerializeField, Min(.1f)] private float radius = 1.5f;
    [SerializeField, Min(0)] private int damage = 10;

    public override float DangerRadius => Mathf.Max(.1f, radius);
    public override bool IsConfigured => rocketPrefab != null &&
        rocketPrefab.GetComponent<ParticleSystem>() != null && targetPrefab != null &&
        targetPrefab.GetComponentInChildren<ParticleSystem>() != null && explosionPrefab != null;

    public override IWorldHazardAttack CreateAttack(MonoBehaviour owner) => new Attack(owner, this);

    private sealed class Attack : IWorldHazardAttack
    {
        private readonly RocketHazardDefinition definition;
        private readonly RocketAttackRunner runner;
        public bool IsBusy => runner.PendingCount > 0;

        public Attack(MonoBehaviour owner, RocketHazardDefinition definition)
        {
            this.definition = definition;
            runner = new RocketAttackRunner(owner, definition.rocketPrefab,
                definition.targetPrefab, definition.explosionPrefab);
        }

        public bool TryStart(Vector3 target) => !IsBusy && runner.Launch(target, null,
            Mathf.Max(1.5f, definition.warningDelay), Mathf.Max(.3f, definition.fallDuration), definition.DangerRadius);

        public void Tick(float deltaTime, Func<bool> combatAllowed) => runner.Tick(deltaTime,
            definition.spawnHeight, definition.DangerRadius, Mathf.Max(0, definition.damage), combatAllowed, true);
        public void Cancel() => runner.Cancel();
        public void Dispose() => runner.Dispose();
    }
}
