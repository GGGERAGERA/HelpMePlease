using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class CarrierEscapeBehaviour : MonoBehaviour
{
    private EnemyMovement[] movements;
    private bool[] movementWasEnabled;
    private Rigidbody2D body;
    private GameplayAreaService gameplayArea;
    private CarrierTargetMarker marker;
    private WorldEventMarker offscreenIndicator;
    private Action escaped;
    private Vector2 destination;
    private float escapeSpeed;
    private bool running;
    private bool restored;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool movementWriterLogged;
#endif

    public bool Initialize(
        EnemyMovement[] targetMovements,
        bool[] targetMovementEnabledStates,
        Rigidbody2D targetBody,
        GameplayAreaService area,
        Vector2 playerPosition,
        float speed,
        Action onEscaped)
    {
        if (running)
            StopAndRestore();

        if (targetBody == null || area == null ||
            targetMovements == null ||
            targetMovementEnabledStates == null ||
            targetMovements.Length != targetMovementEnabledStates.Length)
        {
            return false;
        }

        movements = targetMovements;
        movementWasEnabled = targetMovementEnabledStates;
        body = targetBody;
        gameplayArea = area;
        escapeSpeed = Mathf.Max(0.1f, speed);
        escaped = onEscaped;
        destination = FindEscapeDestination(playerPosition);
        body.linearVelocity = Vector2.zero;

        restored = false;
        running = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        movementWriterLogged = false;
        Debug.Log(
            $"[CarrierEscape] Initialized '{name}': " +
            $"position={body.position}, player={playerPosition}, " +
            $"target={destination}, direction=" +
            $"{(destination - body.position).normalized}, " +
            $"movementWritersDisabled={movements.Length}.",
            this
        );
#endif
        return true;
    }

    public void SetIndicators(
        CarrierTargetMarker targetMarker,
        WorldEventMarker targetIndicator)
    {
        marker = targetMarker;
        offscreenIndicator = targetIndicator;
    }

    private void FixedUpdate()
    {
        if (!running || body == null || gameplayArea == null ||
            Time.timeScale == 0f)
        {
            return;
        }

        if (!gameplayArea.IsInsidePlayableArea(body.position))
        {
            running = false;
            Action callback = escaped;
            escaped = null;
            callback?.Invoke();
            return;
        }

        body.linearVelocity = Vector2.zero;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!movementWriterLogged)
        {
            movementWriterLogged = true;
            Debug.Log(
                $"[CarrierEscape] Movement writer=" +
                $"{nameof(CarrierEscapeBehaviour)}.FixedUpdate/" +
                $"Rigidbody2D.MovePosition, position={body.position}, " +
                $"direction={(destination - body.position).normalized}.",
                this
            );
        }
#endif

        Vector2 nextPosition = Vector2.MoveTowards(
            body.position,
            destination,
            escapeSpeed * Time.fixedDeltaTime
        );
        body.MovePosition(nextPosition);
    }

    public void StopAndRestore()
    {
        if (restored)
            return;

        running = false;
        escaped = null;

        marker?.SetSuppressed(true);
        offscreenIndicator?.SetSuppressed(true);

        if (marker != null)
            Destroy(marker.gameObject);

        marker = null;
        offscreenIndicator = null;

        if (body != null)
            body.linearVelocity = Vector2.zero;

        RestoreMovements();

        restored = true;
    }

    private Vector2 FindEscapeDestination(Vector2 playerPosition)
    {
        Collider2D playableArea = gameplayArea != null
            ? gameplayArea.PlayableArea
            : null;

        if (playableArea == null)
            return body != null ? body.position : transform.position;

        Bounds bounds = playableArea.bounds;
        Vector2 position = body != null ? body.position : transform.position;
        Vector2 directionAway = position - playerPosition;

        if (directionAway.sqrMagnitude > 0.0001f)
        {
            directionAway.Normalize();
            float xDistance = float.PositiveInfinity;
            float yDistance = float.PositiveInfinity;

            if (directionAway.x > 0.0001f)
                xDistance = (bounds.max.x - position.x) / directionAway.x;
            else if (directionAway.x < -0.0001f)
                xDistance = (bounds.min.x - position.x) / directionAway.x;

            if (directionAway.y > 0.0001f)
                yDistance = (bounds.max.y - position.y) / directionAway.y;
            else if (directionAway.y < -0.0001f)
                yDistance = (bounds.min.y - position.y) / directionAway.y;

            float distanceToEdge = Mathf.Min(xDistance, yDistance);

            if (!float.IsInfinity(distanceToEdge) && distanceToEdge >= 0f)
            {
                const float OutsideOffset = 1.5f;
                return position + directionAway *
                    (distanceToEdge + OutsideOffset);
            }
        }

        return FindNearestExitDestination(bounds, position);
    }

    private static Vector2 FindNearestExitDestination(
        Bounds bounds,
        Vector2 position)
    {
        float leftDistance = Mathf.Abs(position.x - bounds.min.x);
        float rightDistance = Mathf.Abs(bounds.max.x - position.x);
        float bottomDistance = Mathf.Abs(position.y - bounds.min.y);
        float topDistance = Mathf.Abs(bounds.max.y - position.y);
        float nearest = Mathf.Min(
            leftDistance,
            rightDistance,
            bottomDistance,
            topDistance
        );
        const float OutsideOffset = 1.5f;

        if (Mathf.Approximately(nearest, leftDistance))
            return new Vector2(bounds.min.x - OutsideOffset, position.y);

        if (Mathf.Approximately(nearest, rightDistance))
            return new Vector2(bounds.max.x + OutsideOffset, position.y);

        if (Mathf.Approximately(nearest, bottomDistance))
            return new Vector2(position.x, bounds.min.y - OutsideOffset);

        return new Vector2(position.x, bounds.max.y + OutsideOffset);
    }

    private void RestoreMovements()
    {
        if (movements == null || movementWasEnabled == null)
            return;

        int count = Mathf.Min(movements.Length, movementWasEnabled.Length);

        for (int i = 0; i < count; i++)
        {
            if (movements[i] != null)
                movements[i].enabled = movementWasEnabled[i];
        }

        movements = null;
        movementWasEnabled = null;
    }

    private void OnDisable()
    {
        StopAndRestore();
    }

    private void OnDestroy()
    {
        StopAndRestore();
    }
}
