using System.Collections;
using TMPro;
using UnityEngine;

public sealed class RunMessageView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private Coroutine routine;

    private void Awake()
    {
        HideInstant();
    }

    public void Show(string title, string description, float duration = 3f)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ShowRoutine(title, description, duration));
    }

    private IEnumerator ShowRoutine(string title, string description, float duration)
    {
        titleText.text = title;
        descriptionText.text = description;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        yield return new WaitForSecondsRealtime(duration);

        HideInstant();
    }

    public void HideInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}