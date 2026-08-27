using Bloodroot.Features.BloodMoon;
using UnityEngine;
using UnityEngine.AI;



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
    [SerializeField] BoxCollider spawnContainment;


    public int maxEnemies;
    public int currentEnemies;
    public int currentRegularPigs;

    public BoxCollider SpawnContainment => spawnContainment;

    float timer = 0f;

    public bool isWaveActive;
    private int enemiesInWave;
    //private int enemiesSpawnedThisWave;

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

            return;
        }

        if (!TryGetSpawnPoint(out Vector3 Spawn, out Quaternion Rotation) ||
            !TryResolveContainedSpawn(Spawn, out Vector3 groundedSpawn))
            return;

        GameObject spawnedPig =
            Instantiate(regularPig, groundedSpawn, Rotation);

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

            isWaveActive = false;
            return;
        }

        if (!TryGetSpawnPoint(out Vector3 Spawn, out Quaternion Rotation) ||
            !TryResolveContainedSpawn(Spawn, out Vector3 groundedSpawn))
        {
            return;
        }

        GameObject spawnedEnemy =
            Instantiate(enemyToSpawn, groundedSpawn, Rotation);

        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        enemyAI enemy = spawnedEnemy.GetComponent<enemyAI>();

        if(enemy != null && manager != null)
        {
            enemy.InitializeEnemy(manager.currentWave);
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

    // DO NOT CHANGE THIS SHAWN
    // THIS MAKES IT SO ONLY CERTAIN MOBS SPAWN PER WAVE
    // also do not delete comments as they can be helpful for future people and give me a way to communicate without 
    // directly having to ping you 1000 times for an answer
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

            isWaveActive = false;
            return false;
        }

        int rando = Random.Range(0, spawnPoint.Length);
        Transform center = spawnPoint[rando];

        if (center == null)
        {

            isWaveActive = false;
            return false;
        }

        BoxCollider box = center.GetComponent<BoxCollider>();

        if (box != null)
        {
            Bounds bounds = box.bounds;

            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);

            Spawn = new Vector3(x, center.position.y, z);
        }
        else
        {
            Spawn = center.position;
        }

        //Vector2 randomCircle =
        //    Random.insideUnitCircle * spawnRadius;

        //Vector3 randomOffset =
        //    new Vector3(randomCircle.x, 0, randomCircle.y);

        //Spawn = center.position + randomOffset;
        Rotation = center.rotation;
        return true;
    }

    public void ConfigureSpawnContainment(BoxCollider containment)
    {
        spawnContainment = containment;
    }

    private bool TryResolveContainedSpawn(
        Vector3 requestedPosition,
        out Vector3 resolvedPosition)
    {
        resolvedPosition = requestedPosition;
        if (!NavMesh.SamplePosition(
                requestedPosition,
                out NavMeshHit hit,
                Mathf.Max(0.1f, spawnRadius),
                1) ||
            !IsInsideSpawnContainment(hit.position))
        {
            return false;
        }

        resolvedPosition = hit.position;
        return true;
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

  public void StartWave(int totalEnemies)
    {
        enemiesInWave = totalEnemies;
       // enemiesSpawnedThisWave = 0;
        currentEnemies = 0;
        maxEnemies = Mathf.Max(0, totalEnemies);

        isWaveActive = maxEnemies > 0;
        timer = 0f;
    }
}
