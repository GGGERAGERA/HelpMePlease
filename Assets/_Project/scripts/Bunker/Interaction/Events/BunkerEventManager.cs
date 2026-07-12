using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerEventManager : MonoBehaviour
{
    [Header("Fullscreen Image")]
    [SerializeField] private GameObject fullscreenRoot;
    [SerializeField] private Image fullscreenImage;

    private Coroutine showImageRoutine;

    private void Awake()
    {
        HideFullscreenImage();
    }

    public void ShowFullscreenImage(Sprite sprite, float seconds)
    {
        if (sprite == null || fullscreenRoot == null || fullscreenImage == null)
            return;

        if (showImageRoutine != null)
            StopCoroutine(showImageRoutine);

        showImageRoutine = StartCoroutine(ShowImageRoutine(sprite, seconds));
    }

    private IEnumerator ShowImageRoutine(Sprite sprite, float seconds)
    {
        fullscreenImage.sprite = sprite;
        fullscreenImage.enabled = true;
        fullscreenRoot.SetActive(true);

        yield return new WaitForSeconds(seconds);

        HideFullscreenImage();
        showImageRoutine = null;
    }

    private void HideFullscreenImage()
    {
        if (fullscreenImage != null)
        {
            fullscreenImage.sprite = null;
            fullscreenImage.enabled = false;
        }

        if (fullscreenRoot != null)
            fullscreenRoot.SetActive(false);
    }
}