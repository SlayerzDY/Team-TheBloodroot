using System;
using Bloodroot.Features.BloodMoon;
using UnityEngine;

// Instuctions:
// 1. Create a empty 3D object
// 2. Create another empty and emplace that as a child under your object
// 3. Add the script to the first 3D object
// 4. Place your desired object/enemy into the Enemy slot
// 5. Add your spawn points to the array by using the drop down arrow
// 6. Adjust spawn rate to desired speed
// 7. Set max enemies
// 8. set spawn radius around spawn points
// 9. last current object is just to visually see how many are currently spawned/ also does not count down as of writing this comment

//

public class MobSpawner : MonoBehaviour, IDamage
{

    [SerializeField] GameObject Enemy;
    [SerializeField] Transform[] spawnPoint;
    [SerializeField]float spawnRate;
    [SerializeField] int maxEnemies;
    [SerializeField] float spawnRadius;

    // to view the amount of objects currently spawned
   [SerializeField] int currentEnemies = 0;

    float timer = 0f;
    int waveEnemyCount;
    int enemiesSpawnedThisWave;
    bool waveSpawning;

    public BloodMoonModifier ActiveModifier { get; private set; }
    public int WaveEnemyCount => waveEnemyCount;
    public int EnemiesSpawnedThisWave => enemiesSpawnedThisWave;

    public event Action<GameObject, BloodMoonModifier> EnemySpawned;
    public event Action EnemyDied;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!waveSpawning)
            return;

        timer += Time.deltaTime;

        if(timer >= spawnRate &&
           currentEnemies < maxEnemies &&
           enemiesSpawnedThisWave < waveEnemyCount)
        {

            SpawnObject();

            // reset timer
            timer = 0f;

        }
        
    }

    void SpawnObject()
    {

        int rando = Random.Range(0, spawnPoint.Length);
        Transform center = spawnPoint[rando];

        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 randomOffset = new Vector3(randomCircle.x, 0, randomCircle.y);
        Vector3 Spawn = center.position + randomOffset;

        GameObject spawnedEnemy = Instantiate(Enemy, Spawn, center.rotation);
        currentEnemies++;
        enemiesSpawnedThisWave++;

        EnemySpawned?.Invoke(spawnedEnemy, ActiveModifier);

        if (enemiesSpawnedThisWave >= waveEnemyCount)
            waveSpawning = false;

    }

    public void ConfigureWave(int enemyCount, BloodMoonModifier modifier)
    {
        waveEnemyCount = Mathf.Max(0, enemyCount);
        enemiesSpawnedThisWave = 0;
        currentEnemies = 0;
        ActiveModifier = modifier;
        timer = spawnRate;
        waveSpawning = waveEnemyCount > 0;
    }

    public void StopWave()
    {
        waveSpawning = false;
        ActiveModifier = null;
    }

    public void MobDied()
    {

        currentEnemies = Mathf.Max(0, currentEnemies - 1);
        EnemyDied?.Invoke();
        Debug.Log(currentEnemies);

    }

    public void TakeDamage(int amount)
    {
        throw new System.NotImplementedException();
    }

    public void onDeath(bool dead)
    {
        MobDied();
    }
}
