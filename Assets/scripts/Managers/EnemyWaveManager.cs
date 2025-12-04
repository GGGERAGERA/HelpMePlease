using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyWaveManager : MonoBehaviour
{
    public List<EnemySO> enemiesToSpawn; // Список врагов для волны

    private void Start()
    {
        StartCoroutine(SpawnWave());
    }

    private IEnumerator SpawnWave()
    {
        foreach (var enemyData in enemiesToSpawn)
        {
            EnemyBehavior enemy = EnemyPool.InstanceEnemyPoolParent.GetEnemy(enemyData.enemyType);
            if (enemy != null)
            {
                enemy.Initialize(enemyData, transform.position);
            }
            yield return new WaitForSeconds(0.5f); // Пауза между спавном
        }
    }
}