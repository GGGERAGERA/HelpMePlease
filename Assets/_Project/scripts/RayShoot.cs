using UnityEngine;

public class WeaponShootFX : MonoBehaviour
{
    public ParticleSystem muzzleFlash;
    public ParticleSystem beamEffect;
    public Transform firePoint;
    private Camera cam;

    void Start() => cam = Camera.main;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RotateToMouse();
            PlayParticle(muzzleFlash);
            PlayParticle(beamEffect);
        }
    }

    void PlayParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();
    }

    void RotateToMouse()
    {
        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector2 dir = (mousePos - firePoint.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}