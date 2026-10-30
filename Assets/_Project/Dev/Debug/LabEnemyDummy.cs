#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

public enum LabEnemyDummyMode
{
    Static,
    NormalAi
}

// Shared lab actor policy: use production health/AI without granting run rewards.
[DisallowMultipleComponent]
public sealed class LabEnemyDummy : EnemyDebugDamagePolicy
{
    private readonly List<Behaviour> controlledBehaviours = new();
    private readonly List<bool> originalEnabledStates = new();
    private Rigidbody2D body;
    private RigidbodyType2D originalBodyType;

    public override bool SuppressesProductionRewards => true;
    public override bool Invulnerable { get; set; } = true;
    public EnemyHealth Health { get; private set; }

    public void Initialize()
    {
        Health = GetComponent<EnemyHealth>();
        body = GetComponent<Rigidbody2D>();
        if (body != null)
            originalBodyType = body.bodyType;

        CaptureBehaviour(GetComponent<EnemyMovement>());
        CaptureBehaviour(GetComponent<EnemyCollisionHandler>());
        CaptureBehaviour(GetComponent<TurretEnemyBehaviour>());
        CaptureBehaviour(GetComponent<EyesEnemyBehaviour>());
    }

    public void ApplyMode(LabEnemyDummyMode mode)
    {
        bool normalAi = mode == LabEnemyDummyMode.NormalAi;
        for (int i = 0; i < controlledBehaviours.Count; i++)
        {
            Behaviour behaviour = controlledBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = normalAi && originalEnabledStates[i];
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = normalAi ? originalBodyType : RigidbodyType2D.Kinematic;
        }
    }

    public void MoveRoot(Vector2 position)
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
        }
        else
        {
            transform.position = position;
        }
    }

    private void CaptureBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || controlledBehaviours.Contains(behaviour))
            return;
        controlledBehaviours.Add(behaviour);
        originalEnabledStates.Add(behaviour.enabled);
    }
}
#endif
