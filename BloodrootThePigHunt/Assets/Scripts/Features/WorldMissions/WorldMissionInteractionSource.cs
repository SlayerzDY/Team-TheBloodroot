using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Authored IInteract bridge. Multiple source objects can feed one shared
    /// interaction objective without placing mission logic on props.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WorldMissionInteractionSource :
        MonoBehaviour,
        IInteract
    {
        [SerializeField] private WorldMissionInteractionObjective objective;
        [SerializeField] private bool singleUse = true;

        [Header("Authored Feedback Hooks")]
        [SerializeField] private UnityEvent interactionAccepted = new();
        [SerializeField] private WorldMissionStringUnityEvent
            interactionRejected = new();

        private bool consumed;
        private WorldMissionInteractionObjective boundObjective;

        public WorldMissionInteractionObjective Objective => objective;
        public bool IsConsumed => consumed;

        private void OnEnable()
        {
            BindObjective();
        }

        private void OnDisable()
        {
            UnbindObjective();
        }

        public void SendInteract(Collider target)
        {
            if (singleUse && consumed)
            {
                Reject("This interaction has already been completed.");
                return;
            }

            if (objective == null)
            {
                Reject("This interaction has no mission objective assigned.");
                return;
            }

            if (!objective.TryRegisterInteraction(
                    this,
                    target,
                    out string rejectionReason))
            {
                Reject(rejectionReason);
                return;
            }

            consumed = singleUse;
            WorldMissionEventUtility.Invoke(interactionAccepted, this);
        }

        public void ResetSource()
        {
            consumed = false;
        }

        public void Configure(
            WorldMissionInteractionObjective targetObjective,
            bool consumeAfterUse)
        {
            UnbindObjective();
            objective = targetObjective;
            singleUse = consumeAfterUse;
            consumed = false;

            if (isActiveAndEnabled)
            {
                BindObjective();
            }
        }

        private void BindObjective()
        {
            if (boundObjective == objective)
                return;

            UnbindObjective();
            boundObjective = objective;

            if (boundObjective != null)
            {
                boundObjective.StateChanged += HandleObjectiveStateChanged;

                if (boundObjective.State ==
                    WorldMissionObjectiveState.Inactive)
                {
                    consumed = false;
                }
            }
        }

        private void UnbindObjective()
        {
            if (boundObjective != null)
            {
                boundObjective.StateChanged -= HandleObjectiveStateChanged;
            }

            boundObjective = null;
        }

        private void HandleObjectiveStateChanged(
            WorldMissionObjective changedObjective)
        {
            if (changedObjective != null &&
                changedObjective.State == WorldMissionObjectiveState.Inactive)
            {
                consumed = false;
            }
        }

        private void Reject(string reason)
        {
            string message = string.IsNullOrWhiteSpace(reason)
                ? "That mission interaction is unavailable."
                : reason.Trim();
            WorldMissionEventUtility.Invoke(
                interactionRejected,
                message,
                this);
        }
    }
}
