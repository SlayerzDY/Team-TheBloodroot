using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.FarmPrologue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FarmChoreInteractable : MonoBehaviour, IInteract
    {
        [Header("Chore")]
        [SerializeField] private FarmPrologueDirector director;
        [SerializeField] private string choreId = "farm_chore";
        [SerializeField] private string objectiveText = "Complete the farm chore.";
        [SerializeField, Min(1)] private int requiredInteractions = 1;

        [Header("Optional Inventory Requirement")]
        [SerializeField] private bool requiresInventoryItem;
        [SerializeField] private string requiredItemName;
        [SerializeField, Min(1)] private int requiredItemsPerInteraction = 1;
        [SerializeField] private FarmInventoryConsumptionMode consumptionMode =
            FarmInventoryConsumptionMode.KeepItems;

        [Header("Authored Feedback Hooks")]
        [SerializeField] private FarmObjectiveProgressUnityEvent progressChanged = new();
        [SerializeField] private UnityEvent choreCompleted = new();
        [SerializeField] private FarmStringUnityEvent interactionRejected = new();

        private int completedInteractions;
        private bool isAvailable;

        public string ChoreId => choreId;
        public string ObjectiveText => objectiveText;
        public int CompletedInteractions => completedInteractions;
        public int RequiredInteractions => Mathf.Max(1, requiredInteractions);
        public bool IsComplete => completedInteractions >= RequiredInteractions;
        public bool IsAvailable => isAvailable && !IsComplete;
        public bool RequiresInventoryItem => requiresInventoryItem;

        public event Action<string, int, int> ProgressChanged;

        private void OnValidate()
        {
            requiredInteractions = Mathf.Max(1, requiredInteractions);
            requiredItemsPerInteraction =
                Mathf.Max(1, requiredItemsPerInteraction);
        }

        public void SendInteract(Collider target)
        {
            if (!IsAvailable)
            {
                RejectAndNotifyDirector(
                    "This chore is not currently available.");
                return;
            }

            if (director == null)
            {
                RejectInteraction("This chore has no Farm Prologue Director assigned.");
                return;
            }

            director.TryPerformChore(this);
        }

        public void SetDirector(FarmPrologueDirector owner)
        {
            director = owner;
        }

        public void Configure(
            FarmPrologueDirector owner,
            string id,
            string objective,
            int interactionCount)
        {
            director = owner;
            choreId = string.IsNullOrWhiteSpace(id)
                ? gameObject.name
                : id.Trim();
            objectiveText = string.IsNullOrWhiteSpace(objective)
                ? "Complete the farm chore."
                : objective.Trim();
            requiredInteractions = Mathf.Max(1, interactionCount);
        }

        public void ConfigureInventoryRequirement(
            bool required,
            string itemName,
            int quantityPerInteraction,
            FarmInventoryConsumptionMode consumption)
        {
            requiresInventoryItem = required;
            requiredItemName = itemName?.Trim() ?? string.Empty;
            requiredItemsPerInteraction =
                Mathf.Max(1, quantityPerInteraction);
            consumptionMode = consumption;
        }

        public void ResetChoreProgress()
        {
            completedInteractions = 0;
            isAvailable = false;
            RaiseProgressChanged();
        }

        /// <summary>
        /// Restores the only durable post-chore state currently represented by
        /// campaign progress. A revealed prologue cursed object proves that
        /// every chore completed before the save, so a Farm reload must not
        /// visually roll those chores back to their pending state.
        /// </summary>
        internal void RestoreCompletedProgress()
        {
            completedInteractions = RequiredInteractions;
            isAvailable = false;
            RaiseProgressChanged();
        }

        internal void SetAvailable(bool available)
        {
            isAvailable = available && !IsComplete;
        }

        internal bool TryApplyInteraction(
            Inventory inventory,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (!IsAvailable)
            {
                failureReason = "This chore is not currently available.";
                return false;
            }

            if (!TryUseRequiredInventoryItems(inventory, out failureReason))
            {
                return false;
            }

            completedInteractions =
                Mathf.Min(completedInteractions + 1, RequiredInteractions);

            RaiseProgressChanged();

            if (IsComplete)
            {
                isAvailable = false;
                FarmPrologueEventUtility.Invoke(choreCompleted, this);
            }

            return true;
        }

        private void RaiseProgressChanged()
        {
            FarmPrologueEventUtility.Invoke(
                progressChanged,
                objectiveText,
                completedInteractions,
                RequiredInteractions,
                this);
            FarmPrologueEventUtility.Invoke(
                ProgressChanged,
                objectiveText,
                completedInteractions,
                RequiredInteractions,
                this);
        }

        internal void RejectInteraction(string reason)
        {
            FarmPrologueEventUtility.Invoke(
                interactionRejected,
                reason ?? string.Empty,
                this);
        }

        private void RejectAndNotifyDirector(string reason)
        {
            if (director != null)
            {
                director.ReportChoreInteractionRejected(this, reason);
                return;
            }

            RejectInteraction(reason);
        }

        private bool TryUseRequiredInventoryItems(
            Inventory inventory,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (!requiresInventoryItem)
                return true;

            if (inventory == null)
            {
                failureReason =
                    "This chore requires an assigned player Inventory.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(requiredItemName))
            {
                failureReason =
                    "This chore has no required inventory item name configured.";
                return false;
            }

            ItemStats itemQuery = new ItemStats
            {
                itemName = requiredItemName
            };

            if (!inventory.CheckItem(itemQuery))
            {
                failureReason = $"Requires {requiredItemName}.";
                return false;
            }

            var foundItem = inventory.FindItem(itemQuery);

            if (foundItem.Key == null ||
                foundItem.Value < requiredItemsPerInteraction)
            {
                failureReason =
                    $"Requires {requiredItemsPerInteraction} " +
                    $"{requiredItemName}.";
                return false;
            }

            switch (consumptionMode)
            {
                case FarmInventoryConsumptionMode.ConsumeRequiredQuantity:
                    inventory.RemoveItem(
                        requiredItemName,
                        requiredItemsPerInteraction,
                        false);
                    break;

                case FarmInventoryConsumptionMode.ConsumeAllMatchingItems:
                    if (!inventory.RemoveMultipleItems(
                            requiredItemName,
                            false))
                    {
                        failureReason =
                            $"Could not consume {requiredItemName}.";
                        return false;
                    }
                    break;
            }

            return true;
        }
    }
}
