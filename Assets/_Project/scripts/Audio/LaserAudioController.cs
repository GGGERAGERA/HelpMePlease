using UnityEngine;

public sealed class LaserAudioController : MonoBehaviour
{
    public void PlayShot(Vector3 position)
    {
        AudioService.Instance?.PlayAt(AudioCueId.LaserShot, position);
    }
}
