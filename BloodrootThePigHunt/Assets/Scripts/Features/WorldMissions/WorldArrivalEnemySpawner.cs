using System;
using System.Collections.Generic;
using Bloodroot.Features.AlphaEnemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    [Serializable]
    public sealed class WorldArrivalEnemySpawnDefinition
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform spawnPoint;

        public GameObject EnemyPrefab => enemyPrefab;
        public Transform SpawnPoint => spawnPoint;

        public WorldArrivalEnemySpawnDefinition()
        {
        }

        public WorldArrivalEnemySpawnDefinition(
            GameObject prefab,
            Transform point)
        {
            enemyPrefab = prefab;
            spawnPoint = point;
        }
    }

    /// <summary>
    /// Finite, scene-authored ambient encounter. The component waits for the
    /// authoritative Player to physically enter its trigger, samples every
    /// authored point against the baked NavMesh, then creates the configured
    /// enemies exactly once for this scene session.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WorldArrivalEnemySpawner : MonoBehaviour
    {
        [Header("Arrival Trigger")]
        [SerializeField] private Collider arrivalTrigger;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool disableTriggerAfterSpawn = true;

        [Header("Finite Authored Spawn Set")]
        [SerializeField] private Transform runtimeContainer;
        [SerializeField] private WorldArrivalEnemySpawnDefinition[] spawns =
            Array.Empty<WorldArrivalEnemySpawnDefinition>();
        [SerializeField, Range(1, 5)] private int difficultyLevel = 1;
        [SerializeField, Range(0.25f, 10f)] private float navMeshSampleRadius =
            6f;

        [Header("Authored Events")]
        [SerializeField] private UnityEvent enemiesSpawned = new UnityEvent();

        private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
        private readonly List<Component> activeControllers =
            new List<Component>();
        private bool hasSpawned;
        private string lastFailure = string.Empty;
        private float nextAlertAt;
        private float nextArrivalReconcileAt;
        private float nextSpawnAttemptAt;

        private const float AlertRefreshSeconds = 0.25f;
        private const float ArrivalReconcileSeconds = 0.25f;
        private const float FailureRetrySeconds = 0.25f;

        public Collider ArrivalTrigger => arrivalTrigger;
        public Transform RuntimeContainer => runtimeContainer;
        public IReadOnlyList<WorldArrivalEnemySpawnDefinition> Spawns => spawns;
        public int DifficultyLevel => difficultyLevel;
        public float NavMeshSampleRadius => navMeshSampleRadius;
        public string PlayerTag => playerTag;
        public bool DisableTriggerAfterSpawn => disableTriggerAfterSpawn;
        public bool HasSpawned => hasSpawned;
        public int SpawnedCount => spawnedEnemies.Count;
        public string LastFailure => lastFailure;

        private void Awake()
        {
            if (arrivalTrigger == null)
            {
                arrivalTrigger = GetComponent<BoxCollider>();
            }
        }

        private void OnEnable()
        {
            if (!hasSpawned)
            {
                nextArrivalReconcileAt =
                    Time.unscaledTime + ArrivalReconcileSeconds;
            }
        }

        private void OnValidate()
        {
            difficultyLevel = Mathf.Clamp(difficultyLevel, 1, 5);
            navMeshSampleRadius = Mathf.Clamp(
                navMeshSampleRadius,
                0.25f,
                10f);
        }

        private void OnTriggerEnter(Collider other)
        {
            TrySpawnFor(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Retry if the Player crossed while Safety's scene bootstrap was
            // still assigning the authoritative gameManager.player reference.
            TrySpawnFor(other);
        }

        private void Update()
        {
            if (!hasSpawned)
            {
                if (Time.unscaledTime >= nextArrivalReconcileAt)
                {
                    nextArrivalReconcileAt =
                        Time.unscaledTime + ArrivalReconcileSeconds;
                    TryReconcileLoadedPlayerArrival();
                }

                return;
            }

            if (Time.time < nextAlertAt)
                return;

            nextAlertAt = Time.time + AlertRefreshSeconds;

            GameObject player = ResolveAuthoritativePlayer();
            if (player == null)
                return;

            Vector3 playerPosition = player.transform.position;
            for (int index = activeControllers.Count - 1; index >= 0; index--)
            {
                Component controller = activeControllers[index];
                if (controller == null)
                {
                    activeControllers.RemoveAt(index);
                    continue;
                }

                // Keep every finite arrival enemy alerted through the shared
                // adapter. Arrival encounters intentionally pursue across the
                // region after their one-time authored activation.
                if (!CampaignSafetyEnemyRuntimeAdapter.TryAlert(
                        controller,
                        playerPosition,
                        out string alertError))
                {
                    activeControllers.RemoveAt(index);

                }
            }
        }

        public void Configure(
            Collider trigger,
            Transform spawnedEnemyContainer,
            WorldArrivalEnemySpawnDefinition[] authoredSpawns,
            int authoredDifficultyLevel,
            float authoredNavMeshSampleRadius,
            string authoredPlayerTag = "Player")
        {
            arrivalTrigger = trigger;
            runtimeContainer = spawnedEnemyContainer;
            spawns = authoredSpawns ??
                Array.Empty<WorldArrivalEnemySpawnDefinition>();
            difficultyLevel = Mathf.Clamp(authoredDifficultyLevel, 1, 5);
            navMeshSampleRadius = Mathf.Clamp(
                authoredNavMeshSampleRadius,
                0.25f,
                10f);
            playerTag = string.IsNullOrWhiteSpace(authoredPlayerTag)
                ? "Player"
                : authoredPlayerTag;
            disableTriggerAfterSpawn = true;
        }

        public bool TrySpawnFor(Collider enteringCollider)
        {
            if (hasSpawned || !isActiveAndEnabled ||
                arrivalTrigger == null || !arrivalTrigger.enabled ||
                Time.unscaledTime < nextSpawnAttemptAt)
            {
                return false;
            }

            GameObject player = ResolveAuthoritativePlayer();
            if (!IsAuthoritativePlayerCollider(enteringCollider, player))
                return false;

            if (!ValidateRuntimeContract(out string contractError))
                return Reject(contractError);

            NavMeshHit[] resolved = new NavMeshHit[spawns.Length];
            for (int index = 0; index < spawns.Length; index++)
            {
                WorldArrivalEnemySpawnDefinition spawn = spawns[index];
                if (!CampaignSafetyEnemyRuntimeAdapter
                        .TryResolveGroundedSpawnPosition(
                            spawn.EnemyPrefab,
                            spawn.SpawnPoint.position,
                            navMeshSampleRadius,
                            out resolved[index],
                            out string groundError))
                {
                    return Reject(
                        $"Arrival enemy point '{spawn.SpawnPoint.name}' was " +
                        $"rejected by grounded NavMesh validation. {groundError}");
                }
            }

            List<GameObject> created = new List<GameObject>(spawns.Length);
            hasSpawned = true;
            if (disableTriggerAfterSpawn)
            {
                arrivalTrigger.enabled = false;
            }

            try
            {
                for (int index = 0; index < spawns.Length; index++)
                {
                    WorldArrivalEnemySpawnDefinition spawn = spawns[index];
                    GameObject instance = Instantiate(
                        spawn.EnemyPrefab,
                        resolved[index].position,
                        spawn.SpawnPoint.rotation,
                        runtimeContainer);
                    instance.name =
                        $"{spawn.EnemyPrefab.name} (Arrival Spawn {index + 1:00})";
                    created.Add(instance);

                    if (!CampaignSafetyEnemyRuntimeAdapter
                            .TryPrepareGroundedSpawn(
                                instance,
                                resolved[index],
                                out Component controller,
                                out string placementError))
                    {
                        throw new InvalidOperationException(
                            placementError);
                    }

                    if (!CampaignSafetyEnemyRuntimeAdapter.TryInitialize(
                            controller,
                            difficultyLevel,
                            out string initializationError))
                    {
                        throw new InvalidOperationException(
                            initializationError);
                    }

                    if (!CampaignSafetyEnemyRuntimeAdapter.TryAlert(
                            controller,
                            player.transform.position,
                            out string alertError))
                    {
                        throw new InvalidOperationException(alertError);
                    }

                    activeControllers.Add(controller);
                }

                spawnedEnemies.AddRange(created);
                lastFailure = string.Empty;
                nextAlertAt = Time.time + AlertRefreshSeconds;
                WorldMissionEventUtility.Invoke(enemiesSpawned, this);
                return true;
            }
            catch (Exception exception)
            {
                activeControllers.Clear();
                for (int index = created.Count - 1; index >= 0; index--)
                {
                    if (created[index] == null)
                        continue;

                    created[index].SetActive(false);

                    if (Application.isPlaying)
                    {
                        Destroy(created[index]);
                    }
                    else
                    {
                        DestroyImmediate(created[index]);
                    }
                }

                hasSpawned = false;
                if (disableTriggerAfterSpawn && arrivalTrigger != null)
                {
                    arrivalTrigger.enabled = true;
                }
                nextSpawnAttemptAt = Application.isPlaying
                    ? Time.unscaledTime + FailureRetrySeconds
                    : 0f;

                return Reject(
                    "Arrival enemy spawning rolled back after a partial " +
                    $"failure: {exception.Message}");
            }
        }

        /// <summary>
        /// Reconciles a Continue/F9/startup placement that restored the Player
        /// beyond the authored arrival plane without physically crossing the
        /// trigger during this scene instance.
        /// </summary>
        public bool TryReconcileLoadedPlayerArrival()
        {
            if (hasSpawned || !isActiveAndEnabled ||
                !(arrivalTrigger is BoxCollider box))
            {
                return false;
            }

            GameObject player = ResolveAuthoritativePlayer();
            if (player == null || !IsAtOrBeyondArrivalPlane(
                    box,
                    player.transform.position))
            {
                return false;
            }

            Collider[] colliders = player.GetComponentsInChildren<Collider>(
                true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider != null && collider.enabled &&
                    collider.gameObject.activeInHierarchy)
                {
                    return TrySpawnFor(collider);
                }
            }

            return Reject(
                "The authoritative Player has no enabled collider for the " +
                "arrival encounter.");
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
            if (arrivalTrigger == null || boxColliders.Length != 1 ||
                allColliders.Length != 1 ||
                arrivalTrigger != boxColliders[0] ||
                !arrivalTrigger.enabled || !arrivalTrigger.isTrigger)
            {
                error =
                    "Arrival spawning requires its one enabled BoxCollider as a trigger.";
                return false;
            }

            if (playerTag != "Player" || !disableTriggerAfterSpawn)
            {
                error =
                    "Arrival spawning must use the exact Player tag and disable its trigger after success.";
                return false;
            }

            if (runtimeContainer == null ||
                runtimeContainer == transform ||
                runtimeContainer.IsChildOf(transform) ||
                (!hasSpawned && runtimeContainer.childCount != 0))
            {
                error =
                    "Arrival spawning requires a separate, initially empty runtime container.";
                return false;
            }

            if (spawns == null || spawns.Length < 1 || spawns.Length > 8)
            {
                error = "Arrival spawning requires one to eight finite spawn points.";
                return false;
            }

            if (difficultyLevel < 1 || difficultyLevel > 5 ||
                navMeshSampleRadius < 0.25f || navMeshSampleRadius > 10f)
            {
                error = "Arrival enemy difficulty or NavMesh sampling is out of bounds.";
                return false;
            }

            HashSet<Transform> uniquePoints = new HashSet<Transform>();
            Transform commonParent = null;
            for (int index = 0; index < spawns.Length; index++)
            {
                WorldArrivalEnemySpawnDefinition spawn = spawns[index];
                if (spawn == null || spawn.EnemyPrefab == null ||
                    spawn.SpawnPoint == null)
                {
                    error = $"Arrival spawn {index + 1} is incomplete.";
                    return false;
                }

                if (!uniquePoints.Add(spawn.SpawnPoint) ||
                    spawn.SpawnPoint == transform ||
                    spawn.SpawnPoint.IsChildOf(runtimeContainer) ||
                    !spawn.SpawnPoint.gameObject.activeSelf ||
                    spawn.SpawnPoint.GetComponents<Component>().Length != 1)
                {
                    error =
                        $"Arrival spawn point {index + 1} must be a unique, active, Transform-only authored marker.";
                    return false;
                }

                if (commonParent == null)
                {
                    commonParent = spawn.SpawnPoint.parent;
                }
                else if (spawn.SpawnPoint.parent != commonParent)
                {
                    error = "Arrival spawn points must share one authored marker root.";
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
                        $"Arrival spawn {index + 1} must reference one exact " +
                        "regular Boar, Root Boar, Juggernaut, raw Screecher, " +
                        "or campaign Wereboar prefab. " +
                        prefabError;
                    return false;
                }
            }

            if (commonParent == null || commonParent == runtimeContainer ||
                commonParent.childCount != spawns.Length)
            {
                error =
                    "Arrival spawn points must be the complete direct children of one marker root.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool Reject(string reason)
        {
            string normalized = string.IsNullOrWhiteSpace(reason)
                ? "Arrival enemy spawning was rejected."
                : reason;
            lastFailure = normalized;
            return false;
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

        private static bool IsAtOrBeyondArrivalPlane(
            BoxCollider box,
            Vector3 playerPosition)
        {
            Vector3 local = box.transform.InverseTransformPoint(playerPosition) -
                box.center;
            return local.z >= -(box.size.z * 0.5f);
        }

    }
}
