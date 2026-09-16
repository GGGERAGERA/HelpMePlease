using System;
using UnityEngine;

/// <summary>Scene-owned presentation only. Combat isolation is authored before Play.</summary>
public sealed class EnemyGalleryController : MonoBehaviour
{
    [Serializable]
    public sealed class Exhibit
    {
        public GameObject instance;
        public Animator animator;
        public string animationState;
        public GameObject label;
        [Tooltip("Only for production articulated visuals without an active Animator.")]
        public Transform motionPivot;
        [NonSerialized] public Quaternion restRotation;
    }

    [SerializeField] private Exhibit[] exhibits = Array.Empty<Exhibit>();
    [SerializeField] private CharacterMovement2D player;
    [SerializeField] private GameObject[] candidateLabels = Array.Empty<GameObject>();
    public Exhibit[] Exhibits => exhibits;
    public CharacterMovement2D Player => player;
    public bool NamesVisible { get; private set; } = true;

    private void Start()
    {
        foreach (var exhibit in exhibits)
        {
            // Original controller, sprites, hierarchy and scale. No root motion or events.
            // The production turret has a static articulated visual, no active Animator.
            if (exhibit.animator != null) exhibit.animator.Play(exhibit.animationState, 0, 0f);
            if (exhibit.motionPivot != null) exhibit.restRotation = exhibit.motionPivot.localRotation;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) SetNamesVisible(!NamesVisible);
        foreach (var exhibit in exhibits)
            if (exhibit.motionPivot != null)
                exhibit.motionPivot.localRotation = exhibit.restRotation * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * .6f) * 25f);
    }

    public void SetNamesVisible(bool visible)
    {
        NamesVisible = visible;
        foreach (var exhibit in exhibits) exhibit.label.SetActive(visible);
        foreach (var label in candidateLabels) label.SetActive(visible);
        foreach (var label in candidateLabels) label.SetActive(visible);
    }
}
