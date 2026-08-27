using System;
using System.Collections;
using System.Collections.Generic;
using Bloodroot.Campaign;
using Bloodroot.Features.AlphaEnemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Bloodroot.Features.FarmPrologue
{
    [Serializable]
    public sealed class FarmEmergenceStringEvent : UnityEvent<string>
    {
    }

    /// <summary>
    /// Runs the Farm emergence obligation created by each durable Root Tree
    /// offering. The prologue object delegates to the existing authored
    /// three-wave encounter. Later Name Stones use an owned spawn ledger, so
    /// unrelated Enemy-tagged objects and protected base-defense coroutines
    /// cannot influence completion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmRecurringEmergenceDirector : MonoBehaviour
    {
        private const int FirstNameStoneEnemyCount = 5;
        private const int EnemiesAddedPerNameStone = 2;
        private const int FirstNameStoneDifficulty = 2;
        private const float GroundedSpawnNavMeshSampleRadius = 3f;

        [Header("Campaign Authority")]
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private FarmPrologueDirector prologueDirector;
        [SerializeField] private FarmEnemyEmergencePresenter emergencePresenter;

        [Header("Authored Recurring Emergence")]
        [SerializeField] private GameObject[] enemyPrefabs =
            Array.Empty<GameObject>();
        [SerializeField] private Transform[] spawnPoints =
            Array.Empty<Transform>();
        [SerializeField] private BoxCollider spawnContainment;
        [SerializeField, Min(0.05f)] private float spawnIntervalSeconds = 1f;
        [SerializeField, Min(0.1f)] private float completionRetrySeconds = 1f;
        [SerializeField, Min(0.1f)] private float completionRetryMaximumSeconds = 15f;

        [Header("Authored Events")]
        [SerializeField] private FarmEmergenceStringEvent emergenceStarted =
            new FarmEmergenceStringEvent();
        [SerializeField] private FarmEmergenceStringEvent emergenceCompleted =
            new FarmEmergenceStringEvent();
        [SerializeField] private FarmEmergenceStringEvent emergenceFailed =
            new FarmEmergenceStringEvent();

        private readonly HashSet<FarmEmergenceEnemyMarker> liveMarkers =
            new HashSet<FarmEmergenceEnemyMarker>();
        private readonly List<GameObject> ownedEnemies =
            new List<GameObject>();

        private CampaignStateService subscribedState;
        private FarmPrologueDirector subscribedDirector;
        private Coroutine emergenceRoutine;
        private Coroutine revealRetryRoutine;
        private Coroutine stateReconcileRoutine;
        private Coroutine beginRetryRoutine;
        private bool reconcileInProgress;
        private bool shuttingDown;
        private bool spawnFailed;
        private int generation;
        private int consecutiveSpawnFailures;
        private int expectedEnemyCount;
        private string activeOfferingId = string.Empty;

        public bool IsRunning => emergenceRoutine != null;
        public string ActiveOfferingId => activeOfferingId;
        public int OwnedEnemyCount => liveMarkers.Count;
        public int ExpectedEnemyCount => expectedEnemyCount;
        public IReadOnlyList<GameObject> EnemyPrefabs => enemyPrefabs;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
        public BoxCollider SpawnContainment => spawnContainment;
        public FarmEmergenceStringEvent EmergenceStartedEvent =>
            emergenceStarted;
        public FarmEmergenceStringEvent EmergenceCompletedEvent =>
            emergenceCompleted;
        public FarmEmergenceStringEvent EmergenceFailedEvent =>
            emergenceFailed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            shuttingDown = false;
            ResolveReferences();
            Bind();
            QueueStateReconcile();
        }

        private void OnDisable()
        {
            shuttingDown = true;
            Unbind();

            if (emergenceRoutine != null)
            {
                StopCoroutine(emergenceRoutine);
                emergenceRoutine = null;
            }

            if (revealRetryRoutine != null)
            {
                StopCoroutine(revealRetryRoutine);
                revealRetryRoutine = null;
            }

            if (stateReconcileRoutine != null)
            {
                StopCoroutine(stateReconcileRoutine);
                stateReconcileRoutine = null;
            }

            if (beginRetryRoutine != null)
            {
                StopCoroutine(beginRetryRoutine);
                beginRetryRoutine = null;
            }

            DestroyOwnedEnemiesWithoutCompletion();
            ResetRuntimeState();
            consecutiveSpawnFailures = 0;
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            Unbind();
        }

        private void OnValidate()
        {
            spawnIntervalSeconds = Mathf.Max(0.05f, spawnIntervalSeconds);
            completionRetrySeconds = Mathf.Max(0.1f, completionRetrySeconds);
            completionRetryMaximumSeconds = Mathf.Max(
                completionRetrySeconds,
                completionRetryMaximumSeconds);
        }

        public void Configure(
            CampaignStateService state,
            FarmPrologueDirector director,
            FarmEnemyEmergencePresenter presenter,
            GameObject[] authoredEnemyPrefabs,
            Transform[] authoredSpawnPoints,
            float authoredSpawnIntervalSeconds)
        {
            Unbind();
            campaignState = state;
            prologueDirector = director;
            emergencePresenter = presenter;
            enemyPrefabs = authoredEnemyPrefabs ?? Array.Empty<GameObject>();
            spawnPoints = authoredSpawnPoints ?? Array.Empty<Transform>();
            spawnIntervalSeconds = Mathf.Max(
                0.05f,
                authoredSpawnIntervalSeconds);

            if (isActiveAndEnabled)
            {
                Bind();
                ReconcileDurableObligation();
            }
        }

        public bool ValidateAuthoredConfiguration(out string error)
        {
            if (campaignState == null || prologueDirector == null ||
                emergencePresenter == null)
            {
                error =
                    "Recurring Farm emergence requires campaign state, the " +
                    "Farm Prologue Director, and the emergence presenter.";
                return false;
            }

            if (enemyPrefabs == null || enemyPrefabs.Length != 3 ||
                !HasExactRecurringEnemyPrefab(
                    enemyPrefabs[0],
                    typeof(global::BoarBruteAI)) ||
                !HasExactRecurringEnemyPrefab(
                    enemyPrefabs[1],
                    typeof(global::BoarBruteRootAI)) ||
                !HasExactRecurringEnemyPrefab(
                    enemyPrefabs[2],
                    typeof(global::juggernautEnemyAI)))
            {
                error =
                    "Recurring Farm emergence requires the exact ordered " +
                    "regular Boar, Root Boar, Juggernaut prefab roster.";
                return false;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                error = "Recurring Farm emergence requires spawn points.";
                return false;
            }

            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint == null)
                {
                    error =
                        "Recurring Farm emergence contains a null spawn point.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void ConfigureSpawnContainment(BoxCollider containment)
        {
            spawnContainment = containment;
        }

        private static bool HasExactRecurringEnemyPrefab(
            GameObject prefab,
            Type expectedControllerType)
        {
            return CampaignSafetyEnemyRuntimeAdapter.TryValidatePrefab(
                       prefab,
                       out Component controller,
                       out _,
                       out _,
                       out _) &&
                   controller.GetType() == expectedControllerType;
        }

        public static int GetEnemyCountForOffering(string offeringId)
        {
            int index = IndexOfNameStoneOffering(offeringId);
            return index < 0
                ? 0
                : FirstNameStoneEnemyCount +
                  (index * EnemiesAddedPerNameStone);
        }

        public static int GetDifficultyForOffering(string offeringId)
        {
            int index = IndexOfNameStoneOffering(offeringId);
            return index < 0
                ? 0
                : FirstNameStoneDifficulty + index;
        }

        internal void NotifyOwnedEnemyDestroyed(
            FarmEmergenceEnemyMarker marker,
            int markerGeneration)
        {
            if (shuttingDown || marker == null ||
                markerGeneration != generation)
            {
                return;
            }

            liveMarkers.Remove(marker);
            ownedEnemies.RemoveAll(candidate => candidate == null);
        }

        private IEnumerator ReconcileAfterSceneStart()
        {
            yield return null;
            stateReconcileRoutine = null;
            ResolveReferences();
            Bind();
            ReconcileDurableObligation();
        }

        private void HandleProgressChanged(CampaignProgressSnapshot snapshot)
        {
            ReconcileDurableObligation();
        }

        private void HandleProgressLoaded(CampaignProgressSnapshot snapshot)
        {
            RestartForReplacedCampaignState();
        }

        private void HandleNewGameStarted()
        {
            RestartForReplacedCampaignState();
        }

        private void HandlePhaseChanged(FarmProloguePhase phase)
        {
            if (phase == FarmProloguePhase.AwaitingOffering)
            {
                EnsurePrologueReveal();
            }

            ReconcileDurableObligation();
        }

        private void ReconcileDurableObligation()
        {
            if (!isActiveAndEnabled || shuttingDown || reconcileInProgress ||
                emergenceRoutine != null || beginRetryRoutine != null)
            {
                return;
            }

            Bind();
            CampaignStateService state = ResolveState();
            FarmPrologueDirector director = ResolveDirector();
            if (state == null || director == null)
                return;

            if (director.CurrentPhase == FarmProloguePhase.AwaitingOffering &&
                !state.PrologueCursedObjectRevealed)
            {
                EnsurePrologueReveal();
                return;
            }

            reconcileInProgress = true;
            try
            {
                string existingActive =
                    state.ActiveFarmEmergenceOfferingId ?? string.Empty;
                string nextPending =
                    state.NextPendingFarmEmergenceOfferingId ?? string.Empty;
                if (existingActive.Length == 0 && nextPending.Length == 0)
                    return;

                if (!state.TryBeginNextFarmEmergence(out string offeringId) ||
                    string.IsNullOrEmpty(offeringId))
                {
                    if (existingActive.Length == 0 && nextPending.Length > 0)
                        EnsureBeginRetry(nextPending);
                    return;
                }

                if (string.Equals(
                        offeringId,
                        CampaignRootOfferingIds.PrologueCursedObject,
                        StringComparison.Ordinal))
                {
                    if (director.CurrentPhase ==
                            FarmProloguePhase.AwaitingOffering ||
                        director.CurrentPhase == FarmProloguePhase.Rumble)
                    {
                        director.PublishCampaignObjective(
                            "The Root Tree rejected the curse. Defend the farm.",
                            0,
                            1);
                        if (!director.BeginTreeFedEmergence())
                        {
                            EnsureBeginRetry(offeringId);
                        }
                    }

                    return;
                }

                if (director.CurrentPhase != FarmProloguePhase.Hub)
                    return;

                int count = GetEnemyCountForOffering(offeringId);
                if (count <= 0)
                {
                    FailWithoutCompleting(
                        offeringId,
                        $"Root offering '{offeringId}' has no recurring emergence definition.");
                    return;
                }

                if (!ValidateAuthoredConfiguration(out string error))
                {
                    FailWithoutCompleting(offeringId, error);
                    return;
                }

                emergenceRoutine = StartCoroutine(
                    RunRecurringEmergence(offeringId, count));
            }
            finally
            {
                reconcileInProgress = false;
            }
        }

        private IEnumerator RunRecurringEmergence(
            string offeringId,
            int enemyCount)
        {
            int difficulty = GetDifficultyForOffering(offeringId);
            if (difficulty <= 0)
            {
                FailWithoutCompleting(
                    offeringId,
                    $"Root offering '{offeringId}' has no recurring enemy difficulty definition.");
                yield break;
            }

            generation++;
            spawnFailed = false;
            activeOfferingId = offeringId;
            expectedEnemyCount = enemyCount;
            liveMarkers.Clear();
            ownedEnemies.Clear();

            prologueDirector.PublishCampaignObjective(
                $"The Root Tree is stirring. Defeat {enemyCount} emerging enemies.",
                0,
                enemyCount);
            FarmPrologueEventUtility.Invoke(
                emergenceStarted,
                offeringId,
                this);

            for (int index = 0; index < enemyCount; index++)
            {
                if (shuttingDown)
                    yield break;

                GameObject prefab = enemyPrefabs[
                    UnityEngine.Random.Range(0, enemyPrefabs.Length)];
                Transform spawnPoint = spawnPoints[index % spawnPoints.Length];
                GameObject enemy = null;

                try
                {
                    if (!CampaignSafetyEnemyRuntimeAdapter
                            .TryResolveGroundedSpawnPosition(
                                prefab,
                                spawnPoint.position,
                                GroundedSpawnNavMeshSampleRadius,
                                out NavMeshHit groundedPosition,
                                out string groundError))
                    {
                        throw new InvalidOperationException(
                            $"Farm emergence point '{spawnPoint.name}' was " +
                            $"rejected by grounded NavMesh validation. {groundError}");
                    }

                    if (!IsInsideSpawnContainment(groundedPosition.position))
                    {
                        throw new InvalidOperationException(
                            $"Farm emergence point '{spawnPoint.name}' resolved outside the fenced Farm spawn area.");
                    }

                    enemy = Instantiate(
                        prefab,
                        groundedPosition.position,
                        spawnPoint.rotation);
                    enemy.name =
                        $"Farm Emergence {offeringId} Enemy {index + 1:00}";
                    PrepareOwnedRecurringEnemy(
                        enemy,
                        groundedPosition,
                        difficulty);
                    FarmEmergenceEnemyMarker marker =
                        enemy.GetComponent<FarmEmergenceEnemyMarker>() ??
                        enemy.AddComponent<FarmEmergenceEnemyMarker>();
                    marker.Initialize(this, generation);
                    liveMarkers.Add(marker);
                    ownedEnemies.Add(enemy);
                    emergencePresenter.PresentExternalEnemy(enemy, this);
                }
                catch (Exception exception)
                {
                    if (enemy != null)
                    {
                        Destroy(enemy);
                    }


                    spawnFailed = true;
                    break;
                }

                if (index + 1 < enemyCount)
                {
                    yield return new WaitForSeconds(spawnIntervalSeconds);
                }
            }

            if (spawnFailed)
            {
                consecutiveSpawnFailures = Mathf.Min(
                    consecutiveSpawnFailures + 1,
                    10);
                float retryDelay = Mathf.Min(
                    completionRetryMaximumSeconds,
                    completionRetrySeconds * Mathf.Pow(
                        2f,
                        Mathf.Max(0, consecutiveSpawnFailures - 1)));
                FailWithoutCompleting(
                    offeringId,
                    "A recurring Farm enemy could not be spawned. The durable emergence remains pending and will retry safely.");
                DestroyOwnedEnemiesWithoutCompletion();
                ResetRuntimeState();
                yield return new WaitForSecondsRealtime(retryDelay);
                if (shuttingDown || !isActiveAndEnabled)
                    yield break;

                emergenceRoutine = null;
                ReconcileDurableObligation();
                yield break;
            }

            consecutiveSpawnFailures = 0;

            while (!shuttingDown && liveMarkers.Count > 0)
            {
                int defeated = Mathf.Max(
                    0,
                    enemyCount - liveMarkers.Count);
                prologueDirector.PublishCampaignObjective(
                    "Defeat the cursed enemies emerging from the Root Tree.",
                    defeated,
                    enemyCount);
                yield return null;
            }

            if (shuttingDown)
                yield break;

            int retry = 0;
            while (!shuttingDown)
            {
                CampaignStateService state = ResolveState();
                if (state != null &&
                    state.TryCompleteFarmEmergence(offeringId))
                {
                    emergenceRoutine = null;
                    ResetRuntimeState();
                    consecutiveSpawnFailures = 0;
                    FarmPrologueEventUtility.Invoke(
                        emergenceCompleted,
                        offeringId,
                        this);
                    prologueDirector.PublishCampaignObjective(
                        "The Farm is safe. The Root Tree is quiet again.",
                        1,
                        1);
                    ReconcileDurableObligation();
                    yield break;
                }

                float delay = Mathf.Min(
                    completionRetryMaximumSeconds,
                    completionRetrySeconds * Mathf.Pow(2f, retry));
                retry = Mathf.Min(retry + 1, 10);
                prologueDirector.PublishCampaignObjective(
                    "The emergence is cleared, but saving is still pending.",
                    0,
                    1);
                yield return new WaitForSecondsRealtime(delay);
            }
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

        private void FailWithoutCompleting(string offeringId, string reason)
        {

            prologueDirector?.PublishCampaignObjective(reason, 0, 1);
            FarmPrologueEventUtility.Invoke(
                emergenceFailed,
                offeringId,
                this);
        }

        private void EnsurePrologueReveal()
        {
            CampaignStateService state = ResolveState();
            if (state == null || state.PrologueCursedObjectRevealed ||
                revealRetryRoutine != null || !isActiveAndEnabled)
            {
                return;
            }

            revealRetryRoutine = StartCoroutine(RevealPrologueObjectWithRetry());
        }

        private IEnumerator RevealPrologueObjectWithRetry()
        {
            int retry = 0;
            while (!shuttingDown && isActiveAndEnabled &&
                   ResolveDirector()?.CurrentPhase ==
                       FarmProloguePhase.AwaitingOffering)
            {
                CampaignStateService state = ResolveState();
                if (state != null &&
                    (state.PrologueCursedObjectRevealed ||
                     state.TryRevealPrologueCursedObject()))
                {
                    revealRetryRoutine = null;
                    ReconcileDurableObligation();
                    yield break;
                }

                prologueDirector?.PublishCampaignObjective(
                    "The cursed object surfaced, but saving its discovery is still pending.",
                    0,
                    1);
                float delay = Mathf.Min(
                    completionRetryMaximumSeconds,
                    completionRetrySeconds * Mathf.Pow(2f, retry));
                retry = Mathf.Min(retry + 1, 10);
                yield return new WaitForSecondsRealtime(delay);
            }

            revealRetryRoutine = null;
        }

        private void EnsureBeginRetry(string expectedOfferingId)
        {
            if (beginRetryRoutine != null || !isActiveAndEnabled ||
                shuttingDown || string.IsNullOrEmpty(expectedOfferingId))
            {
                return;
            }

            beginRetryRoutine = StartCoroutine(
                RetryBeginningEmergence(expectedOfferingId));
        }

        private IEnumerator RetryBeginningEmergence(
            string expectedOfferingId)
        {
            int retry = 0;
            while (!shuttingDown && isActiveAndEnabled)
            {
                CampaignStateService state = ResolveState();
                if (state == null)
                {
                    beginRetryRoutine = null;
                    yield break;
                }

                string activeId =
                    state.ActiveFarmEmergenceOfferingId ?? string.Empty;
                string pendingId =
                    state.NextPendingFarmEmergenceOfferingId ?? string.Empty;
                if (activeId.Length > 0)
                {
                    if (string.Equals(
                            activeId,
                            expectedOfferingId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            activeId,
                            CampaignRootOfferingIds.PrologueCursedObject,
                            StringComparison.Ordinal))
                    {
                        FarmPrologueDirector director = ResolveDirector();
                        if (director != null &&
                            director.CurrentPhase !=
                                FarmProloguePhase.AwaitingOffering &&
                            director.CurrentPhase != FarmProloguePhase.Rumble)
                        {
                            beginRetryRoutine = null;
                            ReconcileDurableObligation();
                            yield break;
                        }

                        director?.PublishCampaignObjective(
                            "The cursed-object emergence is waiting for its authored combat systems.",
                            0,
                            1);
                        float activeRetryDelay = Mathf.Min(
                            completionRetryMaximumSeconds,
                            completionRetrySeconds * Mathf.Pow(2f, retry));
                        retry = Mathf.Min(retry + 1, 10);
                        yield return new WaitForSecondsRealtime(
                            activeRetryDelay);

                        state = ResolveState();
                        director = ResolveDirector();
                        string currentActiveId =
                            state?.ActiveFarmEmergenceOfferingId ??
                            string.Empty;
                        if (!string.Equals(
                                currentActiveId,
                                expectedOfferingId,
                                StringComparison.Ordinal) ||
                            (director != null &&
                             director.CurrentPhase !=
                                 FarmProloguePhase.AwaitingOffering &&
                             director.CurrentPhase !=
                                 FarmProloguePhase.Rumble))
                        {
                            beginRetryRoutine = null;
                            ReconcileDurableObligation();
                            yield break;
                        }

                        if (director != null &&
                            director.BeginTreeFedEmergence())
                        {
                            beginRetryRoutine = null;
                            yield break;
                        }

                        continue;
                    }

                    beginRetryRoutine = null;
                    ReconcileDurableObligation();
                    yield break;
                }

                if (!string.Equals(
                        pendingId,
                        expectedOfferingId,
                        StringComparison.Ordinal))
                {
                    beginRetryRoutine = null;
                    ReconcileDurableObligation();
                    yield break;
                }

                prologueDirector?.PublishCampaignObjective(
                    "The offering is accepted, but saving its emergence is still pending.",
                    0,
                    1);
                float delay = Mathf.Min(
                    completionRetryMaximumSeconds,
                    completionRetrySeconds * Mathf.Pow(2f, retry));
                retry = Mathf.Min(retry + 1, 10);
                yield return new WaitForSecondsRealtime(delay);

                state = ResolveState();
                if (state != null &&
                    state.TryBeginNextFarmEmergence(out string begunId) &&
                    string.Equals(
                        begunId,
                        expectedOfferingId,
                        StringComparison.Ordinal))
                {
                    beginRetryRoutine = null;
                    ReconcileDurableObligation();
                    yield break;
                }
            }

            beginRetryRoutine = null;
        }

        private void DestroyOwnedEnemiesWithoutCompletion()
        {
            foreach (GameObject enemy in ownedEnemies)
            {
                if (enemy != null)
                {
                    enemy.SetActive(false);
                    Destroy(enemy);
                }
            }

            ownedEnemies.Clear();
            liveMarkers.Clear();
        }

        private static void PrepareOwnedRecurringEnemy(
            GameObject enemy,
            NavMeshHit groundedPosition,
            int difficulty)
        {
            if (difficulty < 1 || difficulty > 5)
            {
                throw new InvalidOperationException(
                    "A recurring Farm enemy requires a Name Stone difficulty from one through five.");
            }

            if (!CampaignSafetyEnemyRuntimeAdapter
                    .TryPrepareGroundedSpawn(
                        enemy,
                        groundedPosition,
                        out Component controller,
                        out string placementError))
            {
                throw new InvalidOperationException(placementError);
            }

            if (!CampaignSafetyEnemyRuntimeAdapter.TryInitialize(
                    controller,
                    difficulty,
                    out string initializationError))
            {
                throw new InvalidOperationException(initializationError);
            }
        }

        private void ResetRuntimeState()
        {
            liveMarkers.Clear();
            ownedEnemies.Clear();
            activeOfferingId = string.Empty;
            expectedEnemyCount = 0;
            spawnFailed = false;
        }

        private void QueueStateReconcile()
        {
            if (!isActiveAndEnabled || shuttingDown ||
                stateReconcileRoutine != null)
            {
                return;
            }

            stateReconcileRoutine = StartCoroutine(ReconcileAfterSceneStart());
        }

        private void RestartForReplacedCampaignState()
        {
            if (!isActiveAndEnabled || shuttingDown)
                return;

            if (emergenceRoutine != null)
            {
                StopCoroutine(emergenceRoutine);
                emergenceRoutine = null;
            }

            if (revealRetryRoutine != null)
            {
                StopCoroutine(revealRetryRoutine);
                revealRetryRoutine = null;
            }

            if (stateReconcileRoutine != null)
            {
                StopCoroutine(stateReconcileRoutine);
                stateReconcileRoutine = null;
            }

            if (beginRetryRoutine != null)
            {
                StopCoroutine(beginRetryRoutine);
                beginRetryRoutine = null;
            }

            generation++;
            DestroyOwnedEnemiesWithoutCompletion();
            ResetRuntimeState();
            consecutiveSpawnFailures = 0;
            QueueStateReconcile();
        }

        private void ResolveReferences()
        {
            CampaignStateService persistentState =
                CampaignStateService.Instance;
            if (persistentState != null && campaignState != persistentState)
            {
                campaignState = persistentState;
            }
            else if (campaignState == null)
            {
                campaignState = persistentState;
            }

            if (prologueDirector == null)
            {
                prologueDirector = FindAnyObjectByType<FarmPrologueDirector>();
            }

            if (emergencePresenter == null)
            {
                emergencePresenter =
                    FindAnyObjectByType<FarmEnemyEmergencePresenter>();
            }
        }

        private CampaignStateService ResolveState()
        {
            CampaignStateService persistentState =
                CampaignStateService.Instance;
            if (persistentState != null && campaignState != persistentState)
            {
                campaignState = persistentState;
            }
            else if (campaignState == null)
            {
                campaignState = persistentState;
            }

            return campaignState;
        }

        private FarmPrologueDirector ResolveDirector()
        {
            if (prologueDirector == null)
            {
                prologueDirector = FindAnyObjectByType<FarmPrologueDirector>();
            }

            return prologueDirector;
        }

        private void Bind()
        {
            CampaignStateService state = ResolveState();
            if (subscribedState != state)
            {
                if (subscribedState != null)
                {
                    subscribedState.ProgressChanged -= HandleProgressChanged;
                    subscribedState.ProgressLoaded -= HandleProgressLoaded;
                    subscribedState.NewGameStarted -= HandleNewGameStarted;
                }

                subscribedState = state;
                if (subscribedState != null)
                {
                    subscribedState.ProgressChanged += HandleProgressChanged;
                    subscribedState.ProgressLoaded += HandleProgressLoaded;
                    subscribedState.NewGameStarted += HandleNewGameStarted;
                }
            }

            FarmPrologueDirector director = ResolveDirector();
            if (subscribedDirector == director)
                return;

            if (subscribedDirector != null)
            {
                subscribedDirector.PhaseChanged -= HandlePhaseChanged;
            }

            subscribedDirector = director;
            if (subscribedDirector != null)
            {
                subscribedDirector.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void Unbind()
        {
            if (subscribedState != null)
            {
                subscribedState.ProgressChanged -= HandleProgressChanged;
                subscribedState.ProgressLoaded -= HandleProgressLoaded;
                subscribedState.NewGameStarted -= HandleNewGameStarted;
                subscribedState = null;
            }

            if (subscribedDirector != null)
            {
                subscribedDirector.PhaseChanged -= HandlePhaseChanged;
                subscribedDirector = null;
            }
        }

        private static int IndexOfNameStoneOffering(string offeringId)
        {
            if (string.Equals(
                    offeringId,
                    CampaignNameStoneIds.Esther,
                    StringComparison.Ordinal))
                return 0;
            if (string.Equals(
                    offeringId,
                    CampaignNameStoneIds.Ruth,
                    StringComparison.Ordinal))
                return 1;
            if (string.Equals(
                    offeringId,
                    CampaignNameStoneIds.Naomi,
                    StringComparison.Ordinal))
                return 2;
            if (string.Equals(
                    offeringId,
                    CampaignNameStoneIds.Nell,
                    StringComparison.Ordinal))
                return 3;

            return -1;
        }
    }
}
