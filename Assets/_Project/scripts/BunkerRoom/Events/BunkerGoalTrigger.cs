using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BunkerGoalTrigger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string ballTag = "Ball";

    [Header("Objects To Hide")]
    [SerializeField] private GameObject ballRoot;
    [SerializeField] private GameObject goalRoot;

    [Header("Event")]
    [SerializeField] private BunkerEventManager eventManager;
    [SerializeField] private Sprite fullscreenSprite;
    [SerializeField] private float imageDuration = 5f;

    [Header("Respawn")]
    [SerializeField] private bool respawnAfterEvent = false;
    [SerializeField] private float respawnDelay = 10f;

    private bool triggered;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered)
            return;

        if (!other.CompareTag(ballTag))
            return;

        triggered = true;

        if (ballRoot == null)
            ballRoot = other.gameObject;

        if (goalRoot == null)
            goalRoot = gameObject;

        ballRoot.SetActive(false);
        goalRoot.SetActive(false);

        if (eventManager != null)
            eventManager.ShowFullscreenImage(fullscreenSprite, imageDuration);

        if (respawnAfterEvent)
            StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(imageDuration + respawnDelay);

        if (ballRoot != null)
            ballRoot.SetActive(true);

        if (goalRoot != null)
            goalRoot.SetActive(true);

        triggered = false;
    }
}