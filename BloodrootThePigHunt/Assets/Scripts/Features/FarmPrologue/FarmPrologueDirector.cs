using System;
using System.Collections;
using System.Collections.Generic;
using Bloodroot.Campaign;
using Bloodroot.Features.BloodMoon;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Bloodroot.Features.FarmPrologue
{
    // Campaign services restore durable state at -1000 through -800. Run
    // after those services, but before Safety's default-order player Start,
    // so its tagged-spawn compatibility teleport converges on this phase's
    // exact authored pose instead of a stale scene default.
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    public sealed class FarmPrologueDirector : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField] private bool beginOnStart = true;
        [SerializeField] private bool choresMustBeCompletedInOrder = true;

        [Header("Scene State Roots")]
        [Tooltip("Keep this director outside every root that it controls.")]
        [SerializeField] private GameObject prologueStateRoot;
        [SerializeField] private GameObject hubStateRoot;
        [SerializeField] private GameObject wakeUpSequenceRoot;
        [SerializeField] private GameObject choreSequenceRoot;
        [SerializeField] private GameObject rumbleSequenceRoot;

        [Header("Combat Roots")]
        [Tooltip("The root containing WaveManager. Keep separate from the spawner root.")]
        [SerializeField] private GameObject waveManagerRoot;
        [Tooltip("The root containing MobSpawner and its spawn points.")]
        [SerializeField] private GameObject mobSpawnerRoot;

        [Header("Existing-System Hooks")]
        [SerializeField] private waveManager waveEncounter;
        [SerializeField] private Inventory playerInventory;

        [Header("Campaign State and Farm Spawns")]
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private Transform playerTransform;
        [Tooltip("The one scene-local object tagged PlayerSpawnPos. Its pose is mirrored to the active Farm phase before Safety player startup or respawn.")]
        [SerializeField] private Transform playerSpawnFallback;
        [SerializeField] private Transform prologueSpawn;
        [SerializeField] private Transform hubSpawn;

        [Header("Player Gameplay Input Gate")]
        [Tooltip("Assign only existing player gameplay-input Behaviours, such as playerController and Interact. CharacterController is not disabled by this gate.")]
        [SerializeField] private Behaviour[] gameplayInputBehaviours =
            Array.Empty<Behaviour>();

        [Header("Chores")]
        [SerializeField] private FarmChoreInteractable[] chores =
            Array.Empty<FarmChoreInteractable>();

        [Header("Authored Screen Fade")]
        [Tooltip("Assign a CanvasGroup from the authored UI prefab. No UI is created at runtime.")]
        [SerializeField] private CanvasGroup screenFader;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 1.25f;
        [SerializeField, Min(0f)] private float blackScreenHoldSeconds = 0.5f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 1.25f;

        [Header("Automatic Phase Timing")]
        [SerializeField] private bool autoCompleteWakeUp = true;
        [SerializeField, Min(0f)] private float wakeUpSeconds = 2f;
        [SerializeField] private bool autoCompleteRumble = true;
        [SerializeField, Min(0f)] private float rumbleSeconds = 4f;

        [Header("Completion Save Recovery")]
        [SerializeField, Min(1)] private int completionSaveAttempts = 3;
        [SerializeField, Min(0f)] private float completionSaveRetrySeconds = 1f;
        [SerializeField, Min(0.1f)]
        private float completionSaveMaxRetrySeconds = 15f;

        [Header("Objective Copy")]
        [SerializeField] private string wakeUpObjective =
            "Wake up and get ready for the day.";
        [SerializeField] private string rumbleObjective =
            "Something is moving beneath the farm...";
        [SerializeField] private string combatObjective =
            "Defeat the cursed hogs.";
        [SerializeField] private string hubObjective =
            "The farm is safe. Use the truck when you are ready.";

        [Header("Authored Presentation and Sequence Hooks")]
        [SerializeField] private FarmProloguePhaseUnityEvent phaseChanged = new();
        [SerializeField] private FarmStringUnityEvent objectiveTextChanged = new();
        [SerializeField] private FarmObjectiveProgressUnityEvent objectiveProgressChanged = new();
        [SerializeField] private FarmStringUnityEvent interactionRejected = new();
        [SerializeField] private UnityEvent prologueStarted = new();
        [SerializeField] private UnityEvent wakeUpStarted = new();
        [SerializeField] private UnityEvent choresStarted = new();
        [SerializeField] private UnityEvent rumbleStarted = new();
        [SerializeField] private UnityEvent combatStarted = new();
        [SerializeField] private UnityEvent combatCompleted = new();
        [SerializeField] private UnityEvent hubTransitionStarted = new();
        [SerializeField] private FarmStringUnityEvent hubTransitionFailed = new();
        [SerializeField] private UnityEvent combatCleanupRequested = new();
        [SerializeField] private UnityEvent hubUnlocked = new();

        private Coroutine phaseRoutine;
        private Coroutine groundRumbleRoutine;
        private waveManager boundWaveEncounter;
        private CampaignStateService boundCampaignState;
        private gameManager boundGameManager;
        private bool hasStarted;
        private bool sceneReloadPending;
        private bool playerDeathPending;
        private bool prologueStartedRaised;
        private bool combatStartedRaised;
        private bool combatCompletionReceived;
        private bool combatCompletedRaised;
        private bool hubTransitionStartedRaised;
        private bool hubUnlockedRaised;
        private int completionRetryCycle;
        private bool playerControlsLocked;
        private Behaviour[] gatedBehaviourSnapshot = Array.Empty<Behaviour>();
        private bool[] gatedBehaviourEnabledStates = Array.Empty<bool>();
        private string currentObjectiveText = string.Empty;
        private int currentObjectiveAmount = -1;
        private int currentObjectiveRequired = -1;
        private bool groundRumbleActive;

        public FarmProloguePhase CurrentPhase { get; private set; } =
            FarmProloguePhase.Inactive;
        public bool IsHubUnlocked => CurrentPhase == FarmProloguePhase.Hub;
        public bool IsGroundRumbleActive => groundRumbleActive;
        public bool ChoresMustBeCompletedInOrder =>
            choresMustBeCompletedInOrder;
        public string CurrentObjectiveText => currentObjectiveText;
        public int CurrentObjectiveAmount => Mathf.Max(0, currentObjectiveAmount);
        public int CurrentObjectiveRequired => Mathf.Max(0, currentObjectiveRequired);
        public Transform PlayerSpawnFallback => playerSpawnFallback;

        public event Action<FarmProloguePhase> PhaseChanged;
        public event Action<bool> GroundRumbleStateChanged;
        public event Action<string> ObjectiveTextChanged;
        public event Action<string, int, int> ObjectiveProgressChanged;
        public event Action<string> InteractionRejected;
        public event Action HubUnlocked;

        private void Awake()
        {
            BindChoreDirectors();
            PrepareInitialSceneState();
            ClaimExistingCompletionSystems();
        }

        private void OnEnable()
        {
            ResolveCampaignReferences();
            BindCampaignEvents();
            BindGameManagerEvents();
            BindWaveEvents();

            if (!hasStarted)
                return;

            ApplyPlayerControlForPhase(CurrentPhase);
            ResumeInterruptedLifecycle();
        }

        private void Start()
        {
            hasStarted = true;
            ClaimExistingCompletionSystems();
            ResolveCampaignReferences();
            BindCampaignEvents();
            BindGameManagerEvents();

            if (campaignState != null &&
                campaignState.HasCompletedPrologue)
            {
                EnterHubFromSavedProgress();
                return;
            }

            MovePlayerToAuthoritativeSpawn(prologueSpawn);

            if (beginOnStart)
            {
                BeginPrologue();
            }
        }

        private void OnDisable()
        {
            StopPhaseRoutine();
            StopGroundRumble();
            UnbindWaveEvents();
            UnbindCampaignEvents();
            UnbindGameManagerEvents();
            SetPlayerControlsLocked(false);
        }

        private void OnDestroy()
        {
            StopGroundRumble();
            SetPlayerControlsLocked(false);
            UnbindWaveEvents();
            UnbindCampaignEvents();
            UnbindGameManagerEvents();
        }

        private void OnValidate()
        {
            wakeUpSeconds = Mathf.Max(0f, wakeUpSeconds);
            rumbleSeconds = Mathf.Max(0f, rumbleSeconds);
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            blackScreenHoldSeconds = Mathf.Max(0f, blackScreenHoldSeconds);
            fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            completionSaveAttempts = Mathf.Max(1, completionSaveAttempts);
            completionSaveRetrySeconds =
                Mathf.Max(0f, completionSaveRetrySeconds);
            completionSaveMaxRetrySeconds =
                Mathf.Max(0.1f, completionSaveMaxRetrySeconds);

            if (gameplayInputBehaviours == null)
                return;
        }

        public void BeginPrologue()
        {
            if (sceneReloadPending ||
                CurrentPhase != FarmProloguePhase.Inactive)
            {
                return;
            }

            StopPhaseRoutine();
            StopGroundRumble();
            ResolveCampaignReferences();
            ClaimExistingCompletionSystems();
            combatCompletionReceived = false;
            combatCompletedRaised = false;
            hubTransitionStartedRaised = false;
            completionRetryCycle = 0;

            MovePlayerToAuthoritativeSpawn(prologueSpawn);
            SetActive(prologueStateRoot, true);
            SetActive(hubStateRoot, false);
            SetActive(waveManagerRoot, false);
            SetActive(mobSpawnerRoot, false);
            SetFaderInstantly(0f);

            if (campaignState != null &&
                campaignState.PrologueCursedObjectRevealed)
            {
                RestoreCompletedChores();
                SetPhase(FarmProloguePhase.AwaitingOffering);
                RaisePrologueStartedOnce();
                PublishObjective(
                    campaignState.PrologueCursedObjectOffered
                        ? "The Root Tree accepted the curse. The emergence is pending."
                        : "Carry the cursed object to the Root Tree.",
                    0,
                    1);
                return;
            }

            ResetChores();

            SetPhase(FarmProloguePhase.WakeUp);
            RaisePrologueStartedOnce();
            FarmPrologueEventUtility.Invoke(wakeUpStarted, this);
            PublishObjective(wakeUpObjective, 0, 1);

            if (autoCompleteWakeUp)
            {
                phaseRoutine =
                    StartCoroutine(CompleteWakeUpAfterDelay());
            }
        }

        public void ConfigureCampaign(
            CampaignStateService stateService,
            Transform player,
            Transform fallbackSpawnPoint,
            Transform prologueSpawnPoint,
            Transform hubSpawnPoint)
        {
            UnbindCampaignEvents();
            campaignState = stateService;
            playerTransform = player;
            playerSpawnFallback = fallbackSpawnPoint;
            prologueSpawn = prologueSpawnPoint;
            hubSpawn = hubSpawnPoint;

            if (isActiveAndEnabled)
            {
                ResolveCampaignReferences();
                BindCampaignEvents();
            }
        }

        public void ConfigureCampaign(
            CampaignStateService stateService,
            Transform player,
            Transform prologueSpawnPoint,
            Transform hubSpawnPoint)
        {
            ConfigureCampaign(
                stateService,
                player,
                playerSpawnFallback,
                prologueSpawnPoint,
                hubSpawnPoint);
        }

        public void ConfigureStateRoots(
            GameObject prologueRoot,
            GameObject hubRoot,
            GameObject wakeUpRoot,
            GameObject choresRoot,
            GameObject rumbleRoot)
        {
            prologueStateRoot = prologueRoot;
            hubStateRoot = hubRoot;
            wakeUpSequenceRoot = wakeUpRoot;
            choreSequenceRoot = choresRoot;
            rumbleSequenceRoot = rumbleRoot;
            ApplyPhaseSceneState(CurrentPhase);
        }

        public void ConfigureEncounter(
            waveManager manager,
            GameObject managerRoot,
            GameObject spawnerRoot)
        {
            UnbindWaveEvents();
            waveEncounter = manager;
            waveManagerRoot = managerRoot;
            mobSpawnerRoot = spawnerRoot;

            if (isActiveAndEnabled)
            {
                BindWaveEvents();
            }
        }

        public void ConfigurePlayerControl(
            Behaviour[] inputBehaviours)
        {
            SetPlayerControlsLocked(false);
            gameplayInputBehaviours =
                inputBehaviours ?? Array.Empty<Behaviour>();

            if (isActiveAndEnabled && hasStarted)
            {
                ApplyPlayerControlForPhase(CurrentPhase);
            }
        }

        public void ConfigureChores(
            Inventory inventory,
            FarmChoreInteractable[] choreReferences)
        {
            playerInventory = inventory;
            chores = choreReferences ?? Array.Empty<FarmChoreInteractable>();
            BindChoreDirectors();
        }

        public void ConfigureChoreOrder(bool mustBeCompletedInOrder)
        {
            choresMustBeCompletedInOrder = mustBeCompletedInOrder;

            if (isActiveAndEnabled &&
                hasStarted &&
                CurrentPhase == FarmProloguePhase.Chores)
            {
                RefreshChoreAvailabilityAndObjective();
            }
        }

        public void ConfigureScreenFader(CanvasGroup authoredScreenFader)
        {
            screenFader = authoredScreenFader;
        }

        public void ConfigureAutomaticTiming(
            bool automaticallyCompleteWakeUp,
            float wakeUpDelay,
            bool automaticallyCompleteRumble,
            float rumbleDelay)
        {
            autoCompleteWakeUp = automaticallyCompleteWakeUp;
            wakeUpSeconds = Mathf.Max(0f, wakeUpDelay);
            autoCompleteRumble = automaticallyCompleteRumble;
            rumbleSeconds = Mathf.Max(0f, rumbleDelay);
        }

        public void CompleteWakeUp()
        {
            if (CurrentPhase != FarmProloguePhase.WakeUp)
                return;

            StopPhaseRoutine();

            if (!ValidateChoreConfiguration(out string failureReason))
            {
                PublishObjective(failureReason, 0, 0);
                RejectChoreInteraction(null, failureReason);
                return;
            }

            SetPhase(FarmProloguePhase.Chores);
            FarmPrologueEventUtility.Invoke(choresStarted, this);
            RefreshChoreAvailabilityAndObjective();
        }

        public bool TryPerformChore(FarmChoreInteractable chore)
        {
            if (CurrentPhase != FarmProloguePhase.Chores)
            {
                RejectChoreInteraction(
                    chore,
                    "Farm chores are not active right now.");
                return false;
            }

            if (chore == null || !ContainsChore(chore))
            {
                RejectChoreInteraction(
                    chore,
                    "This chore is not registered with the Farm Prologue Director.");
                return false;
            }

            FarmChoreInteractable expectedChore =
                GetNextIncompleteChore();

            if (choresMustBeCompletedInOrder &&
                expectedChore != null &&
                chore != expectedChore)
            {
                RejectChoreInteraction(
                    chore,
                    $"Complete {expectedChore.ObjectiveText} first.");
                return false;
            }

            if (!chore.TryApplyInteraction(
                    playerInventory,
                    out string failureReason))
            {
                RejectChoreInteraction(chore, failureReason);
                return false;
            }

            RefreshChoreAvailabilityAndObjective();

            if (AreAllChoresComplete())
            {
                BeginAwaitingOffering();
            }

            return true;
        }

        internal void ReportChoreInteractionRejected(
            FarmChoreInteractable chore,
            string reason)
        {
            RejectChoreInteraction(chore, reason);
        }

        public void BeginAwaitingOffering()
        {
            if (CurrentPhase != FarmProloguePhase.Chores)
                return;

            if (!AreAllChoresComplete())
            {
                RejectChoreInteraction(
                    null,
                    "Complete every morning chore before the prologue can continue.");
                return;
            }

            StopPhaseRoutine();
            StopGroundRumble();
            SetPhase(FarmProloguePhase.AwaitingOffering);
            PublishObjective(
                "The cursed object surfaced. Recover it and carry it to the Root Tree.",
                0,
                1);
        }

        /// <summary>
        /// Compatibility entry point retained for authored UnityEvents. Chore
        /// completion now waits for the durable Root Tree offering instead of
        /// starting the presentation or combat early.
        /// </summary>
        public void BeginRumble()
        {
            BeginAwaitingOffering();
        }

        public void CompleteRumble()
        {
            BeginTreeFedEmergence();
        }

        /// <summary>
        /// Starts the authored ground rumble and the existing three-wave
        /// encounter together, but only after the prologue offering is both
        /// durably committed and durably active.
        /// </summary>
        public bool BeginTreeFedEmergence()
        {
            if (CurrentPhase != FarmProloguePhase.AwaitingOffering &&
                CurrentPhase != FarmProloguePhase.Rumble)
            {
                return false;
            }

            ResolveCampaignReferences();
            if (campaignState == null ||
                !campaignState.PrologueCursedObjectOffered ||
                !string.Equals(
                    campaignState.ActiveFarmEmergenceOfferingId,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                RejectChoreInteraction(
                    null,
                    "Offer the cursed object to the Root Tree before the emergence can begin.");
                return false;
            }

            if (!CanStartCombat(out string failureReason))
            {
                PublishObjective(failureReason, 0, 1);
                RejectChoreInteraction(null, failureReason);
                return false;
            }

            StopPhaseRoutine();
            SetPhase(FarmProloguePhase.Rumble);
            PublishObjective(rumbleObjective, 0, 1);
            StartGroundRumble();
            phaseRoutine = StartCoroutine(BeginCombatSequence());
            return true;
        }

        /// <summary>
        /// Lets campaign-owned Farm mechanics reuse the authored objective
        /// presenter without creating another runtime UI authority.
        /// </summary>
        public void PublishCampaignObjective(
            string objective,
            int amount,
            int required)
        {
            PublishObjective(objective, amount, required);
        }

        public void NotifyCombatCompleted()
        {
            if (CurrentPhase != FarmProloguePhase.Combat &&
                CurrentPhase != FarmProloguePhase.CompletionPending)
            {
                return;
            }

            if (waveEncounter != null &&
                !waveEncounter.FinalWaveCleared)
            {
                RejectChoreInteraction(
                    null,
                    "The safe hub cannot unlock until every cursed-hog wave is cleared.");
                return;
            }

            combatCompletionReceived = true;
            RaiseCombatCompletedOnce();

            if (playerDeathPending)
            {
                StopPhaseRoutine();
                SetFaderInstantly(0f);
                SetPhase(FarmProloguePhase.CompletionPending);
                PublishObjective(
                    "Respawn before the farm can transition to the safe hub.",
                    0,
                    1);
                return;
            }

            StartHubTransition();
        }

        public void RetryHubTransition()
        {
            if (CurrentPhase != FarmProloguePhase.CompletionPending ||
                !combatCompletionReceived ||
                playerDeathPending)
            {
                return;
            }

            StartHubTransition();
        }

        private IEnumerator CompleteWakeUpAfterDelay()
        {
            yield return WaitRealtime(wakeUpSeconds);
            phaseRoutine = null;
            CompleteWakeUp();
        }

        private IEnumerator BeginCombatSequence()
        {
            if (!CanStartCombat(out string failureReason))
            {
                StopGroundRumble();
                PublishObjective(failureReason, 0, 1);
                RejectChoreInteraction(null, failureReason);
                phaseRoutine = null;
                yield break;
            }

            SetPhase(FarmProloguePhase.Combat);
            PublishObjective(combatObjective, 0, 1);
            RaiseCombatStartedOnce();
            SetActive(waveManagerRoot, true);

            // Let WaveManager finish Start while MobSpawner remains gated.
            yield return null;

            if (!isActiveAndEnabled ||
                CurrentPhase != FarmProloguePhase.Combat)
            {
                phaseRoutine = null;
                yield break;
            }

            ClaimExistingCompletionSystems();
            BindWaveEvents();
            SetActive(mobSpawnerRoot, true);

            if (waveEncounter == null)
            {
                const string missingManager =
                    "The cursed-hog encounter lost its WaveManager during startup.";
                SetPhase(FarmProloguePhase.AwaitingOffering);
                PublishObjective(missingManager, 0, 1);
                RejectChoreInteraction(null, missingManager);
                phaseRoutine = null;
                yield break;
            }

            if (!waveEncounter.EncounterStarted)
            {
                waveEncounter.BeginEncounter();
            }

            phaseRoutine = null;
        }

        private bool CanStartCombat(out string failureReason)
        {
            if (waveEncounter == null || waveManagerRoot == null ||
                mobSpawnerRoot == null)
            {
                failureReason =
                    "The cursed-hog encounter is missing an authored " +
                    "WaveManager or MobSpawner reference.";
                return false;
            }

            if (waveManagerRoot == mobSpawnerRoot ||
                waveEncounter.gameObject != waveManagerRoot ||
                mobSpawnerRoot.GetComponent<MobSpawner>() == null)
            {
                failureReason =
                    "The cursed-hog encounter roots are not wired to the " +
                    "expected WaveManager and MobSpawner objects.";
                return false;
            }

            if (gameManager.instance == null)
            {
                failureReason =
                    "The cursed-hog encounter cannot begin without the " +
                    "authored GameManager.";
                return false;
            }

            MobSpawner[] sceneSpawners =
                Resources.FindObjectsOfTypeAll<MobSpawner>();
            int matchingSceneSpawners = 0;

            foreach (MobSpawner candidate in sceneSpawners)
            {
                if (candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene.isLoaded &&
                    candidate.gameObject.scene == gameObject.scene)
                {
                    matchingSceneSpawners++;
                }
            }

            if (matchingSceneSpawners != 1)
            {
                failureReason =
                    "The Farm must contain exactly one authored MobSpawner " +
                    $"before combat can begin; found {matchingSceneSpawners}.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private void HandleWaveStarted(
            int waveNumber,
            int enemyCount,
            BloodMoonModifier modifier)
        {
            if (CurrentPhase != FarmProloguePhase.Combat)
                return;

            PublishCombatProgress(
                waveNumber,
                Mathf.Max(0, enemyCount),
                Mathf.Max(0, enemyCount));
        }

        private void HandleEnemyCountChanged(
            int waveNumber,
            int remaining,
            int expected)
        {
            if (CurrentPhase != FarmProloguePhase.Combat)
                return;

            PublishCombatProgress(waveNumber, remaining, expected);
        }

        private void PublishCombatProgress(
            int waveNumber,
            int remaining,
            int expected)
        {
            int safeExpected = Mathf.Max(0, expected);
            int safeRemaining = Mathf.Clamp(
                remaining,
                0,
                safeExpected);
            int defeated = safeExpected - safeRemaining;
            string objective = safeExpected == 1
                ? $"Defeat the cursed hog. Wave {waveNumber}."
                : $"Defeat the cursed hogs. Wave {waveNumber}.";

            PublishObjective(
                objective,
                defeated,
                Mathf.Max(1, safeExpected));
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            if (CurrentPhase != FarmProloguePhase.Combat)
                return;

            PublishObjective(
                $"Wave {waveNumber} cleared. More cursed hogs are coming.",
                1,
                1);
        }

        private void HandleAllWavesCompleted()
        {
            NotifyCombatCompleted();
        }

        private void StartHubTransition()
        {
            if (!combatCompletionReceived ||
                playerDeathPending ||
                CurrentPhase == FarmProloguePhase.FadeToHub ||
                CurrentPhase == FarmProloguePhase.Hub)
            {
                return;
            }

            StopPhaseRoutine();
            phaseRoutine = StartCoroutine(TransitionToHub());
        }

        private IEnumerator TransitionToHub()
        {
            SetPhase(FarmProloguePhase.FadeToHub);
            RaiseHubTransitionStartedOnce();
            PublishObjective(string.Empty, 0, 0);
            yield return FadeTo(1f, fadeOutSeconds);

            if (playerDeathPending)
            {
                phaseRoutine = null;
                yield break;
            }

            string saveFailureReason = string.Empty;
            bool saved = false;

            for (int attempt = 1;
                 attempt <= completionSaveAttempts;
                 attempt++)
            {
                ResolveCampaignReferences();

                try
                {
                    if (TryPersistPrologueCompletion(out saveFailureReason))
                    {
                        saved = true;
                        break;
                    }
                }
                catch (Exception exception)
                {

                    saveFailureReason =
                        "The safe hub remains locked because campaign " +
                        "completion raised an unexpected save error.";
                }

                if (attempt < completionSaveAttempts)
                {
                    yield return WaitRealtime(completionSaveRetrySeconds);
                }
            }

            if (!saved)
            {

                SetPhase(FarmProloguePhase.CompletionPending);
                string retryMessage =
                    saveFailureReason +
                    " Prologue completion is still pending; retrying is safe.";
                PublishObjective(retryMessage, 0, 1);
                RejectChoreInteraction(null, retryMessage);
                FarmPrologueEventUtility.Invoke(
                    hubTransitionFailed,
                    retryMessage,
                    this);
                yield return FadeTo(0f, fadeInSeconds);
                phaseRoutine = null;
                ScheduleHubTransitionRetry();
                yield break;
            }

            completionRetryCycle = 0;
            yield return WaitRealtime(blackScreenHoldSeconds);
            FarmPrologueEventUtility.Invoke(combatCleanupRequested, this);
            SetActive(mobSpawnerRoot, false);
            SetActive(waveManagerRoot, false);
            // Prologue Objectives contains the persistent physical Farm
            // environment. Its wake, rumble, interaction, and combat systems
            // are gated independently, so keep the parent active in Hub.
            SetActive(prologueStateRoot, true);
            SetActive(hubStateRoot, true);
            MovePlayerToAuthoritativeSpawn(hubSpawn);
            yield return FadeTo(0f, fadeInSeconds);

            // Keep gameplay input gated until the authored fade is fully
            // transparent. Entering Hub earlier would let the player move or
            // interact while the screen was still black.
            SetPhase(FarmProloguePhase.Hub);
            PublishObjective(hubObjective, 1, 1);
            RaiseHubUnlockedOnce();
            phaseRoutine = null;
        }

        private void ScheduleHubTransitionRetry()
        {
            if (!isActiveAndEnabled ||
                phaseRoutine != null ||
                playerDeathPending ||
                !combatCompletionReceived ||
                CurrentPhase != FarmProloguePhase.CompletionPending)
            {
                return;
            }

            phaseRoutine = StartCoroutine(RetryHubTransitionAfterDelay());
        }

        private IEnumerator RetryHubTransitionAfterDelay()
        {
            float retryDelay = CalculateCompletionRetryDelay(
                completionRetryCycle,
                completionSaveRetrySeconds,
                completionSaveMaxRetrySeconds);
            completionRetryCycle = Mathf.Min(completionRetryCycle + 1, 10);
            yield return WaitRealtime(retryDelay);
            phaseRoutine = null;

            if (!isActiveAndEnabled ||
                playerDeathPending ||
                !combatCompletionReceived ||
                CurrentPhase != FarmProloguePhase.CompletionPending)
            {
                yield break;
            }

            StartHubTransition();
        }

        private static float CalculateCompletionRetryDelay(
            int retryCycle,
            float initialDelay,
            float maximumDelay)
        {
            float safeInitial = Mathf.Max(0.1f, initialDelay);
            float safeMaximum = Mathf.Max(0.1f, maximumDelay);
            int exponent = Mathf.Clamp(retryCycle, 0, 10);
            return Mathf.Min(
                safeMaximum,
                safeInitial * Mathf.Pow(2f, exponent));
        }

        private bool TryPersistPrologueCompletion(
            out string failureReason)
        {
            if (campaignState == null)
            {
                failureReason =
                    "The safe hub remains locked because campaign progress " +
                    "could not be saved. No Campaign State Service is active.";
                return false;
            }

            if (campaignState.HasCompletedPrologue)
            {
                if (campaignState.SaveNow())
                {
                    failureReason = string.Empty;
                    return true;
                }

                failureReason =
                    "The safe hub remains locked because completed prologue " +
                    "progress could not be verified on disk.";
                return false;
            }

            bool markedCompleted = campaignState.MarkPrologueCompleted();

            if (!markedCompleted ||
                !campaignState.HasCompletedPrologue)
            {
                failureReason =
                    "The safe hub remains locked because prologue completion " +
                    "was not accepted by Campaign State.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private void PrepareInitialSceneState()
        {
            SetActive(hubStateRoot, false);
            SetActive(waveManagerRoot, false);
            SetActive(mobSpawnerRoot, false);
            SetFaderInstantly(0f);
        }

        private void EnterHubFromSavedProgress()
        {
            StopPhaseRoutine();
            ClaimExistingCompletionSystems();
            RestoreCompletedChores();
            playerDeathPending = false;
            combatCompletionReceived = true;
            completionRetryCycle = 0;

            SetActive(waveManagerRoot, false);
            SetActive(mobSpawnerRoot, false);
            // A saved Hub arrival must reconstruct the same completed Farm
            // environment as a continuous prologue playthrough.
            SetActive(prologueStateRoot, true);
            SetActive(hubStateRoot, true);
            SetFaderInstantly(0f);
            MovePlayerToAuthoritativeSpawn(hubSpawn);

            SetPhase(FarmProloguePhase.Hub);
            PublishObjective(hubObjective, 1, 1);
            RaiseHubUnlockedOnce();
        }

        private void ResolveCampaignReferences()
        {
            CampaignStateService resolvedState =
                CampaignStateService.Instance != null
                    ? CampaignStateService.Instance
                    : campaignState;

            if (resolvedState != campaignState)
            {
                UnbindCampaignEvents();
                campaignState = resolvedState;
            }

            if (playerTransform == null &&
                gameManager.instance != null &&
                gameManager.instance.player != null)
            {
                playerTransform = gameManager.instance.player.transform;
            }

            BindGameManagerEvents();
        }

        private void MovePlayerToAuthoritativeSpawn(Transform destination)
        {
            if (destination == null)
                return;

            if (playerSpawnFallback != null)
            {
                playerSpawnFallback.SetPositionAndRotation(
                    destination.position,
                    destination.rotation);
            }

            MovePlayerTo(destination);
        }

        private void MovePlayerTo(Transform destination)
        {
            if (playerTransform == null || destination == null)
                return;

            CharacterController characterController =
                playerTransform.GetComponent<CharacterController>();
            bool restoreController =
                characterController != null && characterController.enabled;

            try
            {
                if (restoreController)
                {
                    // CharacterController is disabled only for the atomic
                    // teleport; it is never part of the longer input gate.
                    characterController.enabled = false;
                }

                playerTransform.SetPositionAndRotation(
                    destination.position,
                    destination.rotation);
                Physics.SyncTransforms();
            }
            catch (Exception exception)
            {

            }
            finally
            {
                if (restoreController && characterController != null)
                {
                    characterController.enabled = true;
                }
            }
        }

        private void ClaimExistingCompletionSystems()
        {
            if (waveEncounter != null)
            {
                waveEncounter.SetCompletionOwnedExternally(true);
            }

            //if (gameManager.instance != null)
            //{
            //    gameManager.instance.SetWaveManagerControlsWin(true);
            //}

        }

        private void BindWaveEvents()
        {
            if (boundWaveEncounter == waveEncounter &&
                boundWaveEncounter != null)
            {
                return;
            }

            UnbindWaveEvents();

            if (waveEncounter == null)
                return;

            waveEncounter.WaveStarted += HandleWaveStarted;
            waveEncounter.WaveCompleted += HandleWaveCompleted;
            waveEncounter.EnemyCountChanged += HandleEnemyCountChanged;
            waveEncounter.AllWavesCompleted += HandleAllWavesCompleted;
            boundWaveEncounter = waveEncounter;
        }

        private void UnbindWaveEvents()
        {
            if (boundWaveEncounter != null)
            {
                boundWaveEncounter.WaveStarted -= HandleWaveStarted;
                boundWaveEncounter.WaveCompleted -= HandleWaveCompleted;
                boundWaveEncounter.EnemyCountChanged -= HandleEnemyCountChanged;
                boundWaveEncounter.AllWavesCompleted -= HandleAllWavesCompleted;
            }

            boundWaveEncounter = null;
        }

        private void BindCampaignEvents()
        {
            if (boundCampaignState == campaignState &&
                boundCampaignState != null)
            {
                return;
            }

            UnbindCampaignEvents();

            if (campaignState == null)
                return;

            campaignState.NewGameStarted += HandleNewGameStarted;
            campaignState.ProgressLoaded += HandleProgressLoaded;
            boundCampaignState = campaignState;
        }

        private void UnbindCampaignEvents()
        {
            if (boundCampaignState != null)
            {
                boundCampaignState.NewGameStarted -= HandleNewGameStarted;
                boundCampaignState.ProgressLoaded -= HandleProgressLoaded;
            }

            boundCampaignState = null;
        }

        private void BindGameManagerEvents()
        {
            gameManager resolvedManager = gameManager.instance;

            if (boundGameManager == resolvedManager)
                return;

            UnbindGameManagerEvents();

            if (resolvedManager == null)
                return;

            resolvedManager.PlayerLost += HandlePlayerLost;
            resolvedManager.PlayerRespawned += HandlePlayerRespawned;
            boundGameManager = resolvedManager;
        }

        private void UnbindGameManagerEvents()
        {
            if (boundGameManager != null)
            {
                boundGameManager.PlayerLost -= HandlePlayerLost;
                boundGameManager.PlayerRespawned -= HandlePlayerRespawned;
            }

            boundGameManager = null;
        }

        private void HandlePlayerLost()
        {
            if (CurrentPhase != FarmProloguePhase.Combat &&
                CurrentPhase != FarmProloguePhase.FadeToHub &&
                CurrentPhase != FarmProloguePhase.CompletionPending)
            {
                return;
            }

            playerDeathPending = true;
            StopPhaseRoutine();
            SetFaderInstantly(0f);

            bool encounterCleared =
                combatCompletionReceived ||
                (waveEncounter != null && waveEncounter.FinalWaveCleared);
            SetPhase(
                encounterCleared
                    ? FarmProloguePhase.CompletionPending
                    : FarmProloguePhase.Combat);
            PublishObjective(
                encounterCleared
                    ? "Respawn before the farm can transition to the safe hub."
                    : "You were overwhelmed. Respawn to continue the defense.",
                0,
                1);
        }

        private void HandlePlayerRespawned()
        {
            ResolveCampaignReferences();
            bool shouldUseHubSpawn =
                CurrentPhase == FarmProloguePhase.Hub ||
                (campaignState != null &&
                 campaignState.HasCompletedPrologue);
            MovePlayerToAuthoritativeSpawn(
                shouldUseHubSpawn ? hubSpawn : prologueSpawn);

            if (!playerDeathPending)
                return;

            playerDeathPending = false;

            if (campaignState != null && campaignState.HasCompletedPrologue)
            {
                EnterHubFromSavedProgress();
                return;
            }

            if (combatCompletionReceived ||
                (waveEncounter != null && waveEncounter.FinalWaveCleared))
            {
                combatCompletionReceived = true;
                SetPhase(FarmProloguePhase.CompletionPending);
                StartHubTransition();
                return;
            }

            SetPhase(FarmProloguePhase.Combat);
            PublishObjective(combatObjective, 0, 1);
            ResumeCombatStartupIfNeeded();
        }

        private void HandleNewGameStarted()
        {
            if (!hasStarted || sceneReloadPending)
                return;

            RestartForCurrentCampaign();
        }

        private void HandleProgressLoaded(
            CampaignProgressSnapshot snapshot)
        {
            if (!hasStarted || sceneReloadPending)
                return;

            if (HasRunningEncounter())
            {
                ScheduleFarmSceneReload();
                return;
            }

            if (snapshot.PrologueCompleted)
            {
                EnterHubFromSavedProgress();
            }
            else
            {
                RestartForCurrentCampaign();
            }
        }

        private void RestartForCurrentCampaign()
        {
            if (HasRunningEncounter())
            {
                ScheduleFarmSceneReload();
                return;
            }

            StopPhaseRoutine();
            prologueStartedRaised = false;
            combatStartedRaised = false;
            combatCompletionReceived = false;
            combatCompletedRaised = false;
            hubTransitionStartedRaised = false;
            hubUnlockedRaised = false;
            completionRetryCycle = 0;
            playerDeathPending = false;
            currentObjectiveText = string.Empty;
            currentObjectiveAmount = -1;
            currentObjectiveRequired = -1;
            SetPhase(FarmProloguePhase.Inactive);
            BeginPrologue();
        }

        private bool HasRunningEncounter()
        {
            return waveEncounter != null && waveEncounter.EncounterStarted;
        }

        private void ScheduleFarmSceneReload()
        {
            if (sceneReloadPending)
                return;

            Scene scene = gameObject.scene;

            if (!scene.IsValid() || !scene.isLoaded)
            {

                return;
            }

            sceneReloadPending = true;
            StopPhaseRoutine();
            SetPlayerControlsLocked(true);
            SetFaderInstantly(1f);
            phaseRoutine = StartCoroutine(ReloadFarmSceneNextFrame(scene.name));
        }

        private IEnumerator ReloadFarmSceneNextFrame(string sceneName)
        {
            yield return null;
            phaseRoutine = null;

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                HandleFarmSceneReloadFailure(
                    "The Farm scene has no valid name and could not reload.");
                yield break;
            }

            try
            {
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {

                HandleFarmSceneReloadFailure(
                    "The Farm scene could not reload to reset the active encounter.");
            }
        }

        private void HandleFarmSceneReloadFailure(string reason)
        {
            sceneReloadPending = false;
            SetPlayerControlsLocked(false);
            SetFaderInstantly(0f);
            PublishObjective(reason, 0, 1);
            RejectChoreInteraction(null, reason);
        }

        private void ResumeInterruptedLifecycle()
        {
            if (playerDeathPending)
            {
                SetPlayerControlsLocked(true);
                return;
            }

            if (sceneReloadPending)
            {
                SetPlayerControlsLocked(true);
                Scene scene = gameObject.scene;
                phaseRoutine = StartCoroutine(
                    ReloadFarmSceneNextFrame(scene.name));
                return;
            }

            switch (CurrentPhase)
            {
                case FarmProloguePhase.WakeUp when autoCompleteWakeUp:
                    phaseRoutine =
                        StartCoroutine(CompleteWakeUpAfterDelay());
                    break;

                case FarmProloguePhase.Rumble:
                    BeginTreeFedEmergence();
                    break;

                case FarmProloguePhase.Combat:
                    if (waveEncounter != null &&
                        waveEncounter.FinalWaveCleared)
                    {
                        NotifyCombatCompleted();
                    }
                    else
                    {
                        ResumeCombatStartupIfNeeded();
                    }
                    break;

                case FarmProloguePhase.FadeToHub:
                    phaseRoutine = StartCoroutine(TransitionToHub());
                    break;

                case FarmProloguePhase.CompletionPending when
                    combatCompletionReceived:
                    ScheduleHubTransitionRetry();
                    break;
            }
        }

        private void BindChoreDirectors()
        {
            if (chores == null)
                return;

            foreach (FarmChoreInteractable chore in chores)
            {
                if (chore != null)
                {
                    chore.SetDirector(this);
                }
            }
        }

        private void ResetChores()
        {
            if (chores == null)
                return;

            foreach (FarmChoreInteractable chore in chores)
            {
                if (chore != null)
                {
                    chore.ResetChoreProgress();
                }
            }
        }

        private void RestoreCompletedChores()
        {
            if (chores == null)
                return;

            foreach (FarmChoreInteractable chore in chores)
            {
                chore?.RestoreCompletedProgress();
            }
        }

        private bool ValidateChoreConfiguration(out string failureReason)
        {
            if (chores == null || chores.Length == 0)
            {
                failureReason =
                    "The prologue cannot begin chores because no authored chores are configured.";
                return false;
            }

            var references = new HashSet<FarmChoreInteractable>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < chores.Length; index++)
            {
                FarmChoreInteractable chore = chores[index];

                if (chore == null)
                {
                    failureReason =
                        $"Farm chore slot {index + 1} has no authored interactable.";
                    return false;
                }

                if (!references.Add(chore))
                {
                    failureReason =
                        $"Farm chore '{chore.name}' is assigned more than once.";
                    return false;
                }

                string choreId = chore.ChoreId?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(choreId) || !ids.Add(choreId))
                {
                    failureReason =
                        "Every authored Farm chore needs a unique non-empty chore ID.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private void RefreshChoreAvailabilityAndObjective()
        {
            FarmChoreInteractable nextChore = GetNextIncompleteChore();

            if (chores != null)
            {
                foreach (FarmChoreInteractable chore in chores)
                {
                    if (chore == null)
                        continue;

                    bool available =
                        CurrentPhase == FarmProloguePhase.Chores &&
                        !chore.IsComplete &&
                        (!choresMustBeCompletedInOrder || chore == nextChore);
                    chore.SetAvailable(available);
                }
            }

            if (nextChore == null)
            {
                PublishObjective("Farm chores complete.", 1, 1);
                return;
            }

            if (choresMustBeCompletedInOrder)
            {
                PublishObjective(
                    nextChore.ObjectiveText,
                    nextChore.CompletedInteractions,
                    nextChore.RequiredInteractions);
                return;
            }

            int completed = CountCompletedChores();
            PublishObjective(
                $"Complete the morning chores: {completed}/{chores.Length}",
                completed,
                chores.Length);
        }

        private void SetAllChoresUnavailable()
        {
            if (chores == null)
                return;

            foreach (FarmChoreInteractable chore in chores)
            {
                chore?.SetAvailable(false);
            }
        }

        private FarmChoreInteractable GetNextIncompleteChore()
        {
            if (chores == null)
                return null;

            foreach (FarmChoreInteractable chore in chores)
            {
                if (chore != null && !chore.IsComplete)
                    return chore;
            }

            return null;
        }

        private bool AreAllChoresComplete()
        {
            if (chores == null || chores.Length == 0)
                return false;

            foreach (FarmChoreInteractable chore in chores)
            {
                if (chore == null || !chore.IsComplete)
                    return false;
            }

            return true;
        }

        private int CountCompletedChores()
        {
            int completed = 0;

            if (chores == null)
                return completed;

            foreach (FarmChoreInteractable chore in chores)
            {
                if (chore != null && chore.IsComplete)
                {
                    completed++;
                }
            }

            return completed;
        }

        private bool ContainsChore(FarmChoreInteractable candidate)
        {
            if (chores == null)
                return false;

            foreach (FarmChoreInteractable chore in chores)
            {
                if (chore == candidate)
                    return true;
            }

            return false;
        }

        private void RejectChoreInteraction(
            FarmChoreInteractable chore,
            string reason)
        {
            string safeReason = string.IsNullOrWhiteSpace(reason)
                ? "The chore could not be completed."
                : reason;

            chore?.RejectInteraction(safeReason);
            FarmPrologueEventUtility.Invoke(
                interactionRejected,
                safeReason,
                this);
            FarmPrologueEventUtility.Invoke(
                InteractionRejected,
                safeReason,
                this);
        }

        private void SetPhase(FarmProloguePhase phase)
        {
            if (phase != FarmProloguePhase.Rumble &&
                phase != FarmProloguePhase.Combat)
            {
                StopGroundRumble();
            }

            bool changed = CurrentPhase != phase;
            CurrentPhase = phase;
            ApplyPhaseSceneState(phase);
            ApplyPlayerControlForPhase(phase);

            if (phase != FarmProloguePhase.Chores)
            {
                SetAllChoresUnavailable();
            }

            if (!changed)
                return;

            FarmPrologueEventUtility.Invoke(phaseChanged, phase, this);
            FarmPrologueEventUtility.Invoke(PhaseChanged, phase, this);
        }

        private void ApplyPhaseSceneState(FarmProloguePhase phase)
        {
            SetActive(
                wakeUpSequenceRoot,
                phase == FarmProloguePhase.WakeUp);
            SetActive(
                choreSequenceRoot,
                ShouldShowChoreEnvironment(phase));
            SetActive(
                rumbleSequenceRoot,
                groundRumbleActive);
        }

        /// <summary>
        /// Chore interaction is phase-gated separately. The authored chore
        /// environment contains physical Farm props plus pending/completed
        /// state presentation, so it remains visible after each interaction,
        /// throughout the emergence, and in the saved Hub state.
        /// </summary>
        public static bool ShouldShowChoreEnvironment(
            FarmProloguePhase phase)
        {
            return phase != FarmProloguePhase.Inactive;
        }

        private void ApplyPlayerControlForPhase(FarmProloguePhase phase)
        {
            bool shouldLock =
                phase == FarmProloguePhase.WakeUp ||
                phase == FarmProloguePhase.FadeToHub ||
                playerDeathPending;
            // Rumble is authored gameplay, not a cutscene: keep movement,
            // interaction, and camera input live while its presenter shakes.
            SetPlayerControlsLocked(shouldLock);
        }

        private void SetPlayerControlsLocked(bool locked)
        {
            if (locked == playerControlsLocked)
                return;

            if (locked)
            {
                var uniqueBehaviours = new HashSet<Behaviour>();
                var targets = new List<Behaviour>();

                if (gameplayInputBehaviours != null)
                {
                    foreach (Behaviour inputBehaviour in
                             gameplayInputBehaviours)
                    {
                        if (inputBehaviour == null ||
                            inputBehaviour == this ||
                            !uniqueBehaviours.Add(inputBehaviour))
                        {
                            continue;
                        }

                        targets.Add(inputBehaviour);
                    }
                }

                gatedBehaviourSnapshot = targets.ToArray();
                gatedBehaviourEnabledStates =
                    new bool[gatedBehaviourSnapshot.Length];

                playerControlsLocked = true;

                for (int index = 0;
                     index < gatedBehaviourSnapshot.Length;
                     index++)
                {
                    Behaviour inputBehaviour =
                        gatedBehaviourSnapshot[index];

                    try
                    {
                        gatedBehaviourEnabledStates[index] =
                            inputBehaviour.enabled;
                        inputBehaviour.enabled = false;
                    }
                    catch (Exception exception)
                    {

                    }
                }
                return;
            }

            int restoreCount = Mathf.Min(
                gatedBehaviourSnapshot.Length,
                gatedBehaviourEnabledStates.Length);

            for (int index = 0; index < restoreCount; index++)
            {
                Behaviour inputBehaviour = gatedBehaviourSnapshot[index];

                if (inputBehaviour != null)
                {
                    try
                    {
                        inputBehaviour.enabled =
                            gatedBehaviourEnabledStates[index];
                    }
                    catch (Exception exception)
                    {

                    }
                }
            }

            gatedBehaviourSnapshot = Array.Empty<Behaviour>();
            gatedBehaviourEnabledStates = Array.Empty<bool>();
            playerControlsLocked = false;
        }

        private void PublishObjective(
            string text,
            int current,
            int required)
        {
            string safeText = text ?? string.Empty;
            int safeCurrent = Mathf.Max(0, current);
            int safeRequired = Mathf.Max(0, required);

            if (string.Equals(
                    currentObjectiveText,
                    safeText,
                    StringComparison.Ordinal) &&
                currentObjectiveAmount == safeCurrent &&
                currentObjectiveRequired == safeRequired)
            {
                return;
            }

            currentObjectiveText = safeText;
            currentObjectiveAmount = safeCurrent;
            currentObjectiveRequired = safeRequired;
            FarmPrologueEventUtility.Invoke(
                objectiveTextChanged,
                safeText,
                this);
            FarmPrologueEventUtility.Invoke(
                objectiveProgressChanged,
                safeText,
                safeCurrent,
                safeRequired,
                this);
            FarmPrologueEventUtility.Invoke(
                ObjectiveTextChanged,
                safeText,
                this);
            FarmPrologueEventUtility.Invoke(
                ObjectiveProgressChanged,
                safeText,
                safeCurrent,
                safeRequired,
                this);
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (screenFader == null)
                yield break;

            float startAlpha = screenFader.alpha;

            if (duration <= 0f)
            {
                SetFaderInstantly(targetAlpha);
                yield break;
            }

            screenFader.gameObject.SetActive(true);
            screenFader.blocksRaycasts = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (screenFader == null)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                screenFader.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            SetFaderInstantly(targetAlpha);
        }

        private void SetFaderInstantly(float alpha)
        {
            if (screenFader == null)
                return;

            screenFader.alpha = Mathf.Clamp01(alpha);
            bool blocksInput = screenFader.alpha > 0.001f;
            screenFader.blocksRaycasts = blocksInput;
            screenFader.interactable = blocksInput;
        }

        private void RaisePrologueStartedOnce()
        {
            if (prologueStartedRaised)
                return;

            prologueStartedRaised = true;
            FarmPrologueEventUtility.Invoke(prologueStarted, this);
        }

        private void RaiseCombatStartedOnce()
        {
            if (combatStartedRaised)
                return;

            combatStartedRaised = true;
            FarmPrologueEventUtility.Invoke(combatStarted, this);
        }

        private void RaiseCombatCompletedOnce()
        {
            if (combatCompletedRaised)
                return;

            combatCompletedRaised = true;
            FarmPrologueEventUtility.Invoke(combatCompleted, this);
        }

        private void RaiseHubTransitionStartedOnce()
        {
            if (hubTransitionStartedRaised)
                return;

            hubTransitionStartedRaised = true;
            FarmPrologueEventUtility.Invoke(hubTransitionStarted, this);
        }

        private void RaiseHubUnlockedOnce()
        {
            if (hubUnlockedRaised)
                return;

            hubUnlockedRaised = true;
            FarmPrologueEventUtility.Invoke(hubUnlocked, this);
            FarmPrologueEventUtility.Invoke(HubUnlocked, this);
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            if (seconds <= 0f)
                yield break;

            yield return new WaitForSecondsRealtime(seconds);
        }

        private void ResumeCombatStartupIfNeeded()
        {
            if (CurrentPhase != FarmProloguePhase.Combat ||
                phaseRoutine != null ||
                waveEncounter == null ||
                waveEncounter.EncounterStarted)
            {
                return;
            }

            phaseRoutine = StartCoroutine(BeginCombatSequence());
        }

        private void StartGroundRumble()
        {
            StopGroundRumbleRoutine();
            SetGroundRumbleActive(true);
            groundRumbleRoutine =
                StartCoroutine(StopGroundRumbleAfterDelay());
        }

        private IEnumerator StopGroundRumbleAfterDelay()
        {
            yield return WaitRealtime(rumbleSeconds);
            groundRumbleRoutine = null;
            SetGroundRumbleActive(false);
        }

        private void StopGroundRumble()
        {
            StopGroundRumbleRoutine();
            SetGroundRumbleActive(false);
        }

        private void StopGroundRumbleRoutine()
        {
            if (groundRumbleRoutine == null)
                return;

            StopCoroutine(groundRumbleRoutine);
            groundRumbleRoutine = null;
        }

        private void SetGroundRumbleActive(bool active)
        {
            if (groundRumbleActive == active)
            {
                ApplyPhaseSceneState(CurrentPhase);
                return;
            }

            groundRumbleActive = active;

            if (active)
            {
                ApplyPhaseSceneState(CurrentPhase);
                FarmPrologueEventUtility.Invoke(
                    GroundRumbleStateChanged,
                    true,
                    this);
                FarmPrologueEventUtility.Invoke(rumbleStarted, this);
                return;
            }

            FarmPrologueEventUtility.Invoke(
                GroundRumbleStateChanged,
                false,
                this);
            ApplyPhaseSceneState(CurrentPhase);
        }

        private void StopPhaseRoutine()
        {
            if (phaseRoutine == null)
                return;

            StopCoroutine(phaseRoutine);
            phaseRoutine = null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
