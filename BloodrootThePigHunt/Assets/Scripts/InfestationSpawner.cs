using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

// How To use:
// 1. Create a Empty Object
// 2. Create other empty object/objects as childs for spawn points
// 3. Add script to the original empty object
// 4. Add spawn points to the seralized array of points
// 5. Set radius wanted for how far they can spawn away from a spawn point
// 6. enter the amount of zone you wish to add when the script is ran
// TLDR: this script currently does not function as a standalone spawner without the event for when the curse activates
// also this will not work unless there is an active terrain in the hierarchy and should spawn anywhere on the terrain but no where past it
// Update1: currently will only spawn on terrain so if it spawns inside house will spawn below floor
// 


public class InfestationSpawner : MonoBehaviour
{
    // makes the struct serializable, basically just makes it so I can make it into a serlized field
    // this is going to be used to add chance to trap spawns
    [System.Serializable]
    public struct TrapConfig
    {

        public GameObject prefab;

        [Range(1, 100)] public int spawnWeight;

    }

    //[SerializeField] GameObject infestation;
    [SerializeField] private TrapConfig[] traps;

    [SerializeField] private float clearanceRadius = 2f;
    [SerializeField] private float WithinArea = 1.0f;

    // this basically cadds the component that tells it which layers to avoid like oposite of nav meshes asking which layers to bake
    // example avoid enemies,player, or other traps(needed for clearance radius to work) when placed
    // do not use for this like default which is most things or it will cause issues like spawning on top of the corn
    [SerializeField] private LayerMask avoidanceLayer;


    public Transform[] spawnPoints;
    public float spawnRadius;
    public int zoneAmount = 3;


    private List<GameObject> activeZones = new List<GameObject>();
    private waveManager waveManager;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // there is probably a better way to make this better like just changing trigger curse to a boolean but 
        // this works for now but would be better to change later for expanded use
        waveManager = FindAnyObjectByType<waveManager>();

        if (waveManager != null)
        {
            // just subcribes to event in wave manager
            waveManager.CurseStarted += TriggerCurseSpawning;
            
        }
        

    }

    void OnDestroy()
    {
        if (waveManager != null)
        {
            //unsubs from event
            waveManager.CurseStarted -= TriggerCurseSpawning;
        }
    }

    public void TriggerCurseSpawning()
    {

        SpawnAndClear(zoneAmount);

    }

    private GameObject GetWeight()
    {

        // basically just reused shawns code that he put on mob spawner that I changed to make certain enemies on certain waves
        //this one actually should hopefully grab a random weight and use that as a basis to spawn different traps
        int Weight = 0;

        for (int i = 0; i < traps.Length; i++)
        {

            Weight += traps[i].spawnWeight;

        }

        int RandomIndex = Random.Range(0, Weight);
        int currentWeight = 0;

        for (int i = 0; i < traps.Length; i++) {

            currentWeight += traps[i].spawnWeight;

            if(RandomIndex < currentWeight)
            {

                return traps[i].prefab;

            }

        }

        return traps[0].prefab;

    }
    public void SpawnAndClear(int Zones)
    {

        // just cleans up any leaftover zones if they are there
        for (int i = 0; i < activeZones.Count; i++)
        {

            if (activeZones[i] != null)
            {

                Destroy(activeZones[i]);

            }

        }
        
        activeZones.Clear();

        // gets the terrain in use
        Terrain CurrentTerrain = Terrain.activeTerrain;

        // check for valid spawn points if not then wont spawn
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < Zones; i++)
            {
                Vector3 spawnPos = Vector3.zero;

                bool spotFound = false;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    Transform center = spawnPoints[Random.Range(0, spawnPoints.Length)];

                    Vector2 offset = Random.insideUnitCircle * spawnRadius;

                    Vector3 candidate = center.position + new Vector3(offset.x, 0, offset.y);

                    // candidate.y = 0f;

                    if (CurrentTerrain)
                    {

                        candidate.y = CurrentTerrain.SampleHeight(candidate) + CurrentTerrain.transform.position.y;

                    }

                   // this is for playable area within the conditions of the navmesh
                   if(NavMesh.SamplePosition(candidate, out NavMeshHit navHit, WithinArea, NavMesh.AllAreas))
                    {

                        candidate = navHit.position;

                        if (!Physics.CheckSphere(candidate, clearanceRadius, avoidanceLayer))
                        {
                            spawnPos = candidate;

                            spotFound = true;

                            break;
                        }

                    }

                }
                if (spotFound)
                {

                    GameObject theWinner = GetWeight();

                    GameObject ILoathZone = Instantiate(theWinner, spawnPos, Quaternion.identity, transform);

                    activeZones.Add(ILoathZone);

                }
                // could cause issues if spawner is not set corectly. Ex: can spawn things outside of nav mesh
                else
                {
                    GameObject theWinner = GetWeight();

                    Transform center2 = spawnPoints[Random.Range(0, spawnPoints.Length)];

                    GameObject IhateZones = Instantiate(theWinner, center2.position, Quaternion.identity, transform);

                    activeZones.Add(IhateZones);
                }
            }
        }
    }
}