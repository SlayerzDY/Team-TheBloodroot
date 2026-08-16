//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.AI;
//==============================================================================================
// Declare Spawner
//==============================================================================================
public class spawn : MonoBehaviour
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] GameObject objectToSpawn;
    [SerializeField] int amountToSpawn;
    [SerializeField] int spawnRate;
    [SerializeField] int spawnDistance;
    private int spawnCount;
    private float spawnTimer;
    private bool startSpawning;
    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Function, Start
    //------------------------------------------------------------------------------------------
    void Start()
    {
        //gameManager.instance.updateGameGoal(amountToSpawn);
    }
    //==========================================================================================
    // Function, Update
    //------------------------------------------------------------------------------------------
    void Update()
    {
        if (startSpawning)
        {
            spawnTimer = Time.deltaTime;
            if (spawnCount < amountToSpawn && spawnTimer > spawnRate)
            {
                spawner();
            }
        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //------------------------------------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            startSpawning = true;
        }
    }
    //==========================================================================================
    // Function, Spawn
    //------------------------------------------------------------------------------------------
    void spawner()
    {
        spawnTimer = 0;
        spawnCount++;
        Vector3 ranPos = Random.insideUnitSphere * spawnDistance;
        ranPos += transform.position;
        NavMeshHit hit;
        NavMesh.SamplePosition(ranPos, out hit, spawnDistance, 1);
        Instantiate(objectToSpawn, hit.position, Quaternion.Euler(0, Random.Range(0, 360), 0));
    }

    //==========================================================================================
}
//==============================================================================================
// End of Spawner CS
//==============================================================================================