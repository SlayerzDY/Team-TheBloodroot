using System;
using System.Collections.Generic;
using Bloodroot.Features.AlphaEnemies;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodroot.Features.WorldMissions
{
    [Serializable]
    public sealed class OpenWorldAmbientEnemySpawnDefinition
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform spawnPoint;

        public GameObject EnemyPrefab => enemyPrefab;
        public Transform SpawnPoint => spawnPoint;

        public OpenWorldAmbientEnemySpawnDefinition()
        {
        }

        public OpenWorldAmbientEnemySpawnDefinition(
            GameObject prefab,
            Transform point)
        {
            enemyPrefab = prefab;
            spawnPoint = point;
        }
    }

    /// <summary>
    /// Area-owned ambient population for the Open World. Each active campaign
    /// area owns a small capped set of recurring threats; encounter spawners
    /// remain finite and independent.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    [DisallowMultipleComponent]
    public sealed class OpenWorldAmbientThreatSpawner : MonoBehaviour
    {
        private const int WalkableAreaMask = 1;

        [Header("Recurring Population")]
        [SerializeField] private Transform runtimeContainer;
        [SerializeField] private OpenWorldAmbientEnemySpawnDefinition[] spawns =
            Array.Empty<OpenWorldAmbientEnemySpawnDefinition>();
        [SerializeField, Range(1, 5)] private int difficultyLevel = 1;
        [SerializeField, Range(1, 12)] private int maximumAlive = 5;
        [SerializeField, Min(0.5f)] private float respawnInterval = 7f;
        [SerializeField, Min(0f)] private float initialSpawnDelay = 2f;
        [SerializeField, Min(1f)] private float minimumPlayerDistance = 22f;
        [SerializeField, Range(0.25f, 25f)] private float navMeshSampleRadius =
            10f;

        [Header("Finale Safety")]
        [SerializeField] private bool suppressDuringWitchCombat;
        [SerializeField] private WitchEncounterDirector witchEncounterDirector;

        private readonly List<GameObject> aliveEnemies =
            new List<GameObject>();
        private int nextSpawnIndex;
        private float nextSpawnAt;
        private bool initialPopulationFilled;

        public IReadOnlyList<OpenWorldAmbientEnemySpawnDefinition> Spawns =>
            spawns;
        public int MaximumAlive => maximumAlive;
        public int AliveCount => aliveEnemies.Count;
        public int DifficultyLevel => difficultyLevel;
        public bool IsSuppressed => suppressDuringWitchCombat &&
                                    witchEncounterDirector != null &&
                                    witchEncounterDirector.IsDefenseActive;

        private void Awake()
        {
            if (runtimeContainer == null)
            {
                runtimeContainer = transform;
            }
        }

        private void OnEnable()
        {
            initialPopulationFilled = false;
            nextSpawnAt = Time.unscaledTime + initialSpawnDelay;
        }

        private void OnDisable()
        {
            ClearOwnedEnemies();
            initialPopulationFilled = false;
        }

        private void OnValidate()
        {
            spawns ??= Array.Empty<OpenWorldAmbientEnemySpawnDefinition>();
            difficultyLevel = Mathf.Clamp(difficultyLevel, 1, 5);
            maximumAlive = Mathf.Clamp(maximumAlive, 1, 12);
            respawnInterval = Mathf.Max(0.5f, respawnInterval);
            initialSpawnDelay = Mathf.Max(0f, initialSpawnDelay);
            minimumPlayerDistance = Mathf.Max(1f, minimumPlayerDistance);
            navMeshSampleRadius = Mathf.Clamp(navMeshSampleRadius, 0.25f, 25f);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PruneDestroyedEnemies();
            if (Time.unscaledTime < nextSpawnAt || IsSuppressed ||
                aliveEnemies.Count >= maximumAlive)
            {
                return;
            }

            GameObject player = ResolveAuthoritativePlayer();
            if (player == null)
            {
                return;
            }

            bool spawned = TrySpawnOne(player);
            if (aliveEnemies.Count >= maximumAlive)
            {
                initialPopulationFilled = true;
            }

            // Bring an area to its authored threat cap promptly on entry, then
            // maintain that population at the normal recurring interval.
            float delay = spawned && !initialPopulationFilled
                ? Mathf.Min(0.35f, respawnInterval)
                : (spawned ? respawnInterval : Mathf.Min(2f, respawnInterval));
            nextSpawnAt = Time.unscaledTime + delay;
        }

        public void Configure(
            Transform spawnedEnemyContainer,
            OpenWorldAmbientEnemySpawnDefinition[] authoredSpawns,
            int authoredDifficultyLevel,
            int authoredMaximumAlive,
            float authoredRespawnInterval,
            float authoredInitialSpawnDelay,
            float authoredMinimumPlayerDistance,
            float authoredNavMeshSampleRadius,
            bool authoredSuppressDuringWitchCombat = false,
            WitchEncounterDirector authoredWitchEncounterDirector = null)
        {
            runtimeContainer = spawnedEnemyContainer != null
                ? spawnedEnemyContainer
                : transform;
            spawns = authoredSpawns ??
                Array.Empty<OpenWorldAmbientEnemySpawnDefinition>();
            difficultyLevel = Mathf.Clamp(authoredDifficultyLevel, 1, 5);
            maximumAlive = Mathf.Clamp(authoredMaximumAlive, 1, 12);
            respawnInterval = Mathf.Max(0.5f, authoredRespawnInterval);
            initialSpawnDelay = Mathf.Max(0f, authoredInitialSpawnDelay);
            minimumPlayerDistance = Mathf.Max(1f, authoredMinimumPlayerDistance);
            navMeshSampleRadius = Mathf.Clamp(
                authoredNavMeshSampleRadius,
                0.25f,
                25f);
            suppressDuringWitchCombat = authoredSuppressDuringWitchCombat;
            witchEncounterDirector = authoredWitchEncounterDirector;
        }

        public bool ValidateRuntimeContract(out string error)
        {
            if (!CampaignSafetyEnemyRuntimeAdapter.ValidateSafetyContract(
                    out error))
            {
                return false;
            }

            if (runtimeContainer == null || spawns == null || spawns.Length == 0)
            {
                error = "Ambient population requires a runtime container and at least one spawn definition.";
                return false;
            }

            foreach (OpenWorldAmbientEnemySpawnDefinition spawn in spawns)
            {
                if (spawn == null || spawn.EnemyPrefab == null ||
                    spawn.SpawnPoint == null ||
                    !CampaignSafetyEnemyRuntimeAdapter.TryValidatePrefab(
                        spawn.EnemyPrefab,
                        out _,
                        out _,
                        out _,
                        out error))
                {
                    error = string.IsNullOrWhiteSpace(error)
                        ? "Ambient population contains an invalid enemy spawn definition."
                        : error;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool TrySpawnOne(GameObject player)
        {
            if (!ValidateRuntimeContract(out _))
            {
                return false;
            }

            float minimumDistanceSquared = minimumPlayerDistance * minimumPlayerDistance;
            for (int attempt = 0; attempt < spawns.Length; attempt++)
            {
                int index = (nextSpawnIndex + attempt) % spawns.Length;
                OpenWorldAmbientEnemySpawnDefinition spawn = spawns[index];
                if (spawn == null || spawn.EnemyPrefab == null ||
                    spawn.SpawnPoint == null ||
                    (spawn.SpawnPoint.position - player.transform.position)
                    .sqrMagnitude < minimumDistanceSquared)
                {
                    continue;
                }

                NavMeshAgent prefabAgent = spawn.EnemyPrefab
                    .GetComponentInChildren<NavMeshAgent>(true);
                int areaMask = prefabAgent != null
                    ? prefabAgent.areaMask & WalkableAreaMask
                    : 0;
                if (areaMask == 0 || !NavMesh.SamplePosition(
                        spawn.SpawnPoint.position,
                        out NavMeshHit sampledPosition,
                        navMeshSampleRadius,
                        areaMask))
                {
                    continue;
                }

                if (TryCreateEnemy(spawn, sampledPosition, player))
                {
                    nextSpawnIndex = (index + 1) % spawns.Length;
                    return true;
                }
            }

            return false;
        }

        private bool TryCreateEnemy(
            OpenWorldAmbientEnemySpawnDefinition spawn,
            NavMeshHit sampledPosition,
            GameObject player)
        {
            GameObject instance = null;
            try
            {
                instance = Instantiate(
                    spawn.EnemyPrefab,
                    sampledPosition.position,
                    spawn.SpawnPoint.rotation,
                    runtimeContainer);
                instance.name = spawn.EnemyPrefab.name + " (Ambient Threat)";

                if (!CampaignSafetyEnemyRuntimeAdapter.TryGetExactAllowedController(
                        instance,
                        out Component controller,
                        out _) ||
                    !CampaignSafetyEnemyRuntimeAdapter.TryPrepare(
                        instance,
                        out _) ||
                    !CampaignSafetyEnemyRuntimeAdapter.TryGetAgent(
                        instance,
                        out NavMeshAgent agent,
                        out _) ||
                    (!agent.isOnNavMesh &&
                     (!agent.Warp(sampledPosition.position) || !agent.isOnNavMesh)) ||
                    !CampaignSafetyEnemyRuntimeAdapter.TryInitialize(
                        controller,
                        difficultyLevel,
                        out _) ||
                    !CampaignSafetyEnemyRuntimeAdapter.TryAlert(
                        controller,
                        player.transform.position,
                        out _))
                {
                    Destroy(instance);
                    return false;
                }

                aliveEnemies.Add(instance);
                return true;
            }
            catch (Exception)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }

                return false;
            }
        }

        private static GameObject ResolveAuthoritativePlayer()
        {
            GameObject player = gameManager.instance != null
                ? gameManager.instance.player
                : null;
            return player != null && player.CompareTag("Player") ? player : null;
        }

        private void PruneDestroyedEnemies()
        {
            aliveEnemies.RemoveAll(enemy => enemy == null);
        }

        private void ClearOwnedEnemies()
        {
            foreach (GameObject enemy in aliveEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy);
                }
            }

            aliveEnemies.Clear();
        }
    }
}
