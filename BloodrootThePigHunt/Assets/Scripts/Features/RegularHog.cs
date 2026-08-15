using UnityEngine;
using UnityEngine.AI;

public class RegularHog : MonoBehaviour, IDamage
{
    [SerializeField, Min(1)] int HP = 2;
    [SerializeField, Min(0f)] float moveSpeed = 3f;
    [SerializeField, Min(1f)] float wanderRadius = 10f;
    [SerializeField, Min(0.1f)] float directionChangeTime = 2f;
    [SerializeField, Min(0.1f)] float destinationReachDistance = 1f;
    [SerializeField, Min(0f)] float fleeDistance = 12f;
    [SerializeField, Range(0f, 2f)] float fleeWeight = 0.75f;

    [Header("Sounds")]
    [SerializeField] AudioClip[] movingSounds;
    [SerializeField] AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] float soundVolume = 0.25f;
    [SerializeField, Min(0.1f)] float minSoundDelay = 2.5f;
    [SerializeField, Min(0.1f)] float maxSoundDelay = 5f;
    [SerializeField, Range(0f, 0.5f)] float pitchVariation = 0.12f;

    private waveManager manager;
    private MobSpawner spawner;
    private Transform player;
    private NavMeshAgent agent;
    private AudioSource audioSource;
    private float directionTimer;
    private float soundTimer;
    private bool isDead;
    private bool removedFromSpawner;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 25f;
        audioSource.volume = soundVolume;
    }

    private void OnValidate()
    {
        maxSoundDelay =
            Mathf.Max(minSoundDelay, maxSoundDelay);
    }

    private void Start()
    {
        FindManagerAndPlayer();

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = 0f;
            agent.autoBraking = false;
            agent.updateRotation = true;
            agent.updatePosition = true;

            PlaceOnNavMesh();
            PickNewDestination();
        }

        ResetSoundTimer();
    }

    private void Update()
    {
        if (isDead)
            return;

        if (manager != null && manager.HogHuntFinished)
        {
            RemoveFromSpawner();
            Destroy(gameObject);
            return;
        }

        if (agent == null)
            return;

        if (!agent.isOnNavMesh)
        {
            PlaceOnNavMesh();
            return;
        }

        directionTimer -= Time.deltaTime;

        if (directionTimer <= 0f ||
            (!agent.pathPending &&
             agent.remainingDistance <= destinationReachDistance))
        {
            PickNewDestination();
        }

        PlayMovingSound();
    }

    private void OnDestroy()
    {
        RemoveFromSpawner();
    }

    public void SetSpawner(MobSpawner owner)
    {
        spawner = owner;
    }

    public void SetManager(waveManager owner)
    {
        manager = owner;
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        HP -= amount;

        if (HP <= 0)
        {
            Die();
        }
    }

    public void onDeath(bool dead)
    {
        if (dead)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        //ScoreboardManager.GetOrCreate().AddRegularHogKilled();
        PlayDeathSound();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        if (manager != null)
        {
            manager.RegularPigKilled(gameObject);
        }

        RemoveFromSpawner();
        Destroy(gameObject);
    }

    private void FindManagerAndPlayer()
    {
        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        if (gameManager.instance != null &&
            gameManager.instance.player != null)
        {
            player = gameManager.instance.player.transform;
            return;
        }

        GameObject playerObject =
            GameObject.FindWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void PlaceOnNavMesh()
    {
        if (agent == null)
            return;

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                8f,
                NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    private void PickNewDestination()
    {
        directionTimer =
            Random.Range(
                directionChangeTime * 0.75f,
                directionChangeTime * 1.25f);

        if (agent == null || !agent.isOnNavMesh)
            return;

        Vector3 direction =
            GetDirectionAwayFromPlayer();

        Vector3 targetPosition =
            transform.position + direction * wanderRadius;

        if (NavMesh.SamplePosition(
                targetPosition,
                out NavMeshHit hit,
                wanderRadius,
                NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    private Vector3 GetDirectionAwayFromPlayer()
    {
        Vector2 randomCircle =
            Random.insideUnitCircle.normalized;

        Vector3 randomDirection =
            new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (randomDirection.sqrMagnitude < 0.001f)
        {
            randomDirection = transform.forward;
        }

        if (player == null)
        {
            FindManagerAndPlayer();
        }

        if (player == null)
            return randomDirection.normalized;

        Vector3 awayFromPlayer =
            transform.position - player.position;

        awayFromPlayer.y = 0f;

        if (awayFromPlayer.sqrMagnitude < 0.001f)
            return randomDirection.normalized;

        float playerDistance =
            awayFromPlayer.magnitude;

        if (playerDistance > fleeDistance)
            return randomDirection.normalized;

        awayFromPlayer.Normalize();

        Vector3 mixedDirection =
            awayFromPlayer * fleeWeight + randomDirection;

        if (mixedDirection.sqrMagnitude < 0.001f)
            return randomDirection.normalized;

        return mixedDirection.normalized;
    }

    private void PlayMovingSound()
    {
        if (audioSource == null ||
            movingSounds == null ||
            movingSounds.Length == 0 ||
            agent == null ||
            agent.velocity.sqrMagnitude < 0.05f)
        {
            return;
        }

        soundTimer -= Time.deltaTime;

        if (soundTimer > 0f)
            return;

        AudioClip clip =
            movingSounds[Random.Range(0, movingSounds.Length)];

        if (clip != null)
        {
            audioSource.pitch =
                Random.Range(
                    1f - pitchVariation,
                    1f + pitchVariation);

            audioSource.PlayOneShot(clip, soundVolume);
        }

        ResetSoundTimer();
    }

    private void PlayDeathSound()
    {
        if (deathSound == null)
            return;

        AudioSource.PlayClipAtPoint(
            deathSound,
            transform.position,
            soundVolume);
    }

    private void ResetSoundTimer()
    {
        soundTimer =
            Random.Range(minSoundDelay, maxSoundDelay);
    }

    private void RemoveFromSpawner()
    {
        if (removedFromSpawner)
            return;

        removedFromSpawner = true;

        if (spawner != null)
        {
            spawner.RegularPigDied();
        }
    }
}
