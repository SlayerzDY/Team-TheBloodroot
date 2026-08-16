using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Counts authored interaction sources for relay restoration, sample
    /// recovery, power, cursed objects, the Hollow altar, Heartroot, or
    /// extraction. It deliberately contains no inventory implementation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionInteractionObjective :
        WorldMissionObjective
    {
        [Header("Interaction")]
        [SerializeField] private WorldMissionInteractionKind interactionKind =
            WorldMissionInteractionKind.Generic;
        [SerializeField, Min(1)] private int requiredInteractions = 1;
        [SerializeField] private WorldMissionGameObjectUnityEvent
            interactionAccepted = new();
        [SerializeField] private UnityEvent requirementReached = new();

        private int completedInteractions;

        public WorldMissionInteractionKind InteractionKind => interactionKind;
        public override int CurrentAmount => completedInteractions;
        public override int RequiredAmount =>
            Mathf.Max(1, requiredInteractions);

        private void OnValidate()
        {
            requiredInteractions = Mathf.Max(1, requiredInteractions);
        }

        public void ConfigureInteraction(
            WorldMissionInteractionKind kind,
            int interactionCount)
        {
            interactionKind = kind;
            requiredInteractions = Mathf.Max(1, interactionCount);
        }

        public void RegisterInteraction()
        {
            TryRegisterInteraction(null, null, out _);
        }

        public bool TryRegisterInteraction()
        {
            return TryRegisterInteraction(null, null, out _);
        }

        public bool TryRegisterInteraction(
            WorldMissionInteractionSource source,
            Collider interactionCollider,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;

            if (IsComplete)
            {
                rejectionReason = "This objective is already complete.";
                RejectAction(rejectionReason);
                return false;
            }

            if (!IsAvailable)
            {
                rejectionReason =
                    "This mission interaction is not currently available.";
                RejectAction(rejectionReason);
                return false;
            }

            if (completedInteractions >= RequiredAmount)
            {
                rejectionReason = "This objective is already complete.";
                RejectAction(rejectionReason);
                return false;
            }

            completedInteractions = Mathf.Min(
                completedInteractions + 1,
                RequiredAmount);

            GameObject sourceObject = source != null
                ? source.gameObject
                : interactionCollider != null
                    ? interactionCollider.gameObject
                    : null;

            WorldMissionEventUtility.Invoke(
                interactionAccepted,
                sourceObject,
                this);
            NotifyProgressChanged();

            if (completedInteractions < RequiredAmount)
            {
                return true;
            }

            WorldMissionEventUtility.Invoke(requirementReached, this);
            return TryCompleteObjective();
        }

        public void RestoreRelay()
        {
            RegisterSemanticInteraction(
                WorldMissionInteractionKind.RelayRestoration,
                "relay restoration");
        }

        public void RecoverSample()
        {
            RegisterSemanticInteraction(
                WorldMissionInteractionKind.SampleRecovery,
                "sample recovery");
        }

        public void RestorePower()
        {
            RegisterSemanticInteraction(
                WorldMissionInteractionKind.PowerRestoration,
                "power restoration");
        }

        public void ResolveCursedObject()
        {
            RegisterSemanticInteraction(
                WorldMissionInteractionKind.CursedObject,
                "cursed-object resolution");
        }

        public void ActivateAltar()
        {
            RegisterSemanticInteraction(
                WorldMissionInteractionKind.AltarActivation,
                "altar activation");
        }

        public void RecoverHeartroot()
        {
            RegisterSemanticInteraction(
                WorldMissionInteractionKind.HeartrootRecovery,
                "Heartroot recovery");
        }

        public void Extract()
        {
            RegisterSemanticInteraction(
                WorldMissionInteractionKind.Extraction,
                "extraction");
        }

        protected override void ResetObjectiveProgress()
        {
            completedInteractions = 0;
        }

        private void RegisterSemanticInteraction(
            WorldMissionInteractionKind expectedKind,
            string actionName)
        {
            if (interactionKind != expectedKind)
            {
                RejectAction(
                    $"Cannot register {actionName}: this objective is " +
                    $"configured as {interactionKind}.");
                return;
            }

            TryRegisterInteraction();
        }
    }
}
