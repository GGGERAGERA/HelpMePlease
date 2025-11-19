using UnityEngine;

public class KnifeBehaviour : ProjectileWeaponBehaviour
{
    KnifeController kc;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
   protected override void Start()
    {
        base.Start();
        kc = FindAnyObjectByType<KnifeController>();
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += direction * kc.speed * Time.deltaTime; // установка скорости для ножа
    }
}
