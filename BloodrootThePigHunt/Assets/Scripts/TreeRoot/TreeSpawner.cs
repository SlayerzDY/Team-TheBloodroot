using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class TreeSpawner : MonoBehaviour
{
    public GameObject[] enemies;
    public Transform[] spawnPoint;
    [Range(0.1f, 10f)] public float spawnRate = 1f;
    [SerializeField] private BoxCollider spawnContainment;

    public BoxCollider SpawnContainment => spawnContainment;

    // could be used for UI elements later
    //private bool isSpawning = false;

    public void StartBaseDefense(int enemycount)
    {

        if(enemies == null || enemies.Length == 0 ||
           spawnPoint == null || spawnPoint.Length == 0) { return; }

        StartCoroutine(SpawnEnemies(enemycount));

    }

    private IEnumerator SpawnEnemies(int totalEnemies)
    {

       // isSpawning = true;
        gameManager.instance.isDefenseActive = true;

        for (int i = 0; i < totalEnemies; i++)
        {

            Transform SelectedPoints = spawnPoint[Random.Range(0, spawnPoint.Length)];

            if (SelectedPoints == null ||
                !NavMesh.SamplePosition(
                    SelectedPoints.position,
                    out NavMeshHit groundedSpawn,
                    3f,
                    NavMesh.AllAreas) ||
                !IsInsideSpawnContainment(groundedSpawn.position))
            {
                continue;
            }

            GameObject SSelectedEnemyPrefab = enemies[Random.Range(0, enemies.Length)];
            if (SSelectedEnemyPrefab == null)
            {
                continue;
            }

            GameObject newEnemy = Instantiate(
                SSelectedEnemyPrefab,
                groundedSpawn.position,
                SelectedPoints.rotation);

            gameManager.instance.StartCheckWave();
            yield return new WaitForSeconds(spawnRate);


        }

        //isSpawning = false;

    }        

    public void ConfigureSpawnContainment(BoxCollider containment)
    {
        spawnContainment = containment;
    }

    private bool IsInsideSpawnContainment(Vector3 worldPosition)
    {
        if (spawnContainment == null)
            return true;

        Vector3 local = spawnContainment.transform
            .InverseTransformPoint(worldPosition) - spawnContainment.center;
        Vector3 halfSize = spawnContainment.size * 0.5f;
        return Mathf.Abs(local.x) <= halfSize.x &&
               Mathf.Abs(local.z) <= halfSize.z;
    }
}
