using System.Collections.Generic;
using UnityEngine;

public sealed class GravityTrajectoryService : MonoBehaviour
{
    public readonly struct Prediction
    {
        public EnemyHealth Target { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }

        public Prediction(EnemyHealth target, Vector2 start, Vector2 end)
        {
            Target = target;
            Start = start;
            End = end;
        }
    }

    private GravityZone gravityZone;
    private int maxTargets;
    private float predictionLength;

    public bool IsEnabled => maxTargets > 0 && predictionLength > 0f;
    public int MaxTargets => maxTargets;
    public float PredictionLength => predictionLength;

    public void ConfigureScanner(int targetCount, float seconds)
    {
        maxTargets = Mathf.Clamp(targetCount, 0, 8);
        predictionLength = Mathf.Clamp(seconds, 0f, 3f);
    }

    public void Disable()
    {
        maxTargets = 0;
        predictionLength = 0f;
    }

    public void SetGravityZone(GravityZone zone)
    {
        gravityZone = zone;
    }

    public void CollectPredictions(List<Prediction> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (!IsEnabled || gravityZone == null)
            return;

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (results.Count >= maxTargets)
                break;

            if (enemy == null || enemy.IsDead ||
                !gravityZone.ContainsWorldPosition(enemy.transform.position))
            {
                continue;
            }

            EnemyMovement movement = enemy.GetComponent<EnemyMovement>();

            if (movement == null)
                continue;

            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            Vector2 start = enemy.transform.position;
            Vector2 velocity = body != null
                ? body.linearVelocity
                : Vector2.zero;
            velocity += gravityZone.GetPredictedExternalVelocity(
                start,
                movement
            );
            results.Add(new Prediction(
                enemy,
                start,
                start + velocity * predictionLength
            ));
        }
    }
}
