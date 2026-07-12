using UnityEngine;

public sealed class RunMessageService : MonoBehaviour
{
    public static RunMessageService Instance { get; private set; }

    [SerializeField] private RunMessageView view;
    [SerializeField] private RunMessageData[] messages;

    private void Awake()
    {
        Instance = this;
    }

    public void Show(RunMessageType type)
    {
        RunMessageData data = FindMessage(type);

        if (data == null)
        {
            Debug.LogWarning($"[RunMessageService] Message not found: {type}");
            return;
        }

        Show(data);
    }

    public void ShowCustom(string title, string description, float duration = 3f)
    {
        if (view == null)
            return;

        view.Show(title, description, duration);
    }

    private void Show(RunMessageData data)
    {
        if (view == null || data == null)
            return;

        view.Show(data.title, data.description, data.duration);

        if (data.sound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(
                data.sound,
                Camera.main.transform.position,
                data.volume
            );
        }
    }

    private RunMessageData FindMessage(RunMessageType type)
    {
        if (messages == null)
            return null;

        foreach (RunMessageData message in messages)
        {
            if (message != null && message.messageType == type)
                return message;
        }

        return null;
    }
}