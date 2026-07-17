using UnityEngine;

public sealed class LaserAudioController : MonoBehaviour
{
    private bool isFiring;

    public void SetFiring(bool firing)
    {
        if (firing == isFiring)
            return;

        isFiring = firing;

        if (isFiring)
            AudioService.Instance?.PlayAt(AudioCueId.LaserShot, transform.position);
    }

    public void SetImpacting(bool impacting)
    {
    }

    private void OnDisable()
    {
        isFiring = false;
    }
}
