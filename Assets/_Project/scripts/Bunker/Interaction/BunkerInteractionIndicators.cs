using System.Collections.Generic;
using UnityEngine;

/// <summary>One visual adapter for the scene's existing interaction contracts.</summary>
[DisallowMultipleComponent]
public sealed class BunkerInteractionIndicators : MonoBehaviour
{
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private BunkerPanelManager panelManager;
    [SerializeField, Min(1f)] private float interactionDistance = 5.5f;
    private readonly Dictionary<MonoBehaviour, BunkerInteractionArrow> arrows = new();
    public static BunkerInteractionIndicators Instance { get; private set; }
    public float InteractionDistance => interactionDistance;
    public bool IsBlocked => !isActiveAndEnabled || Time.timeScale <= 0f ||
        SceneTransitionOverlay.IsTransitioning || (panelManager != null && panelManager.IsAnyPanelOpen);

    private void Awake()
    {
        Instance = this;
        AdoptSceneArrows();
    }

    private void Start()
    {
        RefreshBindings();
    }

    private void AdoptSceneArrows()
    {
        arrows.Clear();
        foreach(var root in gameObject.scene.GetRootGameObjects())
        foreach(var arrow in root.GetComponentsInChildren<BunkerInteractionArrow>(true))
        {
            if(arrow.Source == null) continue;
            arrow.Bind(this,arrow.Source);
            arrows[arrow.Source] = arrow;
        }
    }

    public void RefreshBindings()
    {
        AdoptSceneArrows();
        // One scene-local discovery pass, including authored stations not available yet.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                Register(behaviour);
    }

    public bool TryGetArrow(MonoBehaviour source, out BunkerInteractionArrow arrow)
    {
        arrow = null;
        return source != null && arrows.TryGetValue(source,out arrow) && arrow != null;
    }

    public void Register(MonoBehaviour source)
    {
        if (source == null || source.gameObject.scene != gameObject.scene ||
            source is not IBunkerInteractable || source is CatPetInteractable ||
            source is FootballStartStation || arrowPrefab == null || arrows.ContainsKey(source)) return;
        // Reuse authored original instances (including nested mission/terminal
        // prefabs). Only missing stations instantiate that same original asset.
        GameObject original = null;
        foreach(var animator in source.GetComponentsInChildren<Animator>(true))
            if(animator.runtimeAnimatorController != null &&
                animator.runtimeAnimatorController.name == "PF_InteractionArrow")
            { original = animator.gameObject; break; }
        if(original == null)
        {
#if UNITY_EDITOR
        if(!Application.IsPlaying(gameObject))
            original = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(arrowPrefab,source.transform);
        else
#endif
            original = Instantiate(arrowPrefab, source.transform);
            float top = source.transform.position.y;
            foreach(var sprite in source.GetComponentsInChildren<SpriteRenderer>(true))
                if(sprite.sprite != null && !sprite.transform.IsChildOf(original.transform) &&
                    sprite.name.IndexOf("shadow",System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    Mathf.Abs(sprite.bounds.center.x-source.transform.position.x) <= 4f &&
                    sprite.bounds.max.y <= source.transform.position.y+6f)
                    top = Mathf.Max(top,sprite.bounds.max.y);
            if(original.transform.position.y < top+.4f)
                original.transform.position = new Vector3(original.transform.position.x,top+.4f,original.transform.position.z);
        }
        var arrow = original.GetComponent<BunkerInteractionArrow>() ?? original.AddComponent<BunkerInteractionArrow>();
        arrow.Bind(this, source);
        arrows.Add(source, arrow);
        arrow.gameObject.SetActive(true);
#if UNITY_EDITOR
        if(!Application.IsPlaying(gameObject))
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(arrow);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.GetComponent<BunkerInteractableCollider>());
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
