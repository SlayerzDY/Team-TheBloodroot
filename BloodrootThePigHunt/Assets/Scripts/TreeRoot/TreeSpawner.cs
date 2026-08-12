using System.Collections;
using UnityEngine;

public class TreeSpawner : MonoBehaviour
{
    public GameObject[] enemies;
    public Transform[] spawnPoint;
    [Range(0.1f, 10f)] public float spawnRate = 1f;

    // could be used for UI elements later
    //private bool isSpawning = false;

    public void StartBaseDefense(int enemycount)
    {

        if(enemies == null || enemies.Length == 0) { return; }

        StartCoroutine(SpawnEnemies(enemycount));

    }

    private IEnumerator SpawnEnemies(int totalEnemies)
    {

       // isSpawning = true;
        gameManager.instance.isDefenseActive = true;

        for (int i = 0; i < totalEnemies; i++)
        {

            Transform SelectedPoints = spawnPoint[Random.Range(0, spawnPoint.Length)];

            GameObject SSelectedEnemyPrefab = enemies[Random.Range(0, enemies.Length)];
            GameObject newEnemy = Instantiate(SSelectedEnemyPrefab, SelectedPoints.position, SelectedPoints.rotation);

            gameManager.instance.StartCheckWave();
            yield return new WaitForSeconds(spawnRate);


        }

        //isSpawning = false;

    }        
}
