using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class AnomalySiteDebugSelector : MonoBehaviour
{
    private GravityAnomalySiteController gravitySite;
    private ElectricAnomalySiteController electricSite;
    private BeamAnomalySiteController beamSite;
    private NormalAnomalySiteController normalSite;
    private bool explorationLocked;

    public void Configure(
        GravityAnomalySiteController gravity,
        ElectricAnomalySiteController electric,
        BeamAnomalySiteController beam,
        NormalAnomalySiteController normal)
    {
        gravitySite = gravity;
        electricSite = electric;
        beamSite = beam;
        normalSite = normal;
        StopAll();
    }

    private void Update()
    {
        if (explorationLocked)
            return;

        if (Input.GetKeyDown(KeyCode.F6))
            HandleSiteKey(6);
        if (Input.GetKeyDown(KeyCode.F7))
            HandleSiteKey(7);
        if (Input.GetKeyDown(KeyCode.F9))
            HandleSiteKey(9);
        if (Input.GetKeyDown(KeyCode.F10))
            HandleSiteKey(10);
    }

    public void SetExplorationLocked(bool locked)
    {
        explorationLocked = locked;
        if (locked)
            StopAll();

        if (electricSite != null)
            electricSite.enabled = !locked;
        if (beamSite != null)
            beamSite.enabled = !locked;
    }

    private void HandleSiteKey(int site)
    {
        bool stop = Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (stop)
        {
            if (site == 6)
                gravitySite?.StopSite();
            else if (site == 7)
                electricSite?.StopSite();
            else if (site == 9)
                beamSite?.StopSite();
            else
                normalSite?.StopSite();
            return;
        }

        StopAll();

        if (site == 6)
            gravitySite?.StartOrResetSite();
        else if (site == 7)
            electricSite?.StartOrResetSite();
        else if (site == 9)
            beamSite?.StartOrResetSite();
        else
            normalSite?.StartOrResetSite();
    }

    private void StopAll()
    {
        gravitySite?.StopSite();
        electricSite?.StopSite();
        beamSite?.StopSite();
        normalSite?.StopSite();
    }
}
#endif
