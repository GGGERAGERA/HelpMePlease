using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyWaveManager : MonoBehaviour
{
    private List<EnemyWaveSO> enemiesToSpawn;
    public List<EnemyWaveSO> allWaves;
    private int _currentWave = 0;

    private void Start()
    {
        StartCoroutine(SpawnWave());
    }

    private IEnumerator SpawnWave()
    {
        foreach (var enemyData in enemiesToSpawn)
        {
            /*EnemyBehavior enemy = EnemyPool.InstanceEnemyPoolParent.GetEnemy(enemyData.enemyType);
            if (enemy != null)
            {
                enemy.Initialize(enemyData, transform.position);
            }*/
            yield return new WaitForSeconds(0.5f); // Пауза между спавном
        }
    }
}