using System;
using System.Collections.Generic;
using Bloodroot.Campaign;
using UnityEngine;

namespace Bloodroot.Features.Hub
{
    [Serializable]
    public sealed class HubLoadoutItem
    {
        [SerializeField] private GameObject itemPickupPrefab;
        [SerializeField, Min(1)] private int quantity = 1;

        public GameObject ItemPickupPrefab => itemPickupPrefab;
        public int Quantity => Mathf.Max(1, quantity);

        public void Configure(GameObject pickupPrefab, int amount)
        {
            itemPickupPrefab = pickupPrefab;
            quantity = Mathf.Max(1, amount);
        }
    }

    [Serializable]
    public sealed class HubLoadoutDefinition
    {
        [SerializeField] private string loadoutId = "farm_loadout";
        [SerializeField] private string displayName = "Farm Loadout";
        [SerializeField] private HubLoadoutItem[] items =
            Array.Empty<HubLoadoutItem>();

        public string LoadoutId => string.IsNullOrWhiteSpace(loadoutId)
            ? "farm_loadout"
            : loadoutId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? LoadoutId
            : displayName.Trim();
        public IReadOnlyList<HubLoadoutItem> Items =>
            items ?? Array.Empty<HubLoadoutItem>();

        public void Configure(
            string id,
            string label,
            HubLoadoutItem[] loadoutItems)
        {
            loadoutId = string.IsNullOrWhiteSpace(id)
                ? "farm_loadout"
                : id.Trim();
            displayName = string.IsNullOrWhiteSpace(label)
                ? loadoutId
                : label.Trim();
            items = loadoutItems ?? Array.Empty<HubLoadoutItem>();
        }
    }

    /// <summary>
    /// Supplies authored minimum-quantity loadout presets using only
    /// Inventory's public API. The complete grant is capacity-preflighted
    /// before the first item is added, so a rejected preset cannot partially
    /// mutate the player's inventory or trigger Inventory's world-drop
    /// fallback. Existing non-preset items are intentionally preserved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubLoadoutStation : MonoBehaviour
    {
        [SerializeField] private HubStationProgression progression;
        [SerializeField] private Inventory playerInventory;
        [SerializeField] private CampaignInventoryCarryover inventoryCarryover;
        [SerializeField] private HubLoadoutDefinition[] loadouts =
            Array.Empty<HubLoadoutDefinition>();
        [SerializeField, Min(0)] private int selectedLoadoutIndex;
        [SerializeField] private HubStringUnityEvent loadoutSelected = new();
        [SerializeField] private HubLoadoutResultUnityEvent loadoutApplied = new();

        public IReadOnlyList<HubLoadoutDefinition> Loadouts =>
            loadouts ?? Array.Empty<HubLoadoutDefinition>();
        public int SelectedLoadoutIndex => selectedLoadoutIndex;
        public Inventory PlayerInventory => playerInventory;
        public CampaignInventoryCarryover InventoryCarryover =>
            inventoryCarryover;

        public event Action<string> LoadoutSelected;
        public event Action<string, bool, string> LoadoutApplied;

        private void OnValidate()
        {
            loadouts ??= Array.Empty<HubLoadoutDefinition>();
            selectedLoadoutIndex = loadouts.Length == 0
                ? 0
                : Mathf.Clamp(selectedLoadoutIndex, 0, loadouts.Length - 1);
        }

        public bool SelectLoadout(int index)
        {
            if (loadouts == null || index < 0 || index >= loadouts.Length ||
                loadouts[index] == null)
            {
                PublishResult(string.Empty, false, "That loadout is not configured.");
                return false;
            }

            selectedLoadoutIndex = index;
            string id = loadouts[index].LoadoutId;
            HubEventUtility.Invoke(LoadoutSelected, id, this);
            HubEventUtility.Invoke(loadoutSelected, id, this);
            return true;
        }

        public bool SelectNextLoadout()
        {
            if (loadouts == null || loadouts.Length == 0)
                return false;

            return SelectLoadout((selectedLoadoutIndex + 1) % loadouts.Length);
        }

        public bool SelectPreviousLoadout()
        {
            if (loadouts == null || loadouts.Length == 0)
                return false;

            int index = (selectedLoadoutIndex - 1 + loadouts.Length) %
                        loadouts.Length;
            return SelectLoadout(index);
        }

        public bool ApplySelectedLoadout()
        {
            return ApplyLoadout(selectedLoadoutIndex);
        }

        /// <summary>
        /// Parameterless UnityEvent adapter for the authored station collider.
        /// </summary>
        public void ApplySelectedLoadoutFromInteraction()
        {
            ApplySelectedLoadout();
        }

        /// <summary>
        /// Parameterless UnityEvent adapter for an authored next control.
        /// </summary>
        public void SelectNextLoadoutFromInteraction()
        {
            SelectNextLoadout();
        }

        /// <summary>
        /// Parameterless UnityEvent adapter for an authored previous control.
        /// </summary>
        public void SelectPreviousLoadoutFromInteraction()
        {
            SelectPreviousLoadout();
        }

        public bool ApplyLoadout(int index)
        {
            if (progression == null ||
                !progression.IsStationUnlocked(HubStationId.Loadout))
            {
                PublishResult(string.Empty, false, "The loadout station is locked.");
                return false;
            }

            ResolveInventory();
            ResolveCarryover();

            if (playerInventory == null)
            {
                PublishResult(string.Empty, false, "No player inventory is assigned.");
                return false;
            }

            if (inventoryCarryover == null)
            {
                PublishResult(
                    string.Empty,
                    false,
                    "The campaign inventory handoff is not available yet.");
                return false;
            }

            if (inventoryCarryover.IsRestoreInProgress ||
                inventoryCarryover.HasPendingRestoreFailure)
            {
                PublishResult(
                    string.Empty,
                    false,
                    inventoryCarryover.IsRestoreInProgress
                        ? "The campaign inventory is still restoring. Wait for it to finish before selecting a loadout."
                        : "The campaign inventory restore still needs recovery. Reload the Farm before selecting a loadout.");
                return false;
            }

            if (loadouts == null || index < 0 || index >= loadouts.Length ||
                loadouts[index] == null)
            {
                PublishResult(string.Empty, false, "That loadout is not configured.");
                return false;
            }

            HubLoadoutDefinition definition = loadouts[index];
            if (!TryBuildItemPlan(
                    definition,
                    out List<PlannedItem> plan,
                    out string failureReason))
            {
                PublishResult(definition.LoadoutId, false, failureReason);
                return false;
            }

            if (!TryValidateInventoryRuntimeDependencies(out failureReason))
            {
                PublishResult(definition.LoadoutId, false, failureReason);
                return false;
            }

            if (!TryPreflightAdds(
                    plan,
                    out List<PlannedAdd> additions,
                    out Dictionary<string, int> startingQuantities,
                    out failureReason))
            {
                PublishResult(definition.LoadoutId, false, failureReason);
                return false;
            }

            // Save the exact pre-grant inventory before the first mutation.
            // This gives a scene reload a durable recovery source even for an
            // older save that reached the Hub without a carryover snapshot.
            if (additions.Count > 0 &&
                !inventoryCarryover.CaptureForTravel())
            {
                PublishResult(
                    definition.LoadoutId,
                    false,
                    "The current inventory could not be saved, so no loadout items were added.");
                return false;
            }

            if (!TryApplyPreflightedAdds(
                    additions,
                    startingQuantities,
                    out failureReason))
            {
                PublishResult(definition.LoadoutId, false, failureReason);
                return false;
            }

            if (!inventoryCarryover.CaptureForTravel())
            {
                bool rolledBack = RollBackToStartingQuantities(
                    startingQuantities);
                bool rollbackCaptured = rolledBack &&
                                        inventoryCarryover.CaptureForTravel();
                if (!rolledBack || !rollbackCaptured)
                {
                    inventoryCarryover.MarkInventoryRecoveryPending(
                        !rolledBack
                            ? "The loadout grant could not be rolled back to its verified starting quantities."
                            : "The rolled-back loadout could not refresh the durable inventory snapshot.");
                }

                failureReason = !rolledBack
                    ? "The selected loadout could not be saved and its live inventory grant could not be fully rolled back. Reload the Farm before traveling."
                    : !rollbackCaptured
                        ? "The selected loadout could not be saved. The live grant was rolled back, but the prior inventory snapshot could not be refreshed; reload the Farm before traveling."
                        : "The selected loadout could not be saved. The grant was rolled back and the prior inventory was preserved.";
                PublishResult(
                    definition.LoadoutId,
                    false,
                    failureReason);
                return false;
            }

            selectedLoadoutIndex = index;
            PublishResult(
                definition.LoadoutId,
                true,
                $"{definition.DisplayName} ready; minimum preset quantities supplied.");
            return true;
        }

        private bool TryValidateInventoryRuntimeDependencies(
            out string failureReason)
        {
            global::gameManager manager = global::gameManager.instance;
            GameObject player = manager != null ? manager.player : null;
            playerController controller = player != null
                ? player.GetComponent<playerController>()
                : null;
            if (player == null ||
                playerInventory == null ||
                playerInventory.gameObject != player ||
                controller == null ||
                manager.playerController != controller ||
                playerInventory.inventoryItems == null ||
                !playerInventory.IsValidIndex(0))
            {
                failureReason =
                    "The player inventory is not fully initialized. " +
                    "No loadout items were added; try again after the hub " +
                    "finishes loading.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public void Configure(
            HubStationProgression stationProgression,
            Inventory inventory,
            CampaignInventoryCarryover carryover,
            HubLoadoutDefinition[] definitions)
        {
            progression = stationProgression;
            playerInventory = inventory;
            inventoryCarryover = carryover;
            loadouts = definitions ?? Array.Empty<HubLoadoutDefinition>();
            selectedLoadoutIndex = 0;
        }

        public void Configure(
            HubStationProgression stationProgression,
            Inventory inventory,
            HubLoadoutDefinition[] definitions)
        {
            Configure(
                stationProgression,
                inventory,
                inventory != null
                    ? CampaignStateService.Instance
                        ?.GetComponent<CampaignInventoryCarryover>()
                    : null,
                definitions);
        }

        private bool TryBuildItemPlan(
            HubLoadoutDefinition definition,
            out List<PlannedItem> plan,
            out string failureReason)
        {
            plan = new List<PlannedItem>();
            var planIndices = new Dictionary<string, int>(
                StringComparer.Ordinal);

            foreach (HubLoadoutItem entry in definition.Items)
            {
                GameObject pickup = entry?.ItemPickupPrefab;
                ItemStats item =
                    CampaignInventoryTokenUtility.GetItemStats(pickup);
                if (item == null || string.IsNullOrWhiteSpace(item.itemName) ||
                    item.quantity <= 0 || item.stackSize <= 0)
                {
                    failureReason =
                        $"Loadout '{definition.DisplayName}' contains an invalid item.";
                    return false;
                }

                string name = item.itemName.Trim();
                if (planIndices.TryGetValue(name, out int existingIndex))
                {
                    PlannedItem existing = plan[existingIndex];
                    existing.Quantity += entry.Quantity;
                    plan[existingIndex] = existing;
                }
                else
                {
                    planIndices.Add(name, plan.Count);
                    plan.Add(new PlannedItem(pickup, item, entry.Quantity));
                }
            }

            if (plan.Count == 0)
            {
                failureReason =
                    $"Loadout '{definition.DisplayName}' has no items.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool TryPreflightAdds(
            IReadOnlyList<PlannedItem> plan,
            out List<PlannedAdd> additions,
            out Dictionary<string, int> startingQuantities,
            out string failureReason)
        {
            additions = new List<PlannedAdd>();
            startingQuantities = new Dictionary<string, int>(
                StringComparer.Ordinal);

            int slotCount = GetInventorySlotCount();
            if (slotCount <= 0)
            {
                failureReason =
                    "The player inventory has no initialized slots; nothing was added.";
                return false;
            }

            int occupiedSlots = CountOccupiedSlots(slotCount);

            foreach (PlannedItem planned in plan)
            {
                string itemName = planned.Source.itemName.Trim();
                int currentQuantity = Mathf.Max(
                    0,
                    playerInventory.FindItem(planned.Source).Value);
                int matchingSlots = CountMatchingSlots(
                    planned.Source,
                    slotCount);
                int stackSize = planned.Source.stackSize;
                int packQuantity = planned.Source.quantity;

                startingQuantities[itemName] = currentQuantity;

                while (currentQuantity < planned.Quantity)
                {
                    // Inventory.AddItem(GameObject) immediately takes its
                    // world-drop path when its occupied-slot count is full,
                    // even if a matching stack has room. Require a free slot
                    // before every authored pickup call to match that public
                    // API contract exactly.
                    if (occupiedSlots >= slotCount)
                    {
                        failureReason =
                            $"The complete {itemName} grant does not fit. " +
                            "Free inventory space and try again; nothing was added.";
                        return false;
                    }

                    int matchingCapacity = Mathf.Max(
                        0,
                        matchingSlots * stackSize - currentQuantity);
                    int quantityNeedingNewSlots = Mathf.Max(
                        0,
                        packQuantity - matchingCapacity);
                    int newSlotsNeeded = Mathf.CeilToInt(
                        quantityNeedingNewSlots / (float)stackSize);
                    int freeSlots = slotCount - occupiedSlots;

                    if (newSlotsNeeded > freeSlots)
                    {
                        failureReason =
                            $"The complete {itemName} grant needs " +
                            $"{newSlotsNeeded} additional slot(s), but only " +
                            $"{freeSlots} are free; nothing was added.";
                        return false;
                    }

                    additions.Add(new PlannedAdd(
                        planned.PickupPrefab,
                        planned.Source,
                        packQuantity));
                    occupiedSlots += newSlotsNeeded;
                    matchingSlots += newSlotsNeeded;
                    currentQuantity += packQuantity;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private bool TryApplyPreflightedAdds(
            IReadOnlyList<PlannedAdd> additions,
            IReadOnlyDictionary<string, int> startingQuantities,
            out string failureReason)
        {
            try
            {
                foreach (PlannedAdd addition in additions)
                {
                    string itemName = addition.Source.itemName.Trim();
                    int before = Mathf.Max(
                        0,
                        playerInventory.FindItem(addition.Source).Value);

                    playerInventory.AddItem(
                        CampaignInventoryTokenUtility.CloneItemStats(
                            addition.Source,
                            addition.ExpectedQuantity));

                    int after = Mathf.Max(
                        0,
                        playerInventory.FindItem(addition.Source).Value);
                    int accepted = Mathf.Max(0, after - before);

                    if (accepted != addition.ExpectedQuantity)
                    {
                        bool rolledBack = RollBackToStartingQuantities(
                            startingQuantities);
                        if (!rolledBack)
                        {
                            inventoryCarryover?.MarkInventoryRecoveryPending(
                                $"Inventory changed unexpectedly while granting '{itemName}' and could not be restored exactly.");
                        }

                        failureReason = rolledBack
                            ? $"Inventory did not accept the preflighted {itemName} grant. All granted quantities were rolled back."
                            : $"Inventory changed unexpectedly while granting {itemName}; automatic rollback could not restore every starting quantity.";
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                // AddItem can mutate Inventory before a downstream dependency
                // throws. Re-measure every planned item instead of trusting the
                // grants recorded only after AddItem returned successfully.
                bool rolledBack = RollBackToStartingQuantities(
                    startingQuantities);
                if (!rolledBack)
                {
                    inventoryCarryover?.MarkInventoryRecoveryPending(
                        $"Loadout application threw {exception.GetType().Name} after inventory mutation and rollback could not be verified.");
                }

                failureReason = rolledBack
                    ? $"Loadout application failed ({exception.GetType().Name}); all granted quantities were rolled back."
                    : $"Loadout application failed ({exception.GetType().Name}) and automatic rollback could not restore every starting quantity.";

                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool RollBackToStartingQuantities(
            IReadOnlyDictionary<string, int> startingQuantities)
        {
            try
            {
                foreach (PlannedItem planned in BuildVerificationItems())
                {
                    string itemName = planned.Source.itemName.Trim();
                    if (!startingQuantities.TryGetValue(
                            itemName,
                            out int expected))
                    {
                        continue;
                    }

                    int actual = Mathf.Max(
                        0,
                        playerInventory.FindItem(planned.Source).Value);
                    int surplus = actual - expected;
                    if (surplus > 0)
                    {
                        playerInventory.RemoveItem(
                            itemName,
                            surplus,
                            false);
                    }
                }

                foreach (PlannedItem planned in BuildVerificationItems())
                {
                    string itemName = planned.Source.itemName.Trim();
                    if (!startingQuantities.TryGetValue(
                            itemName,
                            out int expected))
                    {
                        continue;
                    }

                    int actual = Mathf.Max(
                        0,
                        playerInventory.FindItem(planned.Source).Value);
                    if (actual != expected)
                        return false;
                }

                return true;
            }
            catch (Exception rollbackException)
            {

                return false;
            }
        }

        private IEnumerable<PlannedItem> BuildVerificationItems()
        {
            if (loadouts == null)
                yield break;

            var yieldedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (HubLoadoutDefinition definition in loadouts)
            {
                if (definition == null ||
                    !TryBuildItemPlan(
                        definition,
                        out List<PlannedItem> plan,
                        out _))
                {
                    continue;
                }

                foreach (PlannedItem planned in plan)
                {
                    string name = planned.Source.itemName.Trim();
                    if (yieldedNames.Add(name))
                        yield return planned;
                }
            }
        }

        private void ResolveInventory()
        {
            if (playerInventory != null)
                return;

            if (gameManager.instance != null &&
                gameManager.instance.player != null)
            {
                playerInventory =
                    gameManager.instance.player.GetComponent<Inventory>();
            }
        }

        private void ResolveCarryover()
        {
            CampaignInventoryCarryover persistent =
                CampaignStateService.Instance
                    ?.GetComponent<CampaignInventoryCarryover>();
            if (persistent != null)
            {
                inventoryCarryover = persistent;
            }
        }

        private int GetInventorySlotCount()
        {
            int count = 0;
            while (count < 4096 && playerInventory.IsValidIndex(count))
            {
                count++;
            }

            return count;
        }

        private int CountOccupiedSlots(int slotCount)
        {
            int occupied = 0;
            for (int index = 0; index < slotCount; index++)
            {
                if (!playerInventory.IsSlotEmpty(index))
                    occupied++;
            }

            return occupied;
        }

        private int CountMatchingSlots(ItemStats item, int slotCount)
        {
            int matches = 0;
            for (int index = 0; index < slotCount; index++)
            {
                if (!playerInventory.IsSlotEmpty(index) &&
                    playerInventory.FindItem(item, index) == index)
                {
                    matches++;
                }
            }

            return matches;
        }

        private void PublishResult(
            string loadoutId,
            bool succeeded,
            string message)
        {
            string safeId = loadoutId ?? string.Empty;
            string safeMessage = message ?? string.Empty;
            HubEventUtility.Invoke(
                LoadoutApplied,
                safeId,
                succeeded,
                safeMessage,
                this);
            HubEventUtility.Invoke(
                loadoutApplied,
                safeId,
                succeeded,
                safeMessage,
                this);
        }

        private struct PlannedItem
        {
            public PlannedItem(
                GameObject pickupPrefab,
                ItemStats source,
                int quantity)
            {
                PickupPrefab = pickupPrefab;
                Source = source;
                Quantity = quantity;
            }

            public GameObject PickupPrefab;
            public ItemStats Source;
            public int Quantity;
        }

        private readonly struct PlannedAdd
        {
            public PlannedAdd(
                GameObject pickupPrefab,
                ItemStats source,
                int expectedQuantity)
            {
                PickupPrefab = pickupPrefab;
                Source = source;
                ExpectedQuantity = expectedQuantity;
            }

            public GameObject PickupPrefab { get; }
            public ItemStats Source { get; }
            public int ExpectedQuantity { get; }
        }
    }
}
