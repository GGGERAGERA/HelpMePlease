using UnityEngine;

public sealed class LaserAudioController : MonoBehaviour
{
    private AudioLoopHandle beamLoop;
    private AudioLoopHandle impactLoop;
    private bool isFiring;
    private bool isImpacting;

    public void SetFiring(bool firing)
    {
        if (firing == isFiring)
            return;

        if (firing)
            BeginFiring();
        else
            EndFiring(true);
    }

    public void SetImpacting(bool impacting)
    {
        impacting &= isFiring;

        if (impacting == isImpacting)
            return;

        isImpacting = impacting;

        if (impacting)
        {
            impactLoop = AudioService.Instance?.StartLoop(
                AudioCueId.LaserImpactLoop,
                transform
            );
        }
        else
        {
            StopHandle(ref impactLoop);
        }
    }

    private void BeginFiring()
    {
        isFiring = true;
        isImpacting = false;

        AudioService.Instance?.PlayAt(AudioCueId.LaserStart, transform.position);
        beamLoop = AudioService.Instance?.StartLoop(AudioCueId.LaserLoop, transform);
    }

    private void EndFiring(bool playEnd)
    {
        bool wasFiring = isFiring;
        isFiring = false;
        isImpacting = false;

        StopHandle(ref impactLoop);
        StopHandle(ref beamLoop);

        if (playEnd && wasFiring)
            AudioService.Instance?.PlayAt(AudioCueId.LaserEnd, transform.position);
    }

    private void OnDisable()
    {
        EndFiring(false);
    }

    private void OnDestroy()
    {
        EndFiring(false);
    }

    private static void StopHandle(ref AudioLoopHandle handle)
    {
        handle?.Stop();
        handle = null;
    }
}
