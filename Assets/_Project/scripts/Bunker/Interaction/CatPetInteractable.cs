using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(CatWanderController))]
public sealed class CatPetInteractable : Interactable
{
    [SerializeField] private CatWanderController wander;
    [SerializeField] private CatHeartFx heartPrefab;
    [SerializeField] private Transform heartAnchor;
    [SerializeField, Min(.1f)] private float petCooldown = 1.8f;
    [SerializeField, Min(.1f)] private float interactionPause = 1.5f;
    private float nextPetTime;

    public override bool CanInteract => isActiveAndEnabled && wander != null &&
        wander.State != CatWanderController.CatState.Interacting && Time.time >= nextPetTime &&
        Time.timeScale > 0f && !SceneTransitionOverlay.IsTransitioning &&
        (BunkerContext.Instance == null || BunkerContext.Instance.Panels == null || !BunkerContext.Instance.Panels.IsAnyPanelOpen);

    private void Awake()
    {
        if (wander == null) wander = GetComponent<CatWanderController>();
    }

    public override void Interact()
    {
        if (!CanInteract) return;
        nextPetTime = Time.time + petCooldown;
        wander.BeginPetting(Mathf.Max(interactionPause, heartPrefab != null ? heartPrefab.Duration : 0f));
        if (heartPrefab == null || heartAnchor == null) return;
        CatHeartFx heart = Instantiate(heartPrefab, heartAnchor.position, Quaternion.identity);
        SortingGroup sorting = GetComponent<SortingGroup>();
        if (sorting != null) heart.SetSorting(sorting.sortingLayerID, sorting.sortingOrder + 5);
    }
}
