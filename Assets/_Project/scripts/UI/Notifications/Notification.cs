using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Notification : MonoBehaviour
{
    public Text notificationText;

    public void Show(string message, float duration = 2f)
    {
        StartCoroutine(ShowCoroutine(message, duration));
    }

    IEnumerator ShowCoroutine(string message, float duration)
    {
        notificationText.text = message;
        notificationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        notificationText.gameObject.SetActive(false);
    }
}
