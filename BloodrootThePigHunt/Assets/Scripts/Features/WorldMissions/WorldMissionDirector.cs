using System;
using System.Collections;
using System.Collections.Generic;
using Bloodroot.Campaign;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Runs an explicitly authored objective sequence and calls the campaign
    /// completion relay only after every required objective succeeds. Optional
    /// objectives activate at their array position but never block progression.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionDirector : MonoBehaviour
    {
        [Header("Mission")]
        [SerializeField] private bool beginOnEnable = true;
        [SerializeField] private WorldMissionObjective[] orderedObjectives =
            Array.Empty<WorldMissionObjective>();
        [SerializeField] private CampaignAreaCompletionRelay completionRelay;

        [Header("Completion Save Recovery")]
        [SerializeField, Min(0.1f)]
        private float completionRetryInitialSeconds = 1f;
        [SerializeField, Min(0.1f)]
        private float completionRetryMaximumSeconds = 15f;

        [Header("Authored Mission Hooks")]
        [SerializeField] private UnityEvent missionStarted = new();
        [SerializeField] private WorldMissionStateUnityEvent
            missionStateChanged = new();
        [SerializeField] private WorldMissionObjectiveUnityEvent
            objectiveActivated = new();
        [SerializeField] private WorldMissionObjectiveUnityEvent
            objectiveCompleted = new();
        [SerializeField] private WorldMissionObjectiveUnityEvent
            currentObjectiveChanged = new();
        [SerializeField] private WorldMissionObjectiveProgressUnityEvent
            objectiveProgressChanged = new();
        [SerializeField] private WorldMissionStringUnityEvent
            interactionRejected = new();
        [SerializeField] private UnityEvent completionPending = new();
        [SerializeField] private UnityEvent campaignCompletionRejected = new();
        [SerializeField] private UnityEvent missionCompleted = new();

        private WorldMissionState state = WorldMissionState.Inactive;
        private WorldMissionObjective currentObjective;
        private int sequenceCursor;
        private bool objectivesBound;
        private bool advancingSequence;
        private bool advanceRequested;
        private Coroutine completionRetryRoutine;
        private int completionRetryAttempt;
        private CampaignStateService boundCampaignState;
        private gameManager boundGameManager;

        public WorldMissionState State => state;
        public WorldMissionObjective CurrentObjective => currentObjective;
        public string CurrentObjectiveText =>
            currentObjective != null
                ? currentObjective.ObjectiveText
                : string.Empty;
        public int CurrentObjectiveAmount =>
            currentObjective != null
                ? currentObjective.CurrentAmount
                : 0;
        public int CurrentObjectiveRequired =>
            currentObjective != null
                ? currentObjective.RequiredAmount
                : 0;
        public IReadOnlyList<WorldMissionObjective> OrderedObjectives =>
            orderedObjectives ?? Array.Empty<WorldMissionObjective>();
        public CampaignAreaCompletionRelay CompletionRelay => completionRelay;

        public event Action MissionStarted;
        public event Action<WorldMissionState> MissionStateChanged;
        public event Action<WorldMissionObjective> ObjectiveActivated;
        public event Action<WorldMissionObjective> ObjectiveCompleted;
        public event Action<WorldMissionObjective> CurrentObjectiveChanged;
        public event Action<string> ObjectiveTextChanged;
        public event Action<string, int, int> ObjectiveProgressChanged;
        public event Action<string> InteractionRejected;
        public event Action CampaignCompletionRejected;
        public event Action MissionCompleted;

        private void OnEnable()
        {
            BindObjectives();
            BindLifecycleEvents();

            if (beginOnEnable && state == WorldMissionState.Inactive)
            {
                TryBeginMission();
            }

            if (state == WorldMissionState.CompletionPending)
            {
                ScheduleCompletionRetry();
            }
        }

        private void OnDisable()
        {
            CancelCompletionRetry(false);
            UnbindObjectives();
            UnbindLifecycleEvents();
        }

        private void OnValidate()
        {
            completionRetryInitialSeconds =
                Mathf.Max(0.1f, completionRetryInitialSeconds);
            completionRetryMaximumSeconds =
                Mathf.Max(0.1f, completionRetryMaximumSeconds);
        }

        public void BeginMission()
        {
            TryBeginMission();
        }

        public bool TryBeginMission()
        {
            if (state == WorldMissionState.Running ||
                state == WorldMissionState.CompletionPending)
            {
                return false;
            }

            if (state == WorldMissionState.Completed)
            {
                return true;
            }

            if (!ValidateConfiguration(out string problem))
            {

                ForwardRejection(problem);
                return false;
            }

            BindObjectives();
            completionRetryAttempt = 0;
            sequenceCursor = 0;
            currentObjective = null;

            foreach (WorldMissionObjective objective in orderedObjectives)
            {
                objective.PrepareForMission(this);
            }

            SetState(WorldMissionState.Running);
            WorldMissionEventUtility.Invoke(missionStarted, this);
            WorldMissionEventUtility.Invoke(MissionStarted, this);
            AdvanceSequence();
            return true;
        }

        public void RetryCampaignCompletion()
        {
            TryRetryCampaignCompletion();
        }

        public bool TryRetryCampaignCompletion()
        {
            if (state != WorldMissionState.CompletionPending)
            {
                ForwardRejection(
                    "Campaign completion is not currently pending.");
                return false;
            }

            return TryFinalizeMission();
        }

        public void ResetMission()
        {
            CancelCompletionRetry(true);
            BindObjectives();

            if (orderedObjectives != null)
            {
                foreach (WorldMissionObjective objective in orderedObjectives)
                {
                    if (objective == null)
                        continue;

                    objective.DeactivateObjective();
                    objective.PrepareForMission(this);
                }
            }

            sequenceCursor = 0;
            currentObjective = null;
            SetState(WorldMissionState.Inactive);
            PublishCurrentObjective();
        }

        public void Configure(
            CampaignAreaCompletionRelay campaignCompletionRelay,
            WorldMissionObjective[] objectives,
            bool autoBegin)
        {
            CancelCompletionRetry(true);
            UnbindObjectives();
            completionRelay = campaignCompletionRelay;
            orderedObjectives = objectives ??
                Array.Empty<WorldMissionObjective>();
            beginOnEnable = autoBegin;
            objectivesBound = false;

            if (isActiveAndEnabled)
            {
                BindObjectives();
            }
        }

        private void AdvanceSequence()
        {
            if (advancingSequence)
            {
                advanceRequested = true;
                return;
            }

            do
            {
                advanceRequested = false;
                advancingSequence = true;
                AdvanceSequenceCore();
                advancingSequence = false;
            }
            while (advanceRequested && state == WorldMissionState.Running);
        }

        private void AdvanceSequenceCore()
        {
            if (state != WorldMissionState.Running)
                return;

            while (sequenceCursor < orderedObjectives.Length)
            {
                WorldMissionObjective objective =
                    orderedObjectives[sequenceCursor++];

                if (!objective.IsOptional)
                {
                    // Establish ownership before activation. A defense-start
                    // hook may synchronously spawn and resolve an encounter;
                    // its completion must still be recognized as the current
                    // required step.
                    currentObjective = objective;
                    PublishCurrentObjective();
                }

                if (!objective.ActivateObjective())
                {
                    if (currentObjective == objective)
                    {
                        currentObjective = null;
                        PublishCurrentObjective();
                    }

                    string reason =
                        $"Objective '{objective.ObjectiveId}' could not be " +
                        "activated from its current state.";

                    ForwardRejection(reason);
                    return;
                }

                WorldMissionEventUtility.Invoke(
                    objectiveActivated,
                    objective,
                    this);
                WorldMissionEventUtility.Invoke(
                    ObjectiveActivated,
                    objective,
                    this);

                if (objective.IsOptional)
                {
                    continue;
                }

                return;
            }

            currentObjective = null;
            PublishCurrentObjective();
            TryFinalizeMission();
        }

        private bool TryFinalizeMission()
        {
            if (completionRelay == null)
            {
                SetState(WorldMissionState.CompletionPending);
                RejectCampaignCompletion(
                    "Mission has no CampaignAreaCompletionRelay assigned.");
                ScheduleCompletionRetry();
                return false;
            }

            SetState(WorldMissionState.CompletionPending);
            WorldMissionEventUtility.Invoke(completionPending, this);

            if (!completionRelay.TryCompleteArea())
            {
                RejectCampaignCompletion(
                    "Campaign progression rejected mission completion.");
                ScheduleCompletionRetry();
                return false;
            }

            DeactivateIncompleteOptionalObjectives();
            completionRetryAttempt = 0;
            SetState(WorldMissionState.Completed);
            WorldMissionEventUtility.Invoke(missionCompleted, this);
            WorldMissionEventUtility.Invoke(MissionCompleted, this);
            return true;
        }

        private void ScheduleCompletionRetry()
        {
            BindLifecycleEvents();

            if (!isActiveAndEnabled ||
                completionRetryRoutine != null ||
                state != WorldMissionState.CompletionPending)
            {
                return;
            }

            completionRetryRoutine =
                StartCoroutine(RetryCampaignCompletionAfterDelay());
        }

        private IEnumerator RetryCampaignCompletionAfterDelay()
        {
            float retryDelay = CalculateCompletionRetryDelay(
                completionRetryAttempt,
                completionRetryInitialSeconds,
                completionRetryMaximumSeconds);
            completionRetryAttempt =
                Mathf.Min(completionRetryAttempt + 1, 10);
            yield return new WaitForSecondsRealtime(retryDelay);
            completionRetryRoutine = null;

            if (!isActiveAndEnabled ||
                state != WorldMissionState.CompletionPending)
            {
                yield break;
            }

            TryFinalizeMission();
        }

        private static float CalculateCompletionRetryDelay(
            int retryAttempt,
            float initialDelay,
            float maximumDelay)
        {
            float safeInitial = Mathf.Max(0.1f, initialDelay);
            float safeMaximum = Mathf.Max(0.1f, maximumDelay);
            int exponent = Mathf.Clamp(retryAttempt, 0, 10);
            return Mathf.Min(
                safeMaximum,
                safeInitial * Mathf.Pow(2f, exponent));
        }

        private void CancelCompletionRetry(bool resetBackoff)
        {
            if (completionRetryRoutine != null)
            {
                StopCoroutine(completionRetryRoutine);
                completionRetryRoutine = null;
            }

            if (resetBackoff)
            {
                completionRetryAttempt = 0;
            }
        }

        private void BindLifecycleEvents()
        {
            CampaignStateService resolvedState =
                CampaignStateService.Instance;

            if (boundCampaignState != resolvedState)
            {
                if (boundCampaignState != null)
                {
                    boundCampaignState.NewGameStarted -=
                        HandleNewGameStarted;
                }

                boundCampaignState = resolvedState;

                if (boundCampaignState != null)
                {
                    boundCampaignState.NewGameStarted +=
                        HandleNewGameStarted;
                }
            }

            gameManager resolvedGameManager = gameManager.instance;

            if (boundGameManager == resolvedGameManager)
                return;

            if (boundGameManager != null)
            {
                boundGameManager.PlayerLost -= HandlePlayerLost;
                boundGameManager.PlayerRespawned -= HandlePlayerRespawned;
            }

            boundGameManager = resolvedGameManager;

            if (boundGameManager != null)
            {
                boundGameManager.PlayerLost += HandlePlayerLost;
                boundGameManager.PlayerRespawned += HandlePlayerRespawned;
            }
        }

        private void UnbindLifecycleEvents()
        {
            if (boundCampaignState != null)
            {
                boundCampaignState.NewGameStarted -= HandleNewGameStarted;
                boundCampaignState = null;
            }

            if (boundGameManager != null)
            {
                boundGameManager.PlayerLost -= HandlePlayerLost;
                boundGameManager.PlayerRespawned -= HandlePlayerRespawned;
                boundGameManager = null;
            }
        }

        private void HandleNewGameStarted()
        {
            ResetMission();
        }

        private void HandlePlayerLost()
        {
            CancelCompletionRetry(false);
        }

        private void HandlePlayerRespawned()
        {
            if (state == WorldMissionState.CompletionPending)
            {
                ScheduleCompletionRetry();
            }
        }

        private void HandleObjectiveCompleted(
            WorldMissionObjective objective)
        {
            WorldMissionEventUtility.Invoke(
                objectiveCompleted,
                objective,
                this);
            WorldMissionEventUtility.Invoke(
                ObjectiveCompleted,
                objective,
                this);

            if (objective.IsOptional)
                return;

            if (objective != currentObjective)
            {

                return;
            }

            currentObjective = null;
            AdvanceSequence();
        }

        private void HandleObjectiveProgressed(
            WorldMissionObjective objective)
        {
            if (objective != currentObjective)
                return;

            PublishCurrentObjective();
        }

        private void HandleObjectiveRejected(string reason)
        {
            ForwardRejection(reason);
        }

        private void PublishCurrentObjective()
        {
            WorldMissionEventUtility.Invoke(
                currentObjectiveChanged,
                currentObjective,
                this);
            WorldMissionEventUtility.Invoke(
                CurrentObjectiveChanged,
                currentObjective,
                this);
            WorldMissionEventUtility.Invoke(
                ObjectiveTextChanged,
                CurrentObjectiveText,
                this);
            WorldMissionEventUtility.Invoke(
                objectiveProgressChanged,
                CurrentObjectiveText,
                CurrentObjectiveAmount,
                CurrentObjectiveRequired,
                this);
            WorldMissionEventUtility.Invoke(
                ObjectiveProgressChanged,
                CurrentObjectiveText,
                CurrentObjectiveAmount,
                CurrentObjectiveRequired,
                this);
        }

        private void ForwardRejection(string reason)
        {
            string message = string.IsNullOrWhiteSpace(reason)
                ? "That mission action cannot be completed right now."
                : reason.Trim();
            WorldMissionEventUtility.Invoke(
                interactionRejected,
                message,
                this);
            WorldMissionEventUtility.Invoke(
                InteractionRejected,
                message,
                this);
        }

        private void RejectCampaignCompletion(string reason)
        {

            ForwardRejection(reason);
            WorldMissionEventUtility.Invoke(
                campaignCompletionRejected,
                this);
            WorldMissionEventUtility.Invoke(
                CampaignCompletionRejected,
                this);
        }

        private void DeactivateIncompleteOptionalObjectives()
        {
            foreach (WorldMissionObjective objective in orderedObjectives)
            {
                if (objective != null &&
                    objective.IsOptional &&
                    objective.IsAvailable)
                {
                    objective.DeactivateObjective();
                }
            }
        }

        private void SetState(WorldMissionState nextState)
        {
            if (state == nextState)
                return;

            state = nextState;
            WorldMissionEventUtility.Invoke(
                missionStateChanged,
                state,
                this);
            WorldMissionEventUtility.Invoke(
                MissionStateChanged,
                state,
                this);
        }

        private void BindObjectives()
        {
            if (objectivesBound || orderedObjectives == null)
                return;

            foreach (WorldMissionObjective objective in orderedObjectives)
            {
                if (objective == null)
                    continue;

                objective.Completed -= HandleObjectiveCompleted;
                objective.ProgressChanged -= HandleObjectiveProgressed;
                objective.ActionRejected -= HandleObjectiveRejected;
                objective.Completed += HandleObjectiveCompleted;
                objective.ProgressChanged += HandleObjectiveProgressed;
                objective.ActionRejected += HandleObjectiveRejected;
            }

            objectivesBound = true;
        }

        private void UnbindObjectives()
        {
            if (!objectivesBound || orderedObjectives == null)
                return;

            foreach (WorldMissionObjective objective in orderedObjectives)
            {
                if (objective == null)
                    continue;

                objective.Completed -= HandleObjectiveCompleted;
                objective.ProgressChanged -= HandleObjectiveProgressed;
                objective.ActionRejected -= HandleObjectiveRejected;
            }

            objectivesBound = false;
        }

        private bool ValidateConfiguration(out string problem)
        {
            problem = string.Empty;

            if (completionRelay == null)
            {
                problem =
                    "World mission requires a CampaignAreaCompletionRelay.";
                return false;
            }

            if (orderedObjectives == null || orderedObjectives.Length == 0)
            {
                problem = "World mission has no ordered objectives.";
                return false;
            }

            HashSet<WorldMissionObjective> seenObjectives = new();
            HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
            bool hasRequiredObjective = false;

            for (int index = 0; index < orderedObjectives.Length; index++)
            {
                WorldMissionObjective objective = orderedObjectives[index];

                if (objective == null)
                {
                    problem =
                        $"World mission objective {index} is not assigned.";
                    return false;
                }

                if (!seenObjectives.Add(objective))
                {
                    problem =
                        $"Objective '{objective.ObjectiveId}' is assigned more " +
                        "than once.";
                    return false;
                }

                if (!seenIds.Add(objective.ObjectiveId))
                {
                    problem =
                        $"Objective id '{objective.ObjectiveId}' is duplicated.";
                    return false;
                }

                hasRequiredObjective |= !objective.IsOptional;
            }

            if (!hasRequiredObjective)
            {
                problem =
                    "World mission must contain at least one required objective.";
                return false;
            }

            if (orderedObjectives[orderedObjectives.Length - 1].IsOptional)
            {
                problem =
                    "The final ordered objective cannot be optional because " +
                    "mission completion would immediately deactivate it.";
                return false;
            }

            return true;
        }
    }
}
