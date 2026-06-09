using UnityEngine;

public class ExitPortal : MonoBehaviour
{
    public enum PortalAction
    {
        VictoryResult,
        NextLevel
    }

    [SerializeField] private PortalAction action;
    [SerializeField] private float activationDelay = 0.5f;

    private bool canActivate;
    private bool used;

    private void Start()
    {
        Invoke(nameof(EnablePortal), activationDelay);
    }

    private void EnablePortal()
    {
        canActivate = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Portal trigger entered by: " + other.name);
        if (!canActivate || used)
            return;

        if (!other.CompareTag("Player"))
            return;

        used = true;

        switch (action)
        {
            case PortalAction.VictoryResult:
                VictoryManager.Instance?.Victory();
                break;

            case PortalAction.NextLevel:
                RunLevelManager.Instance?.GoToNextLevel();
                Destroy(gameObject);
                break;
        }
    }
}