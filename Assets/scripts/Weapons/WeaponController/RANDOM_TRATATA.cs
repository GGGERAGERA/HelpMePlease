using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;

public class RANDOM_TRATATA : WeaponController
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();
    }

    // Update is called once per frame
    protected override void Attack()
    {
        base.Attack();
        GameObject spawnedKnife = Instantiate(weaponData.Prefab);
        Vector2 randomStyle = new Vector2(Random.Range(-10, 10), Random.Range(-10, 10));
        spawnedKnife.transform.position = transform.position;
        spawnedKnife.GetComponent<KnifeBehaviour>().DirectionChecker(randomStyle); // Reference and set direction
    }
}
