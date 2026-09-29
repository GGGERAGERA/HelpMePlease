using System;
using UnityEngine;

namespace Subject42.Bunker.Gallery
{
    /// <summary>Authored on the physical target; all display references belong to this room.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class EnemyGalleryController : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string name;
            public GameObject enemyPrefab;
            public EnemyGalleryPreviewController.DemoKind demo;
            [Header("Presentation assets (existing production VFX, no gameplay scripts)")]
            public GameObject warningPrefab;
            public GameObject explosionPrefab;
            public GameObject projectilePrefab;
            public GameObject shockwavePrefab;
            [TextArea(3, 6)] public string description;
            [TextArea(3, 6)] public string funFact;
        }

        [SerializeField] private Entry[] enemies = Array.Empty<Entry>();
        [SerializeField] private EnemyGalleryPreviewController preview;
        [SerializeField] private TextMesh enemyName;
        [SerializeField] private TextMesh description;
        [SerializeField] private TextMesh funFact;
        [SerializeField] private TextMesh page;
        [SerializeField] private SpriteRenderer buttonFace;
        [SerializeField] private LayerMask ballLayers;
        [SerializeField, Min(0.01f)] private float feedbackDuration = 0.18f;
        [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.35f);

        private int currentIndex;
        private float feedbackRemaining;
        private Color restingColor;

        public int CurrentIndex => currentIndex;

        private void Awake() => restingColor = buttonFace.color;

        private void OnEnable()
        {
            currentIndex = 0;
            feedbackRemaining = 0f;
            buttonFace.color = restingColor;
            ShowCurrent();
        }

        private void OnDisable()
        {
            feedbackRemaining = 0f;
            buttonFace.color = restingColor;
            preview.Clear();
        }

        private void Update()
        {
            if (feedbackRemaining <= 0f) return;
            feedbackRemaining -= Time.deltaTime;
            if (feedbackRemaining <= 0f) buttonFace.color = restingColor;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Only the ball's solid collider counts, never its kick-range trigger or the player.
            if ((ballLayers.value & (1 << collision.gameObject.layer)) == 0 ||
                !collision.rigidbody || !collision.rigidbody.TryGetComponent<BallRollVisual>(out _)) return;
            // A brief lockout also coalesces multiple ball contacts in the same physics step.
            if (feedbackRemaining > 0f || enemies.Length == 0) return;
            currentIndex = (currentIndex + 1) % enemies.Length;
            ShowCurrent();
            buttonFace.color = hitColor;
            feedbackRemaining = feedbackDuration;
        }

        private void ShowCurrent()
        {
            // An intentionally empty Inspector list has a defined display state.
            bool hasEntry = enemies.Length > 0;
            if (hasEntry) preview.Show(enemies[currentIndex]);
            else preview.Clear();
            enemyName.text = hasEntry ? enemies[currentIndex].name : "НЕТ ЗАПИСЕЙ";
            description.text = hasEntry ? enemies[currentIndex].description : "";
            funFact.text = hasEntry ? enemies[currentIndex].funFact : "";
            page.text = hasEntry ? $"{currentIndex + 1:00} / {enemies.Length:00}" : "00 / 00";

        }
    }
}
