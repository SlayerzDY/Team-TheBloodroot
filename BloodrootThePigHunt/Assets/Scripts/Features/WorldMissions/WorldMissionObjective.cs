using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Base for an explicitly authored mission objective. Objectives never
    /// search the scene or create presentation objects; the director owns
    /// availability and ordered progression.
    /// </summary>
    public abstract class WorldMissionObjective : MonoBehaviour
    {
        [Header("Objective")]
        [SerializeField] private string objectiveId = "objective";
        [SerializeField, TextArea] private string objectiveText =
            "Complete the objective.";
        [SerializeField] private bool optional;

        [Header("Authored Lifecycle Hooks")]
        [SerializeField] private UnityEvent objectiveAvailable = new();
        [SerializeField] private WorldMissionObjectiveStateUnityEvent
            stateChanged = new();
        [SerializeField] private WorldMissionObjectiveProgressUnityEvent
            progressChanged = new();
        [SerializeField] private UnityEvent objectiveCompleted = new();
        [SerializeField] private UnityEvent objectiveDeactivated = new();
        [SerializeField] private WorldMissionStringUnityEvent
            actionRejected = new();

        private WorldMissionDirector director;
        private WorldMissionObjectiveState state =
            WorldMissionObjectiveState.Inactive;

        public string ObjectiveId => string.IsNullOrWhiteSpace(objectiveId)
            ? gameObject.name
            : objectiveId.Trim();

        public string ObjectiveText => objectiveText?.Trim() ?? string.Empty;
        public bool IsOptional => optional;
        public WorldMissionDirector Director => director;
        public WorldMissionObjectiveState State => state;
        public bool IsAvailable => state == WorldMissionObjectiveState.Available;
        public bool IsComplete => state == WorldMissionObjectiveState.Completed;
        public abstract int CurrentAmount { get; }
        public abstract int RequiredAmount { get; }

        public event Action<WorldMissionObjective> StateChanged;
        public event Action<WorldMissionObjective> ProgressChanged;
        public event Action<WorldMissionObjective> Completed;
        public event Action<string> ActionRejected;

        public void ConfigureDefinition(
            string id,
            string text,
            bool isOptional)
        {
            objectiveId = string.IsNullOrWhiteSpace(id)
                ? gameObject.name
                : id.Trim();
            objectiveText = text?.Trim() ?? string.Empty;
            optional = isOptional;
        }

        /// <summary>
        /// UnityEvent-friendly completion request. This cannot bypass the
        /// subclass's completion requirements or the director's availability.
        /// </summary>
        public void CompleteIfReady()
        {
            TryCompleteObjective();
        }

        public bool TryCompleteObjective()
        {
            if (!IsAvailable)
            {
                RejectAction("This objective is not currently available.");
                return false;
            }

            if (!CanComplete(out string reason))
            {
                RejectAction(string.IsNullOrWhiteSpace(reason)
                    ? "This objective is not complete yet."
                    : reason);
                return false;
            }

            return CompleteObjectiveCore();
        }

        internal void PrepareForMission(WorldMissionDirector owner)
        {
            director = owner;
            ResetObjectiveProgress();
            OnPreparedForMission();
            SetState(WorldMissionObjectiveState.Inactive, true);
            NotifyProgressChanged();
        }

        internal bool ActivateObjective()
        {
            if (state != WorldMissionObjectiveState.Inactive)
            {
                return false;
            }

            SetState(WorldMissionObjectiveState.Available, true);
            OnObjectiveActivated();
            NotifyProgressChanged();
            WorldMissionEventUtility.Invoke(objectiveAvailable, this);
            return true;
        }

        internal void DeactivateObjective()
        {
            if (state != WorldMissionObjectiveState.Available)
                return;

            OnObjectiveDeactivated();
            SetState(WorldMissionObjectiveState.Inactive, true);
            WorldMissionEventUtility.Invoke(objectiveDeactivated, this);
        }

        protected void NotifyProgressChanged()
        {
            int current = Mathf.Max(0, CurrentAmount);
            int required = Mathf.Max(0, RequiredAmount);

            WorldMissionEventUtility.Invoke(
                progressChanged,
                ObjectiveText,
                current,
                required,
                this);
            WorldMissionEventUtility.Invoke(ProgressChanged, this, this);
        }

        protected void RejectAction(string reason)
        {
            string message = string.IsNullOrWhiteSpace(reason)
                ? "That action cannot be completed right now."
                : reason.Trim();

            WorldMissionEventUtility.Invoke(actionRejected, message, this);
            WorldMissionEventUtility.Invoke(ActionRejected, message, this);
        }

        /// <summary>
        /// Polling-friendly completion check for timer and encounter objectives.
        /// Unlike the public request API, unmet requirements do not emit a
        /// rejection every frame or after every incremental kill.
        /// </summary>
        protected bool TryCompleteWhenReady()
        {
            return IsAvailable &&
                   CanComplete(out _) &&
                   CompleteObjectiveCore();
        }

        protected virtual bool CanComplete(out string reason)
        {
            bool complete = RequiredAmount <= 0 ||
                            CurrentAmount >= RequiredAmount;
            reason = complete
                ? string.Empty
                : $"Requires {RequiredAmount - CurrentAmount} more.";
            return complete;
        }

        protected virtual void OnPreparedForMission()
        {
        }

        protected virtual void OnObjectiveActivated()
        {
        }

        protected virtual void OnObjectiveCompleted()
        {
        }

        protected virtual void OnObjectiveDeactivated()
        {
        }

        protected abstract void ResetObjectiveProgress();

        private bool CompleteObjectiveCore()
        {
            if (!IsAvailable)
                return false;

            SetState(WorldMissionObjectiveState.Completed, true);
            OnObjectiveCompleted();
            NotifyProgressChanged();
            WorldMissionEventUtility.Invoke(objectiveCompleted, this);
            WorldMissionEventUtility.Invoke(Completed, this, this);
            return true;
        }

        private void SetState(
            WorldMissionObjectiveState nextState,
            bool notifyWhenUnchanged)
        {
            bool changed = state != nextState;
            state = nextState;

            if (!changed && !notifyWhenUnchanged)
                return;

            WorldMissionEventUtility.Invoke(stateChanged, state, this);
            WorldMissionEventUtility.Invoke(StateChanged, this, this);
        }
    }
}
