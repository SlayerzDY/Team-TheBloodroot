using UnityEngine;
using Bloodroot.Features.BloodMoon;



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

public class MobSpawner : MonoBehaviour
{

    [SerializeField] GameObject Enemy;
    [SerializeField] GameObject[] enemies;
    [SerializeField] GameObject regularPig;
    [SerializeField, Min(0)] int regularPigsOnScreen = 4;
    [SerializeField] Transform[] spawnPoint;
    [SerializeField]float spawnRate;
    [SerializeField] float spawnRadius;


    public int maxEnemies;
    public int currentEnemies;
    public int currentRegularPigs;

    float timer = 0f;

    public bool isWaveActive;
    private int enemiesInWave;
    private int enemiesSpawnedThisWave;

    private waveManager manager;
    private BloodMoonModifier activeModifier;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        manager = FindAnyObjectByType<waveManager>();
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        if (manager != null && manager.ShouldSpawnRegularPigs)
        {
            if (currentRegularPigs < regularPigsOnScreen)
            {
                SpawnRegularPig();
            }

            return;
        }

        float currentSpawnRate = spawnRate;

        if(manager != null)
        {
            currentSpawnRate = Mathf.Max(0.3f, spawnRate - (manager.currentWave * 0.05f));
        }
        if(timer >= currentSpawnRate && isWaveActive && currentEnemies < maxEnemies)
        {
            SpawnObject();

            timer = 0f;
        }
    }

    public void MobDied()
    {
        currentEnemies--;
        if (currentEnemies < 0)
        {
            currentEnemies = 0;
        }
    }

    public void RegularPigDied()
    {
        currentRegularPigs--;
        if (currentRegularPigs < 0)
        {
            currentRegularPigs = 0;
        }
    }

    void SpawnRegularPig()
    {
        if (regularPig == null)
        {
            Debug.LogError("MobSpawner cannot spawn regular pigs because no regular pig prefab is assigned.");
            return;
        }

        if (!TryGetSpawnPoint(out Vector3 Spawn, out Quaternion Rotation))
            return;

        GameObject spawnedPig =
            Instantiate(regularPig, Spawn, Rotation);

        RegularHog hog =
            spawnedPig.GetComponent<RegularHog>();

        if (hog != null)
        {
            hog.SetSpawner(this);
            hog.SetManager(manager);
        }

        currentRegularPigs++;
    }

    void SpawnObject()
    {
        GameObject enemyToSpawn = GetEnemyToSpawn();

        if (enemyToSpawn == null)
        {
            Debug.LogError("MobSpawner cannot spawn because no enemy prefab is assigned.");
            isWaveActive = false;
            return;
        }

        if (!TryGetSpawnPoint(out Vector3 Spawn, out Quaternion Rotation))
        {
            return;
        }

        GameObject spawnedEnemy =
            Instantiate(enemyToSpawn, Spawn, Rotation);

        enemyAI enemy = spawnedEnemy.GetComponent<enemyAI>();

        if(enemy != null)
        {
            enemy.InitializeEnemy(manager.currentWave);
        }

        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        if (manager != null)
        {
            manager.EnemySpawned(spawnedEnemy);
        }

        currentEnemies++;

        if(currentEnemies >= maxEnemies)
        {

            isWaveActive = false;

        }

    }

    GameObject GetEnemyToSpawn()
    {
        if (enemies != null && enemies.Length > 0)
        {
            int allowedMobs = Mathf.Min(manager.currentWave, enemies.Length);

            int startIndex = Random.Range(0, allowedMobs);

            if (enemies[startIndex] != null)
            {

                return enemies[startIndex];

            }
          
        }

        return Enemy;
    }

    bool TryGetSpawnPoint(out Vector3 Spawn, out Quaternion Rotation)
    {
        Spawn = transform.position;
        Rotation = transform.rotation;

        if (spawnPoint == null || spawnPoint.Length == 0)
        {
            Debug.LogError("MobSpawner cannot spawn because it has no spawn points.");
            isWaveActive = false;
            return false;
        }

        int rando = Random.Range(0, spawnPoint.Length);
        Transform center = spawnPoint[rando];

        if (center == null)
        {
            Debug.LogError("MobSpawner contains an empty spawn point reference.");
            isWaveActive = false;
            return false;
        }

        Vector2 randomCircle =
            Random.insideUnitCircle * spawnRadius;

        Vector3 randomOffset =
            new Vector3(randomCircle.x, 0, randomCircle.y);

        Spawn = center.position + randomOffset;
        Rotation = center.rotation;
        return true;
    }

  public void StartWave(int totalEnemies)
    {
        enemiesInWave = totalEnemies;
        enemiesSpawnedThisWave = 0;
        currentEnemies = 0;
        maxEnemies = Mathf.Min(10 + manager.currentWave, totalEnemies);

        isWaveActive = true;
    }
}
