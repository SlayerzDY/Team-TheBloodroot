using System;
using System.Collections.Generic;
using Bloodroot.Features.AlphaEnemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    [Serializable]
    public sealed class WorldLandmarkEnemySpawnDefinition
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform spawnPoint;

        public GameObject EnemyPrefab => enemyPrefab;
        public Transform SpawnPoint => spawnPoint;

        public WorldLandmarkEnemySpawnDefinition()
        {
        }

        public WorldLandmarkEnemySpawnDefinition(
            GameObject prefab,
            Transform point)
        {
            enemyPrefab = prefab;
            spawnPoint = point;
        }
    }

    /// <summary>
    /// Finite local encounter for a progression tower or other authored world
    /// landmark. Only the authoritative Player can activate it. A successful
    /// encounter spawns once and alerts each enemy once; it never reconciles
    /// against a world plane and never refreshes aggro across the map.
    /// </summary>
    [DefaultExecutionOrder(-145)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WorldLandmarkEnemySpawner : MonoBehaviour
    {
        private const int WalkableAreaMask = 1;
        private const int MinimumSpawnAttempts = 1;
        private const int MaximumSpawnAttemptsLimit = 5;
        private const float MinimumRetryDelay = 0.1f;
        private const float MaximumRetryDelay = 5f;

        [Header("Local Proximity Trigger")]
        [SerializeField] private Collider proximityTrigger;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool disableTriggerAfterSpawn = true;

        [Header("Finite Authored Spawn Set")]
        [SerializeField] private Transform runtimeContainer;
        [SerializeField] private WorldLandmarkEnemySpawnDefinition[] spawns =
            Array.Empty<WorldLandmarkEnemySpawnDefinition>();
        [SerializeField, Range(1, 5)] private int difficultyLevel = 1;
        [SerializeField, Range(0.25f, 10f)] private float navMeshSampleRadius =
            6f;

        [Header("Bounded Failure Policy")]
        [SerializeField, Range(MinimumSpawnAttempts, MaximumSpawnAttemptsLimit)]
        private int maximumSpawnAttempts = 3;
        [SerializeField, Range(MinimumRetryDelay, MaximumRetryDelay)]
        private float retryDelay = 0.5f;

        [Header("Optional Hollow Witch Suppression")]
        [SerializeField] private bool suppressDuringWitchCombat;
        [SerializeField] private WitchEncounterDirector witchEncounterDirector;

        [Header("Authored Events")]
        [SerializeField] private UnityEvent enemiesSpawned = new UnityEvent();

        private readonly List<GameObject> spawnedEnemies =
            new List<GameObject>();
        private bool hasSpawned;
        private int failedAttemptCount;
        private bool attemptsExhausted;
        private float nextSpawnAttemptAt;
        private string lastFailure = string.Empty;

        public Collider ProximityTrigger => proximityTrigger;
        public Transform RuntimeContainer => runtimeContainer;
        public IReadOnlyList<WorldLandmarkEnemySpawnDefinition> Spawns => spawns;
        public int DifficultyLevel => difficultyLevel;
        public float NavMeshSampleRadius => navMeshSampleRadius;
        public string PlayerTag => playerTag;
        public bool DisableTriggerAfterSpawn => disableTriggerAfterSpawn;
        public int MaximumSpawnAttempts => maximumSpawnAttempts;
        public float RetryDelay => retryDelay;
        public bool SuppressDuringWitchCombat => suppressDuringWitchCombat;
        public WitchEncounterDirector WitchEncounterDirector =>
            witchEncounterDirector;
        public bool IsSuppressed => suppressDuringWitchCombat &&
                                    witchEncounterDirector != null &&
                                    witchEncounterDirector.IsDefenseActive;
        public bool HasSpawned => hasSpawned;
        public int FailedAttemptCount => failedAttemptCount;
        public bool AttemptsExhausted => attemptsExhausted;
        public int SpawnedCount => spawnedEnemies.Count;
        public string LastFailure => lastFailure;

        private void Awake()
        {
            if (proximityTrigger == null)
            {
                proximityTrigger = GetComponent<BoxCollider>();
            }
        }

        private void OnValidate()
        {
            difficultyLevel = Mathf.Clamp(difficultyLevel, 1, 5);
            navMeshSampleRadius = Mathf.Clamp(
                navMeshSampleRadius,
                0.25f,
                10f);
            maximumSpawnAttempts = Mathf.Clamp(
                maximumSpawnAttempts,
                MinimumSpawnAttempts,
                MaximumSpawnAttemptsLimit);
            retryDelay = Mathf.Clamp(
                retryDelay,
                MinimumRetryDelay,
                MaximumRetryDelay);
        }

        private void OnTriggerEnter(Collider other)
        {
            TrySpawnFor(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // TriggerStay provides only the bounded retry window. It does not
            // reconcile distant loaded-player placement or refresh enemy aggro.
            TrySpawnFor(other);
        }

        public void Configure(
            Collider trigger,
            Transform spawnedEnemyContainer,
            WorldLandmarkEnemySpawnDefinition[] authoredSpawns,
            int authoredDifficultyLevel,
            float authoredNavMeshSampleRadius,
            string authoredPlayerTag = "Player",
            int authoredMaximumSpawnAttempts = 3,
            float authoredRetryDelay = 0.5f,
            bool authoredSuppressDuringWitchCombat = false,
            WitchEncounterDirector authoredWitchEncounterDirector = null)
        {
            proximityTrigger = trigger;
            runtimeContainer = spawnedEnemyContainer;
            spawns = authoredSpawns ??
                Array.Empty<WorldLandmarkEnemySpawnDefinition>();
            difficultyLevel = Mathf.Clamp(authoredDifficultyLevel, 1, 5);
            navMeshSampleRadius = Mathf.Clamp(
                authoredNavMeshSampleRadius,
                0.25f,
                10f);
            playerTag = string.IsNullOrWhiteSpace(authoredPlayerTag)
                ? "Player"
                : authoredPlayerTag;
            maximumSpawnAttempts = Mathf.Clamp(
                authoredMaximumSpawnAttempts,
                MinimumSpawnAttempts,
                MaximumSpawnAttemptsLimit);
            retryDelay = Mathf.Clamp(
                authoredRetryDelay,
                MinimumRetryDelay,
                MaximumRetryDelay);
            suppressDuringWitchCombat = authoredSuppressDuringWitchCombat;
            witchEncounterDirector = authoredWitchEncounterDirector;
            disableTriggerAfterSpawn = true;
        }

        public bool TrySpawnFor(Collider enteringCollider)
        {
            if (hasSpawned || attemptsExhausted || !isActiveAndEnabled ||
                proximityTrigger == null || !proximityTrigger.enabled ||
                Time.unscaledTime < nextSpawnAttemptAt)
            {
                return false;
            }

            GameObject player = ResolveAuthoritativePlayer();
            if (!IsAuthoritativePlayerCollider(enteringCollider, player) ||
                IsSuppressed)
            {
                return false;
            }

            if (!ValidateRuntimeContract(out string contractError))
            {
                return RecordFailedAttempt(contractError);
            }

            NavMeshHit[] resolved = new NavMeshHit[spawns.Length];
            for (int index = 0; index < spawns.Length; index++)
            {
                WorldLandmarkEnemySpawnDefinition spawn = spawns[index];
                NavMeshAgent prefabAgent = spawn.EnemyPrefab
                    .GetComponentInChildren<NavMeshAgent>(true);
                int areaMask = prefabAgent != null
                    ? prefabAgent.areaMask & WalkableAreaMask
                    : 0;
                if (areaMask == 0)
                {
                    return RecordFailedAttempt(
                        $"Landmark enemy '{spawn.EnemyPrefab.name}' cannot " +
                        "use the authored Walkable NavMesh area.");
                }

                if (!NavMesh.SamplePosition(
                        spawn.SpawnPoint.position,
                        out resolved[index],
                        navMeshSampleRadius,
                        areaMask))
                {
                    return RecordFailedAttempt(
                        $"Landmark enemy point '{spawn.SpawnPoint.name}' is " +
                        $"not within {navMeshSampleRadius:0.##}m of the baked NavMesh.");
                }
            }

            List<GameObject> created = new List<GameObject>(spawns.Length);
            hasSpawned = true;
            if (disableTriggerAfterSpawn)
            {
                proximityTrigger.enabled = false;
            }

            try
            {
                for (int index = 0; index < spawns.Length; index++)
                {
                    WorldLandmarkEnemySpawnDefinition spawn = spawns[index];
                    GameObject instance = Instantiate(
                        spawn.EnemyPrefab,
                        resolved[index].position,
                        spawn.SpawnPoint.rotation,
                        runtimeContainer);
                    instance.name =
                        $"{spawn.EnemyPrefab.name} (Landmark Spawn {index + 1:00})";
                    created.Add(instance);

                    if (!CampaignSafetyEnemyRuntimeAdapter
                            .TryGetExactAllowedController(
                                instance,
                                out Component controller,
                                out string controllerError))
                    {
                        throw new InvalidOperationException(controllerError);
                    }

                    if (!CampaignSafetyEnemyRuntimeAdapter.TryPrepare(
                            instance,
                            out string compatibilityError))
                    {
                        throw new InvalidOperationException(
                            compatibilityError);
                    }

                    if (!CampaignSafetyEnemyRuntimeAdapter.TryGetAgent(
                            instance,
                            out NavMeshAgent agent,
                            out string agentError))
                    {
                        throw new InvalidOperationException(agentError);
                    }

                    if (!agent.isOnNavMesh &&
                        (!agent.Warp(resolved[index].position) ||
                         !agent.isOnNavMesh))
                    {
                        throw new InvalidOperationException(
                            $"Spawned enemy '{instance.name}' could not bind " +
                            "to the baked NavMesh.");
                    }

                    if (!CampaignSafetyEnemyRuntimeAdapter.TryInitialize(
                            controller,
                            difficultyLevel,
                            out string initializationError))
                    {
                        throw new InvalidOperationException(
                            initializationError);
                    }

                    // Landmark enemies receive one local activation alert.
                    // Their own AI owns all pursuit after this point.
                    if (!CampaignSafetyEnemyRuntimeAdapter.TryAlert(
                            controller,
                            player.transform.position,
                            out string alertError))
                    {
                        throw new InvalidOperationException(alertError);
                    }
                }

                spawnedEnemies.AddRange(created);
                lastFailure = string.Empty;
                WorldMissionEventUtility.Invoke(enemiesSpawned, this);
                return true;
            }
            catch (Exception exception)
            {
                Rollback(created);
                hasSpawned = false;
                return RecordFailedAttempt(
                    "Landmark enemy spawning rolled back after a partial " +
                    $"failure: {exception.Message}");
            }
        }

        public bool ValidateRuntimeContract(out string error)
        {
            if (!CampaignSafetyEnemyRuntimeAdapter.ValidateSafetyContract(
                    out error))
            {
                return false;
            }

            BoxCollider[] boxColliders = GetComponents<BoxCollider>();
            Collider[] allColliders = GetComponents<Collider>();
            if (proximityTrigger == null || boxColliders.Length != 1 ||
                allColliders.Length != 1 ||
                proximityTrigger != boxColliders[0] ||
                !proximityTrigger.enabled || !proximityTrigger.isTrigger)
            {
                error =
                    "Landmark spawning requires its one enabled BoxCollider as a trigger.";
                return false;
            }

            if (playerTag != "Player" || !disableTriggerAfterSpawn)
            {
                error =
                    "Landmark spawning must use the exact Player tag and disable its trigger after success.";
                return false;
            }

            if (runtimeContainer == null ||
                runtimeContainer == transform ||
                runtimeContainer.IsChildOf(transform) ||
                (!hasSpawned && runtimeContainer.childCount != 0))
            {
                error =
                    "Landmark spawning requires a separate, initially empty runtime container.";
                return false;
            }

            if (spawns == null || spawns.Length < 1 || spawns.Length > 8)
            {
                error =
                    "Landmark spawning requires one to eight finite spawn points.";
                return false;
            }

            if (difficultyLevel < 1 || difficultyLevel > 5 ||
                navMeshSampleRadius < 0.25f ||
                navMeshSampleRadius > 10f ||
                maximumSpawnAttempts < MinimumSpawnAttempts ||
                maximumSpawnAttempts > MaximumSpawnAttemptsLimit ||
                retryDelay < MinimumRetryDelay ||
                retryDelay > MaximumRetryDelay)
            {
                error =
                    "Landmark enemy difficulty, NavMesh sampling, or bounded retry policy is out of bounds.";
                return false;
            }

            if (suppressDuringWitchCombat &&
                (witchEncounterDirector == null ||
                 witchEncounterDirector.gameObject.scene.handle !=
                 gameObject.scene.handle))
            {
                error =
                    "Hollow suppression requires an explicitly assigned WitchEncounterDirector in the same scene.";
                return false;
            }

            HashSet<Transform> uniquePoints = new HashSet<Transform>();
            Transform commonParent = null;
            for (int index = 0; index < spawns.Length; index++)
            {
                WorldLandmarkEnemySpawnDefinition spawn = spawns[index];
                if (spawn == null || spawn.EnemyPrefab == null ||
                    spawn.SpawnPoint == null)
                {
                    error = $"Landmark spawn {index + 1} is incomplete.";
                    return false;
                }

                if (!uniquePoints.Add(spawn.SpawnPoint) ||
                    spawn.SpawnPoint == transform ||
                    spawn.SpawnPoint.IsChildOf(runtimeContainer) ||
                    !spawn.SpawnPoint.gameObject.activeSelf ||
                    spawn.SpawnPoint.GetComponents<Component>().Length != 1)
                {
                    error =
                        $"Landmark spawn point {index + 1} must be a unique, active, Transform-only authored marker.";
                    return false;
                }

                if (commonParent == null)
                {
                    commonParent = spawn.SpawnPoint.parent;
                }
                else if (spawn.SpawnPoint.parent != commonParent)
                {
                    error =
                        "Landmark spawn points must share one authored marker root.";
                    return false;
                }

                if (!CampaignSafetyEnemyRuntimeAdapter.TryValidatePrefab(
                        spawn.EnemyPrefab,
                        out _,
                        out _,
                        out _,
                        out string prefabError))
                {
                    error =
                        $"Landmark spawn {index + 1} must reference one exact " +
                        "regular Boar, Root Boar, Juggernaut, raw Screecher, " +
                        "or campaign Wereboar prefab. " + prefabError;
                    return false;
                }
            }

            if (commonParent == null || commonParent == runtimeContainer ||
                commonParent.childCount != spawns.Length)
            {
                error =
                    "Landmark spawn points must be the complete direct children of one marker root.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool RecordFailedAttempt(string reason)
        {
            failedAttemptCount = Mathf.Min(
                failedAttemptCount + 1,
                maximumSpawnAttempts);
            attemptsExhausted = failedAttemptCount >= maximumSpawnAttempts;
            nextSpawnAttemptAt = attemptsExhausted
                ? float.PositiveInfinity
                : Time.unscaledTime + retryDelay;

            if (proximityTrigger != null && disableTriggerAfterSpawn)
            {
                proximityTrigger.enabled = !attemptsExhausted;
            }

            string disposition = attemptsExhausted
                ? $"attempt {failedAttemptCount}/{maximumSpawnAttempts}; retries exhausted"
                : $"attempt {failedAttemptCount}/{maximumSpawnAttempts}; retry in {retryDelay:0.##}s";
            return Reject($"{reason} ({disposition}).");
        }

        private bool Reject(string reason)
        {
            string normalized = string.IsNullOrWhiteSpace(reason)
                ? "Landmark enemy spawning was rejected."
                : reason;
            lastFailure = normalized;
            return false;
        }

        private static void Rollback(List<GameObject> created)
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                GameObject instance = created[index];
                if (instance == null)
                    continue;

                instance.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }
            }
        }

        private static GameObject ResolveAuthoritativePlayer()
        {
            return global::gameManager.instance != null
                ? global::gameManager.instance.player
                : null;
        }

        private bool IsAuthoritativePlayerCollider(
            Collider candidate,
            GameObject player)
        {
            if (candidate == null || player == null ||
                !player.CompareTag(playerTag))
            {
                return false;
            }

            Transform candidateTransform = candidate.transform;
            return candidateTransform == player.transform ||
                   candidateTransform.IsChildOf(player.transform);
        }

    }
}
