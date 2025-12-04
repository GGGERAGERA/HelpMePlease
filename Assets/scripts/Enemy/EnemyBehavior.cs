
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
public class EnemyBehavior : MonoBehaviour
{
    public EnemySO EnemySO1;
    private int EnemyCurrentHealth;

    public void Initialize(EnemySO data, Vector2 spawnPosition)
    {
        EnemySO1 = data;
        EnemyCurrentHealth = data.EnemyMaxHealth;
        transform.position = spawnPosition;
        gameObject.SetActive(true);
    }

    public void TakeDamage(int damage)
    {
        EnemyCurrentHealth -= damage;
        if (EnemyCurrentHealth <= 0)
            Die();
    }

    private void Die()
    {
        gameObject.SetActive(false);
        EnemyPool.InstanceEnemyPoolParent.ReturnEnemy(this);
    }
}