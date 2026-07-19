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

        Interactable = sourceRoot.GetComponent<IBunkerInteractable>();
        Hoverable = sourceRoot.GetComponent<IBunkerHoverable>();

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
}
