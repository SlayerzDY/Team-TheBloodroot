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
    [SerializeField] float spawnRadius;


    public int maxEnemies;
    public int currentEnemies = 0;

    float timer = 0f;

    public bool isWaveActive = false;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if(timer >= spawnRate && isWaveActive && currentEnemies < maxEnemies)
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

        Instantiate(Enemy, Spawn, center.rotation);
        currentEnemies++;

        if(currentEnemies > maxEnemies)
        {

            isWaveActive = false;

        }

    }

    public void MobDied()
    {

        currentEnemies--;
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
