using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BunkerInteractableCollider : MonoBehaviour
{
    [SerializeField] private GameObject sourceRoot;

    public IBunkerInteractable Interactable { get; private set; }
    public IBunkerHoverable Hoverable { get; private set; }

    private void Awake()
    {
        if (sourceRoot == null)
            sourceRoot = transform.root.gameObject;

        ConfigureSource(sourceRoot);

        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[BunkerInteractableCollider] {name} " +
            $"source={sourceRoot.name}, " +
            $"interactable={Interactable != null}, " +
            $"hoverable={Hoverable != null}"
        );
#endif
    }

    public void ConfigureSource(GameObject source)
    {
        sourceRoot = source;
        Interactable = sourceRoot != null ? sourceRoot.GetComponent<IBunkerInteractable>() : null;
        Hoverable = GetComponent<IBunkerHoverable>() ?? sourceRoot?.GetComponent<IBunkerHoverable>();
    }

    private void Start()
    {
        if (Interactable is MonoBehaviour source)
            BunkerInteractionIndicators.Instance?.Register(source);
    }
}
