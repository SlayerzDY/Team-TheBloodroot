using System;
using System.Collections;
using System.Collections.Generic;
using Bloodroot.Campaign;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Bloodroot.Features.AlphaEnemies
{
    [Serializable]
    public sealed class WitchEncounterWave
    {
        [SerializeField, Min(0f)] private float activationDelay;
        [SerializeField] private WitchController[] witches;
        [SerializeField] private GameObject[] supportObjects;

        public float ActivationDelay => Mathf.Max(0f, activationDelay);
        public WitchController[] Witches => witches;
        public GameObject[] SupportObjects => supportObjects;

        public void Configure(
            float delaySeconds,
            WitchController witch,
            GameObject[] authoredSupportObjects = null)
        {
            activationDelay = Mathf.Max(0f, delaySeconds);
            witches = witch == null
                ? Array.Empty<WitchController>()
                : new[] { witch };
            supportObjects = authoredSupportObjects ??
                             Array.Empty<GameObject>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class WitchEncounterDirector : MonoBehaviour
    {
        [Header("Scene-Authored Encounter")]
        [SerializeField] private WitchEncounterWave[] waves;
        [SerializeField] private WitchDefenseAltar defenseAltar;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private bool witchesTargetAltar = true;
        [SerializeField] private bool resolvePlayerFromGameManager = true;
        [SerializeField] private GameObject heartrootExtractionRoot;
        [SerializeField] private CampaignHeartrootFinaleBridge
            heartrootFinaleBridge;
        [SerializeField] private CampaignStateService stateService;
        [SerializeField] private bool deactivateWitchesUntilStarted = true;
        [SerializeField] private bool deactivateWitchesOnFailure;
        [SerializeField] private bool hideExtractionAfterCompletion = true;

        [Header("Failure Recovery")]
        [SerializeField] private bool showLoseMenuOnDefenseFailure = true;
        [SerializeField] private bool reloadSceneAfterFailureRespawn = true;

        [Header("Encounter Scaling")]
        [SerializeField, Min(1)] private int difficultyLevel = 1;
        [SerializeField, Min(0.01f)] private float healthScalar = 1f;
        [SerializeField, Min(0f)] private float damageScalar = 1f;
        [SerializeField, Min(0.01f)] private float speedScalar = 1f;

        [Header("Authored Events")]
        [SerializeField] private WitchEncounterStateEvent onStateChanged = new WitchEncounterStateEvent();
        [SerializeField] private WitchEncounterDirectorEvent onEncounterStarted = new WitchEncounterDirectorEvent();
        [SerializeField] private WitchControllerEvent onWitchDefeated = new WitchControllerEvent();
        [SerializeField] private WitchEncounterDirectorEvent onDefenseSucceeded = new WitchEncounterDirectorEvent();
        [SerializeField] private WitchEncounterDirectorEvent onDefenseFailed = new WitchEncounterDirectorEvent();
        [SerializeField] private WitchEncounterDirectorEvent onHeartrootExtracted = new WitchEncounterDirectorEvent();
        [SerializeField] private GameObjectEvent onExtractionRequesterAccepted = new GameObjectEvent();
        [SerializeField] private UnityEvent onExtractionRequesterRejected = new UnityEvent();

        private readonly HashSet<WitchController> activeWitches = new HashSet<WitchController>();
        private WitchEncounterState state = WitchEncounterState.Idle;
        private int currentWaveIndex = -1;
        private Coroutine waveRoutine;
        private Coroutine failureReloadRoutine;
        private bool altarSubscribed;
        private global::gameManager failureGameManager;

        public event Action<WitchEncounterState> StateChanged;
        public event Action<WitchEncounterDirector> EncounterStarted;
        public event Action<WitchController> WitchDefeated;
        public event Action<WitchEncounterDirector> DefenseSucceeded;
        public event Action<WitchEncounterDirector> DefenseFailed;
        public event Action<WitchEncounterDirector> HeartrootExtracted;

        public WitchEncounterState State => state;
        public int CurrentWaveIndex => currentWaveIndex;
        public int ActiveWitchCount => activeWitches.Count;
        public bool IsDefenseActive => state == WitchEncounterState.Defending;
        public bool CanExtractHeartroot => state == WitchEncounterState.AwaitingExtraction;
        public int ConfiguredWaveCount => waves?.Length ?? 0;
        public int DurableDefeatedWitchCount =>
            ResolveStateService()?.Current.DefeatedWitchCount ?? 0;

        private void Awake()
        {
            DisableAltarMechanic();
            SetExtractionActive(false);
            if (deactivateWitchesUntilStarted)
            {
                SetConfiguredWitchesActive(false);
            }
        }

        private void OnDestroy()
        {
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }

            if (failureReloadRoutine != null)
            {
                StopCoroutine(failureReloadRoutine);
                failureReloadRoutine = null;
            }

            UnsubscribeAltar();
            UnsubscribeFailureGameManager();
            foreach (WitchController witch in activeWitches)
            {
                if (witch != null)
                {
                    witch.Died -= HandleWitchDied;
                }
            }

            activeWitches.Clear();
        }

        public bool BeginEncounter()
        {
            if (state != WitchEncounterState.Idle)
            {
                return false;
            }

            if (!ValidateSequentialWaveContract(out string waveError))
            {

                return false;
            }

            CampaignStateService campaignState = ResolveStateService();
            CampaignProgressSnapshot progress = campaignState != null
                ? campaignState.Current
                : default;
            if (campaignState == null || !progress.HollowVeilCrossed ||
                progress.HeartrootCarried || progress.HeartrootBurned ||
                progress.CampaignCompleted)
            {

                return false;
            }

            ResolvePlayerTargetIfNeeded();
            DisableAltarMechanic();
            if (playerTarget == null)
            {

                return false;
            }

            SetConfiguredWitchesActive(false);
            SetAllWaveSupportObjectsActive(false);
            SetExtractionActive(false);
            int durableDefeatedCount = Mathf.Clamp(
                progress.DefeatedWitchCount,
                0,
                3);
            currentWaveIndex = durableDefeatedCount - 1;

            AlphaEnemyEventUtility.Invoke(onEncounterStarted, this, this, nameof(onEncounterStarted));
            AlphaEnemyEventUtility.Invoke(EncounterStarted, this, this, nameof(EncounterStarted));
            SetState(WitchEncounterState.Defending);
            if (durableDefeatedCount == 3)
            {
                if (!progress.HeartrootExposed)
                {

                    return false;
                }

                CompleteDefense();
                return state == WitchEncounterState.Completed;
            }

            ScheduleNextWave();
            return true;
        }

        /// <summary>
        /// Parameterless UnityEvent adapter for authored mission signals.
        /// Callers that need acceptance feedback should use BeginEncounter().
        /// </summary>
        public void StartEncounter()
        {
            BeginEncounter();
        }

        public bool TryCompleteHeartrootExtraction(GameObject requester = null)
        {
            if (state != WitchEncounterState.AwaitingExtraction)
            {
                AlphaEnemyEventUtility.Invoke(onExtractionRequesterRejected, this, nameof(onExtractionRequesterRejected));
                return false;
            }

            CampaignHeartrootFinaleBridge finale =
                ResolveHeartrootFinaleBridge();
            if (finale == null || !finale.TryRecoverHeartroot(requester))
            {
                AlphaEnemyEventUtility.Invoke(onExtractionRequesterRejected, this, nameof(onExtractionRequesterRejected));
                return false;
            }

            CampaignStateService campaignState = ResolveStateService();
            if (campaignState == null ||
                !campaignState.Current.HeartrootCarried ||
                !campaignState.Current.HollowCompleted)
            {
                AlphaEnemyEventUtility.Invoke(onExtractionRequesterRejected, this, nameof(onExtractionRequesterRejected));
                return false;
            }

            SetState(WitchEncounterState.Completed);
            if (hideExtractionAfterCompletion)
            {
                SetExtractionActive(false);
            }

            AlphaEnemyEventUtility.Invoke(onExtractionRequesterAccepted, requester, this, nameof(onExtractionRequesterAccepted));
            AlphaEnemyEventUtility.Invoke(onHeartrootExtracted, this, this, nameof(onHeartrootExtracted));
            AlphaEnemyEventUtility.Invoke(HeartrootExtracted, this, this, nameof(HeartrootExtracted));
            return true;
        }

        public void FailEncounter()
        {
            if (state != WitchEncounterState.Defending)
            {
                return;
            }

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }

            foreach (WitchController witch in activeWitches)
            {
                if (witch == null)
                {
                    continue;
                }

                witch.Died -= HandleWitchDied;
                witch.SetCombatEnabled(false);
                if (deactivateWitchesOnFailure)
                {
                    witch.gameObject.SetActive(false);
                }
            }

            activeWitches.Clear();
            SetAllWaveSupportObjectsActive(false);

            SetExtractionActive(false);
            SetState(WitchEncounterState.Failed);
            AlphaEnemyEventUtility.Invoke(onDefenseFailed, this, this, nameof(onDefenseFailed));
            AlphaEnemyEventUtility.Invoke(DefenseFailed, this, this, nameof(DefenseFailed));
        }

        /// <summary>
        /// Restarts the authored scene after a failed altar defense. A scene
        /// reload is intentional: earlier waves may already have destroyed
        /// their scene-authored witches and emitted loot, so trying to reset
        /// only the remaining objects would create an incomplete retry.
        /// </summary>
        public bool RetryFailedEncounter()
        {
            if (state != WitchEncounterState.Failed || failureReloadRoutine != null)
            {
                return false;
            }

            failureReloadRoutine = StartCoroutine(ReloadFailedEncounterScene());
            return true;
        }

        private void ScheduleNextWave()
        {
            if (state != WitchEncounterState.Defending || waveRoutine != null)
            {
                return;
            }

            int nextWaveIndex = currentWaveIndex + 1;
            if (waves == null || nextWaveIndex >= waves.Length)
            {
                CompleteDefense();
                return;
            }

            waveRoutine = StartCoroutine(ActivateWaveAfterDelay(nextWaveIndex));
        }

        private IEnumerator ActivateWaveAfterDelay(int waveIndex)
        {
            WitchEncounterWave wave = waves[waveIndex];
            float delay = wave == null ? 0f : wave.ActivationDelay;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            waveRoutine = null;
            if (state != WitchEncounterState.Defending)
            {
                yield break;
            }

            currentWaveIndex = waveIndex;
            Transform encounterTarget = ResolveEncounterTarget();
            SetWaveSupportObjectsActive(wave, true);
            WitchController[] configuredWitches = wave?.Witches;
            if (configuredWitches != null)
            {
                foreach (WitchController witch in configuredWitches)
                {
                    if (witch == null || activeWitches.Contains(witch))
                    {
                        continue;
                    }

                    witch.gameObject.SetActive(true);
                    witch.PrepareForEncounter(encounterTarget: encounterTarget,
                        level: difficultyLevel,
                        encounterHealthScalar: healthScalar,
                        encounterDamageScalar: damageScalar,
                        encounterSpeedScalar: speedScalar);
                    witch.SetSecondaryAttackTarget(
                        ResolveSecondaryEncounterTarget(encounterTarget));
                    witch.Died -= HandleWitchDied;
                    witch.Died += HandleWitchDied;
                    if (!witch.IsDead)
                    {
                        activeWitches.Add(witch);
                    }
                }
            }

            if (activeWitches.Count == 0)
            {
                SetWaveSupportObjectsActive(wave, false);
                ScheduleNextWave();
            }
        }

        private void HandleWitchDied(WitchController witch)
        {
            if (witch == null)
            {
                return;
            }

            witch.Died -= HandleWitchDied;
            if (!activeWitches.Contains(witch))
            {
                return;
            }

            CampaignStateService campaignState = ResolveStateService();
            if (campaignState == null ||
                !campaignState.TryRecordHollowWitchDefeated(
                    currentWaveIndex))
            {

                FailEncounter();
                return;
            }

            activeWitches.Remove(witch);

            if (currentWaveIndex == 2)
            {
                PositionHeartrootDrop(witch.transform.position);
            }

            bool finalWitchDefeated = currentWaveIndex == 2 &&
                                      activeWitches.Count == 0;
            if (finalWitchDefeated)
            {
                SetWaveSupportObjectsActive(
                    waves[currentWaveIndex],
                    false);
                CompleteDefense();
            }

            AlphaEnemyEventUtility.Invoke(onWitchDefeated, witch, this, nameof(onWitchDefeated));
            AlphaEnemyEventUtility.Invoke(WitchDefeated, witch, this, nameof(WitchDefeated));
            if (state == WitchEncounterState.Defending && activeWitches.Count == 0)
            {
                if (waves != null && currentWaveIndex >= 0 &&
                    currentWaveIndex < waves.Length)
                {
                    SetWaveSupportObjectsActive(
                        waves[currentWaveIndex],
                        false);
                }

                ScheduleNextWave();
            }
        }

        private void CompleteDefense()
        {
            if (state != WitchEncounterState.Defending)
            {
                return;
            }

            CampaignStateService campaignState = ResolveStateService();
            CampaignProgressSnapshot progress = campaignState != null
                ? campaignState.Current
                : default;
            if (campaignState == null ||
                progress.DefeatedWitchCount != 3 ||
                !progress.HeartrootExposed ||
                !campaignState.TryCompleteCampaignFromFinalWitch())
            {

                FailEncounter();
                return;
            }

            SetState(WitchEncounterState.Completed);
            SetAllWaveSupportObjectsActive(false);
            SetExtractionActive(true);
            DisableHeartrootInteraction();
            AlphaEnemyEventUtility.Invoke(onDefenseSucceeded, this, this, nameof(onDefenseSucceeded));
            AlphaEnemyEventUtility.Invoke(DefenseSucceeded, this, this, nameof(DefenseSucceeded));

            global::gameManager manager = global::gameManager.instance;
            if (manager == null)
            {

                return;
            }

            manager.youWin();
        }

        private Transform ResolveEncounterTarget()
        {
            ResolvePlayerTargetIfNeeded();
            return playerTarget;
        }

        private Transform ResolveSecondaryEncounterTarget(
            Transform primaryTarget)
        {
            return null;
        }

        private void DisableAltarMechanic()
        {
            witchesTargetAltar = false;
            UnsubscribeAltar();
            if (defenseAltar != null)
            {
                defenseAltar.gameObject.SetActive(false);
            }
        }

        private void DisableHeartrootInteraction()
        {
            if (heartrootExtractionRoot == null)
            {
                return;
            }

            WitchHeartrootExtractionInteractable interactable =
                heartrootExtractionRoot.GetComponentInChildren<
                    WitchHeartrootExtractionInteractable>(true);
            if (interactable != null)
            {
                interactable.enabled = false;
            }

            Collider[] colliders =
                heartrootExtractionRoot.GetComponentsInChildren<Collider>(
                    true);
            foreach (Collider collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        private void PositionHeartrootDrop(Vector3 witchDeathPosition)
        {
            if (heartrootExtractionRoot == null)
            {
                return;
            }

            Vector3 dropPosition = heartrootExtractionRoot.transform.position;
            dropPosition.x = witchDeathPosition.x;
            dropPosition.z = witchDeathPosition.z;
            heartrootExtractionRoot.transform.position = dropPosition;
        }

        private void ResolvePlayerTargetIfNeeded()
        {
            if (playerTarget != null || !resolvePlayerFromGameManager || global::gameManager.instance == null ||
                global::gameManager.instance.player == null)
            {
                return;
            }

            playerTarget = global::gameManager.instance.player.transform;
        }

        private void SubscribeAltar()
        {
            if (altarSubscribed || defenseAltar == null)
            {
                return;
            }

            defenseAltar.Destroyed += HandleAltarDestroyed;
            altarSubscribed = true;
        }

        private void UnsubscribeAltar()
        {
            if (!altarSubscribed)
            {
                return;
            }

            if (defenseAltar != null)
            {
                defenseAltar.Destroyed -= HandleAltarDestroyed;
            }

            altarSubscribed = false;
        }

        private void HandleAltarDestroyed(WitchDefenseAltar altar)
        {
            FailEncounter();
        }

        private void PresentDefenseFailure()
        {
            if (!showLoseMenuOnDefenseFailure)
            {
                return;
            }

            global::gameManager manager = global::gameManager.instance;
            if (manager == null)
            {

                return;
            }

            SubscribeFailureGameManager(manager);
            manager.youLose();
        }

        private void SubscribeFailureGameManager(global::gameManager manager)
        {
            if (failureGameManager == manager)
            {
                return;
            }

            UnsubscribeFailureGameManager();
            failureGameManager = manager;
            failureGameManager.PlayerRespawned += HandleFailureRespawned;
        }

        private void UnsubscribeFailureGameManager()
        {
            if (failureGameManager == null)
            {
                return;
            }

            failureGameManager.PlayerRespawned -= HandleFailureRespawned;
            failureGameManager = null;
        }

        private void HandleFailureRespawned()
        {
            if (reloadSceneAfterFailureRespawn)
            {
                RetryFailedEncounter();
            }
        }

        private IEnumerator ReloadFailedEncounterScene()
        {
            // buttonFunctions notifies listeners before it unpauses the old
            // manager. Restore real time here, then wait one frame so the
            // button callback can finish before the replacement scene loads.
            Time.timeScale = 1f;
            yield return null;

            Scene scene = gameObject.scene;
            failureReloadRoutine = null;
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
            {

                yield break;
            }

            SceneManager.LoadScene(scene.name);
        }

        public bool ValidateRuntimeContract(out string error)
        {
            error = string.Empty;
            if (!ValidateSequentialWaveContract(out error))
                return false;

            if (heartrootExtractionRoot == null)
            {
                error =
                    "The authored Heartroot extraction root is missing.";
                return false;
            }

            CampaignHeartrootFinaleBridge finale =
                ResolveHeartrootFinaleBridge();
            if (finale == null ||
                !finale.ValidateRuntimeContract(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "The Heartroot finale bridge is missing or invalid."
                    : error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool ValidateSequentialWaveContract(out string error)
        {
            if (!deactivateWitchesUntilStarted)
            {
                error =
                    "All three Hollow witches must remain inactive until the durable thorn-veil crossing starts the encounter.";
                return false;
            }

            if (waves == null || waves.Length != 3)
            {
                error =
                    "The Hollow encounter requires exactly three sequential waves.";
                return false;
            }

            var uniqueWitches = new HashSet<WitchController>();
            var uniqueObjects = new HashSet<GameObject>();
            var uniqueVariants = new HashSet<WitchVariant>();
            WitchVariant[] requiredOrder =
            {
                WitchVariant.ShieldBearer,
                WitchVariant.Summoner,
                WitchVariant.Matriarch
            };
            for (int index = 0; index < waves.Length; index++)
            {
                WitchEncounterWave wave = waves[index];
                WitchController[] configuredWitches = wave?.Witches;
                if (wave == null || configuredWitches == null ||
                    configuredWitches.Length != 1 ||
                    configuredWitches[0] == null)
                {
                    error =
                        $"Witch wave {index + 1} must contain exactly one scene-authored witch.";
                    return false;
                }

                WitchController witch = configuredWitches[0];
                if (!uniqueWitches.Add(witch) ||
                    !uniqueObjects.Add(witch.gameObject) ||
                    !uniqueVariants.Add(witch.Variant))
                {
                    error =
                        $"Witch wave {index + 1} duplicates a witch object or variant.";
                    return false;
                }

                if (witch.Variant != requiredOrder[index])
                {
                    error =
                        $"Witch wave {index + 1} must be the {requiredOrder[index]} variant.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(
            WitchEncounterWave[] sequentialWaves,
            CampaignStateService campaignState,
            CampaignHeartrootFinaleBridge finaleBridge,
            GameObject extractionRoot,
            WitchDefenseAltar authoredDefenseAltar = null,
            Transform authoredPlayerTarget = null)
        {
            waves = sequentialWaves ?? Array.Empty<WitchEncounterWave>();
            stateService = campaignState;
            heartrootFinaleBridge = finaleBridge;
            heartrootExtractionRoot = extractionRoot;
            defenseAltar = authoredDefenseAltar;
            playerTarget = authoredPlayerTarget;
            deactivateWitchesUntilStarted = true;
        }

        private CampaignStateService ResolveStateService()
        {
            CampaignStateService persistent =
                CampaignStateService.Instance;
            if (persistent != null && persistent != stateService)
                stateService = persistent;

            return stateService;
        }

        private CampaignHeartrootFinaleBridge
            ResolveHeartrootFinaleBridge()
        {
            if (heartrootFinaleBridge == null)
            {
                heartrootFinaleBridge =
                    GetComponentInChildren<CampaignHeartrootFinaleBridge>(
                        true);
            }

            return heartrootFinaleBridge;
        }

        private void SetConfiguredWitchesActive(bool active)
        {
            if (waves == null)
            {
                return;
            }

            HashSet<WitchController> visited = new HashSet<WitchController>();
            HashSet<GameObject> visitedSupportObjects = new HashSet<GameObject>();
            foreach (WitchEncounterWave wave in waves)
            {
                WitchController[] configuredWitches = wave?.Witches;
                if (configuredWitches != null)
                {
                    foreach (WitchController witch in configuredWitches)
                    {
                        if (witch != null && visited.Add(witch))
                        {
                            witch.gameObject.SetActive(active);
                        }
                    }
                }

                GameObject[] supportObjects = wave?.SupportObjects;
                if (supportObjects == null)
                {
                    continue;
                }

                foreach (GameObject supportObject in supportObjects)
                {
                    if (supportObject != null &&
                        visitedSupportObjects.Add(supportObject))
                    {
                        supportObject.SetActive(active);
                    }
                }
            }
        }

        private void SetAllWaveSupportObjectsActive(bool active)
        {
            if (waves == null)
            {
                return;
            }

            HashSet<GameObject> visited = new HashSet<GameObject>();
            foreach (WitchEncounterWave wave in waves)
            {
                GameObject[] supportObjects = wave?.SupportObjects;
                if (supportObjects == null)
                {
                    continue;
                }

                foreach (GameObject supportObject in supportObjects)
                {
                    if (supportObject != null && visited.Add(supportObject))
                    {
                        supportObject.SetActive(active);
                    }
                }
            }
        }

        private static void SetWaveSupportObjectsActive(
            WitchEncounterWave wave,
            bool active)
        {
            GameObject[] supportObjects = wave?.SupportObjects;
            if (supportObjects == null)
            {
                return;
            }

            foreach (GameObject supportObject in supportObjects)
            {
                if (supportObject != null)
                {
                    supportObject.SetActive(active);
                }
            }
        }

        private void SetExtractionActive(bool active)
        {
            if (heartrootExtractionRoot != null)
            {
                heartrootExtractionRoot.SetActive(active);
            }
        }

        private void SetState(WitchEncounterState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
            AlphaEnemyEventUtility.Invoke(onStateChanged, state, this, nameof(onStateChanged));
            AlphaEnemyEventUtility.Invoke(StateChanged, state, this, nameof(StateChanged));
        }

        private void OnValidate()
        {
            difficultyLevel = Mathf.Max(1, difficultyLevel);
            healthScalar = Mathf.Max(0.01f, healthScalar);
            damageScalar = Mathf.Max(0f, damageScalar);
            speedScalar = Mathf.Max(0.01f, speedScalar);
        }
    }
}
