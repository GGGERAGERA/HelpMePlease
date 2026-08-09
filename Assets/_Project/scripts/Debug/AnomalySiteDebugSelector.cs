using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class AnomalySiteDebugSelector : MonoBehaviour
{
    private GravityAnomalySiteController gravitySite;
    private ElectricAnomalySiteController electricSite;
    private BeamAnomalySiteController beamSite;

    public void Configure(
        GravityAnomalySiteController gravity,
        ElectricAnomalySiteController electric,
        BeamAnomalySiteController beam)
    {
        gravitySite = gravity;
        electricSite = electric;
        beamSite = beam;
        StopAll();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
            HandleSiteKey(6);
        if (Input.GetKeyDown(KeyCode.F7))
            HandleSiteKey(7);
        if (Input.GetKeyDown(KeyCode.F9))
            HandleSiteKey(9);
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
            else
                beamSite?.StopSite();
            return;
        }

        StopAll();

        if (site == 6)
            gravitySite?.StartOrResetSite();
        else if (site == 7)
            electricSite?.StartOrResetSite();
        else
            beamSite?.StartOrResetSite();
    }

    private void StopAll()
    {
        gravitySite?.StopSite();
        electricSite?.StopSite();
        beamSite?.StopSite();
    }
}
#endif
