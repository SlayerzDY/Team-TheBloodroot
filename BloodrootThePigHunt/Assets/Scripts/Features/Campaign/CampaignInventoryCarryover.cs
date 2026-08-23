using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bloodroot.Campaign
{
    [Serializable]
    internal sealed class CampaignStableInventoryCheckpoint
    {
        public bool isAuthoritative;
        public string[] itemIds = Array.Empty<string>();
        public int[] quantities = Array.Empty<int>();
        public float inventoryWeight;

        public static CampaignStableInventoryCheckpoint CreateEmpty()
        {
            return new CampaignStableInventoryCheckpoint
            {
                isAuthoritative = true,
                itemIds = Array.Empty<string>(),
                quantities = Array.Empty<int>(),
                inventoryWeight = 0f
            };
        }
    }

    [Serializable]
    internal sealed class CampaignStableGunCheckpoint
    {
        public bool isAuthoritative;
        public string[] gunIds = Array.Empty<string>();
        public int[] ammo = Array.Empty<int>();
        public int selectedIndex = -1;

        public static CampaignStableGunCheckpoint CreateEmpty()
        {
            return new CampaignStableGunCheckpoint
            {
                isAuthoritative = true,
                gunIds = Array.Empty<string>(),
                ammo = Array.Empty<int>(),
                selectedIndex = -1
            };
        }
    }

    [Serializable]
    public sealed class CampaignInventoryItemBinding
    {
        [SerializeField] private string itemId = string.Empty;
        [SerializeField] private GameObject pickupPrefab;

        public string ItemId => itemId?.Trim() ?? string.Empty;
        public GameObject PickupPrefab => pickupPrefab;
        public ItemStats ItemData =>
            CampaignInventoryTokenUtility.GetItemStats(pickupPrefab);

        public void Configure(string id, GameObject prefab)
        {
            itemId = id?.Trim() ?? string.Empty;
            pickupPrefab = prefab;
        }
    }

    /// <summary>
    /// Preserves the authored Alpha item catalog across the campaign's
    /// single-scene Farm/OpenWorld travel. It observes and mutates Inventory
    /// only through the approved public hooks; Inventory owns its storage.
    /// </summary>
    [DefaultExecutionOrder(-850)]
    [DisallowMultipleComponent]
    public sealed class CampaignInventoryCarryover : MonoBehaviour
    {
        internal const int MaximumSupportedInventoryCapacity = 4096;

        private const string SafetyRifleStableItemId = "m1_garand";
        private const string SafetyRifleItemId = "M1 Garand57457457";
        private const string SafetyRifleAmmoStableItemId =
            "m1_garand_ammo";
        private const string SafetyRifleAmmoItemId =
            "M1 Garand Ammo5736854368";
        private const string SafetyRadarStableItemId = "radar";
        private const string SafetyRadarItemId = "Radar5474357346";
        // Retained only to discard the retired slot while loading older saves.
        private const string RetiredNameStoneStableItemId = "name_stone";
        private const string CursedRootShardStableItemId =
            "cursed_root_shard";
        private const string CursedRootShardItemId =
            "BloodrootCursedRootShardV1";
        private const string HeartrootStableItemId =
            CampaignHeartrootInventoryIds.StableId;
        private const string HeartrootItemId =
            CampaignHeartrootInventoryIds.SerializedItemId;
        private const string SafetyTruckKeyStableItemId = "car_key";
        private const string SafetyTruckKeyItemId = "Car Key2358932";
        private const string SafetyLeatherStableItemId = "leather";
        private const string SafetyLeatherItemId = "Leather4634576";
        // Online Safety now uses the blank-ID master pickup as a Leather
        // template. Retain its former stable ID only as a checkpoint alias;
        // new captures normalize both prefab forms to the Leather binding.
        private const string LegacySafetyPickupMasterStableItemId =
            "item_pickup_master";
        // Safety-native items outside the authored campaign catalog retain
        // their own durable ItemStats.itemID. The prefix keeps that namespace
        // distinct from campaign-owned stable IDs in the context checkpoint.
        private const string NativeSafetyInventoryItemIdPrefix =
            "safety_native/";

        [SerializeField] private CampaignInventoryItemBinding[] itemCatalog =
            Array.Empty<CampaignInventoryItemBinding>();
        [SerializeField] private CampaignInventoryItemBinding[]
            safetyInventoryCatalog = Array.Empty<CampaignInventoryItemBinding>();
        [SerializeField] private GameObject safetyInventoryCarrierPrefab;
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(0f)] private float playerLookupTimeout = 5f;

        private int[] carriedQuantities = Array.Empty<int>();
        private bool hasSnapshot;
        private bool restoreInProgress;
        private bool restoreFailedPending;
        private Coroutine restoreRoutine;
        private Inventory suppressedSceneInventory;
        private bool suppressedSceneInventoryWasEnabled;
        private playerController automaticLoadGuardController;
        private bool automaticLoadGuardPreviousNewGame;
        private bool startupInitializationAttempted;
        private bool sceneLoadedSubscribed;

        public bool HasSnapshot => hasSnapshot;
        public bool IsRestoreInProgress => restoreInProgress;
        public bool HasPendingRestoreFailure => restoreFailedPending;
        public int CatalogCount => itemCatalog?.Length ?? 0;
        public int SafetyInventoryCatalogCount =>
            safetyInventoryCatalog?.Length ?? 0;
        public GameObject SafetyInventoryCarrierPrefab =>
            safetyInventoryCarrierPrefab;

        public event Action<bool> RestoreCompleted;

        private void Awake()
        {
            if (!TryBeginPersistentAuthorityStartup())
                return;

            DontDestroyOnLoad(gameObject);
            bool startupSafe = CampaignSafetySaveIntegration
                .TrySanitizeSafetyGunSave(out string sanitizeError);
            LoadDurableSnapshot();
            if (!startupSafe)
            {
                restoreFailedPending = true;

            }
        }

        private void Start()
        {
            // CampaignStateService is authored at execution order -1000 and
            // this adapter at -850. Retrying once in Start also makes the
            // durable snapshot resilient to unusual test/component creation
            // order without changing the protected Inventory lifecycle.
            if (!hasSnapshot && !restoreFailedPending)
                LoadDurableSnapshot();
        }

        private void OnEnable()
        {
            if (!IsPersistentAuthority())
            {
                // Remove a stale subscription as well as refusing a new one;
                // this remains fail-closed if a rejected destination copy is
                // manually re-enabled before Unity destroys its GameObject.
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                sceneLoadedSubscribed = false;
                enabled = false;
                return;
            }

            if (!sceneLoadedSubscribed)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                SceneManager.sceneLoaded += HandleSceneLoaded;
                sceneLoadedSubscribed = true;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            sceneLoadedSubscribed = false;
            if (restoreRoutine != null)
            {
                StopCoroutine(restoreRoutine);
                restoreRoutine = null;
            }

            restoreInProgress = false;
            RestoreSuppressedSceneInventory();
            ReleaseAutomaticLoadGuard();
        }

        public void Configure(CampaignInventoryItemBinding[] catalog)
        {
            Configure(catalog, Array.Empty<CampaignInventoryItemBinding>());
        }

        public void Configure(
            CampaignInventoryItemBinding[] catalog,
            CampaignInventoryItemBinding[] supplementalSafetyCatalog)
        {
            Configure(catalog, supplementalSafetyCatalog, null);
        }

        public void Configure(
            CampaignInventoryItemBinding[] catalog,
            CampaignInventoryItemBinding[] supplementalSafetyCatalog,
            GameObject authoredSafetyItemCarrier)
        {
            itemCatalog = catalog ?? Array.Empty<CampaignInventoryItemBinding>();
            safetyInventoryCatalog = supplementalSafetyCatalog ??
                                     Array.Empty<CampaignInventoryItemBinding>();
            safetyInventoryCarrierPrefab = authoredSafetyItemCarrier;
            carriedQuantities = new int[itemCatalog.Length];
            hasSnapshot = false;
            restoreFailedPending = false;
        }

        public void ClearSnapshot()
        {
            EnsureQuantityBuffer();
            Array.Clear(carriedQuantities, 0, carriedQuantities.Length);
            hasSnapshot = false;
            restoreFailedPending = false;
        }

        /// <summary>
        /// Fails travel closed when an owned inventory transaction cannot
        /// prove that its live quantities and durable snapshot still agree.
        /// A successful destination restore is the only safe in-session
        /// recovery path; it clears this latch after rebuilding exact totals.
        /// </summary>
        public void MarkInventoryRecoveryPending(string reason)
        {
            restoreFailedPending = true;
            string safeReason = string.IsNullOrWhiteSpace(reason)
                ? "Campaign inventory recovery is required before traveling."
                : reason.Trim();

        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                CaptureCurrentGameplayInventory();
        }

        private void OnApplicationQuit()
        {
            CaptureCurrentGameplayInventory();
        }

        /// <summary>
        /// Captures supported item totals immediately before an authored
        /// CampaignSceneTravel starts the destination scene load.
        /// </summary>
        public bool CaptureForTravel()
        {
            if (!TryCaptureForAreaCompletion(
                    out string[] itemIds,
                    out int[] nextQuantities))
            {
                return false;
            }

            CampaignStateService state = CampaignStateService.Instance;
            string validationError = string.Empty;
            if (state == null ||
                !state.TryValidateInventoryCarryoverSnapshot(
                    itemIds,
                    nextQuantities,
                    out validationError))
            {

                return false;
            }

            if (!state.UpdateInventoryCarryover(
                    itemIds,
                    nextQuantities))
            {
                return false;
            }

            carriedQuantities = nextQuantities;
            hasSnapshot = true;
            restoreFailedPending = false;
            return true;
        }

        /// <summary>
        /// Builds an exact supported-inventory snapshot without writing it.
        /// CampaignAreaCompletionRelay stages this snapshot so inventory and
        /// area completion become durable in one campaign save transaction.
        /// </summary>
        internal bool TryCaptureForAreaCompletion(
            out string[] itemIds,
            out int[] quantities)
        {
            Inventory inventory = FindPlayerInventory();
            return TryCaptureForAreaCompletionFromInventory(
                inventory,
                out itemIds,
                out quantities);
        }

        internal bool TryCaptureForAreaCompletionFromInventory(
            Inventory inventory,
            out string[] itemIds,
            out int[] quantities)
        {
            itemIds = Array.Empty<string>();
            quantities = Array.Empty<int>();
            if (restoreInProgress || restoreFailedPending)
            {

                return false;
            }

            if (!ValidateCatalog(out string error))
            {

                return false;
            }

            if (inventory == null)
            {

                return false;
            }

            itemIds = GetCatalogIds();
            quantities = new int[itemCatalog.Length];
            for (int index = 0; index < itemCatalog.Length; index++)
            {
                ItemStats item = itemCatalog[index].ItemData;
                quantities[index] = Mathf.Max(
                    0,
                    inventory.FindItem(item).Value);
            }

            return true;
        }

        internal void AcceptAreaCompletionSnapshot(int[] quantities)
        {
            EnsureQuantityBuffer();
            if (quantities == null || quantities.Length != carriedQuantities.Length)
            {
                return;
            }

            Array.Copy(quantities, carriedQuantities, quantities.Length);
            hasSnapshot = true;
            restoreFailedPending = false;
        }

        internal bool TryRestoreTravelSnapshot(
            string[] itemIds,
            int[] quantities)
        {
            if (!TryMapExactCatalogSnapshot(
                    itemIds,
                    quantities,
                    out int[] mapped))
            {
                return false;
            }

            CampaignStateService state = CampaignStateService.Instance;
            if (state == null ||
                !state.UpdateInventoryCarryover(GetCatalogIds(), mapped))
            {
                return false;
            }

            carriedQuantities = mapped;
            hasSnapshot = true;
            restoreFailedPending = false;
            return true;
        }

        [Obsolete("Retired campaign data is discarded during save migration.")]
        public bool TryReconcileExtractedNameStoneTokens(
            GameObject authoredPickup,
            Inventory inventory)
        {
            return true;
        }

        /// <summary>
        /// Reconstructs the dedicated Heartroot quest token from the V6
        /// campaign facts. The durable fact is authoritative across the
        /// campaign-first/Safety-pair crash window: recovered and unburned
        /// means exactly one live token; every other state means none.
        /// </summary>
        public bool TryReconcileHeartrootToken(
            Inventory inventory,
            out string error)
        {
            error = string.Empty;
            CampaignStateService state = CampaignStateService.Instance;
            if (state == null || inventory == null)
            {
                error =
                    "Heartroot reconciliation requires campaign state and the active Player Inventory.";
                return false;
            }

            CampaignInventoryItemBinding heartrootBinding = null;
            foreach (CampaignInventoryItemBinding binding in
                     itemCatalog ?? Array.Empty<CampaignInventoryItemBinding>())
            {
                if (binding != null && string.Equals(
                        binding.ItemId,
                        HeartrootStableItemId,
                        StringComparison.Ordinal))
                {
                    heartrootBinding = binding;
                    break;
                }
            }

            ItemStats token = heartrootBinding?.ItemData;
            if (token == null || !string.Equals(
                    token.itemID?.Trim(),
                    HeartrootItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    token.itemName?.Trim(),
                    CampaignHeartrootInventoryIds.ItemName,
                    StringComparison.Ordinal) ||
                token.quantity != 1 || token.stackSize != 1)
            {
                error =
                    "The campaign catalog is missing the exact exposed Heartroot token contract.";
                return false;
            }

            try
            {
                if (!inventory.IsValidIndex(0))
                {
                    error =
                        "The Player Inventory has not initialized its authored slots.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error =
                    "The Player Inventory is unavailable for Heartroot reconciliation: " +
                    exception.Message;
                return false;
            }

            CampaignProgressSnapshot progress = state.Current;
            int requiredQuantity = progress.HeartrootCarried &&
                                   !progress.HeartrootBurned
                ? 1
                : 0;
            int startingQuantity;
            try
            {
                startingQuantity = Mathf.Max(
                    0,
                    inventory.FindItem(token).Value);
            }
            catch (Exception exception)
            {
                error =
                    "The live Heartroot token quantity could not be read: " +
                    exception.Message;
                return false;
            }
            if (startingQuantity == requiredQuantity)
                return true;

            if (TrySetExactInventoryQuantity(
                    inventory,
                    token,
                    requiredQuantity))
            {
                return true;
            }

            bool rolledBack = TrySetExactInventoryQuantity(
                inventory,
                token,
                startingQuantity);
            error =
                $"The live Heartroot token could not be reconciled to durable quantity {requiredQuantity}.";
            if (!rolledBack)
            {
                MarkInventoryRecoveryPending(
                    error +
                    " Its live-token rollback also failed; reload before traveling.");
                error += " Live-token rollback also failed.";
            }

            return false;
        }

        private static bool TrySetExactInventoryQuantity(
            Inventory inventory,
            ItemStats item,
            int targetQuantity)
        {
            if (inventory == null || item == null || targetQuantity < 0)
                return false;

            try
            {
                int current = Mathf.Max(0, inventory.FindItem(item).Value);
                while (current < targetQuantity)
                {
                    inventory.AddItem(
                        CampaignInventoryTokenUtility.CloneItemStats(item, 1));
                    int next = Mathf.Max(0, inventory.FindItem(item).Value);
                    if (next != current + 1)
                        return false;
                    current = next;
                }

                if (current > targetQuantity)
                {
                    inventory.RemoveItem(
                        item.itemName,
                        current - targetQuantity,
                        false);
                }

                return Mathf.Max(0, inventory.FindItem(item).Value) ==
                       targetQuantity;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool ValidateCatalog(out string error)
        {
            CampaignInventoryItemBinding[] catalog =
                itemCatalog ?? Array.Empty<CampaignInventoryItemBinding>();
            if (catalog.Length == 0)
            {
                error = "Campaign inventory carryover requires an authored item catalog.";
                return false;
            }

            var seenIds = new System.Collections.Generic.HashSet<string>(
                StringComparer.Ordinal);
            var seenNames = new System.Collections.Generic.HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < catalog.Length; index++)
            {
                CampaignInventoryItemBinding binding = catalog[index];
                ItemStats item = binding?.ItemData;
                string itemName = item?.itemName?.Trim() ?? string.Empty;
                string itemId = item?.itemID?.Trim() ?? string.Empty;
                if (binding == null || binding.ItemId.Length == 0 ||
                    binding.PickupPrefab == null || item == null ||
                    itemName.Length == 0 || itemId.Length == 0 ||
                    item.quantity <= 0 ||
                    item.stackSize <= 0)
                {
                    error = $"Campaign inventory catalog entry {index} is invalid.";
                    return false;
                }

                if (!seenIds.Add(binding.ItemId) || !seenNames.Add(itemName))
                {
                    error =
                        $"Campaign inventory catalog entry '{binding.ItemId}' duplicates an item ID or name.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool ValidateStableInventoryCatalog(out string error)
        {
            if (!ValidateCatalog(out error))
                return false;

            if (safetyInventoryCarrierPrefab == null ||
                safetyInventoryCarrierPrefab.GetComponent<Item>() == null)
            {
                error =
                    "Stable Safety inventory restoration requires an authored Safety Item carrier prefab.";
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenSerializedItemIds =
                new HashSet<string>(StringComparer.Ordinal);
            var seenDefinitions = new List<ItemStats>();
            foreach (CampaignInventoryItemBinding binding in
                     itemCatalog ?? Array.Empty<CampaignInventoryItemBinding>())
            {
                if (!ValidateStableBindingItemId(binding, out error))
                    return false;

                string serializedItemId =
                    binding.ItemData.itemID.Trim();
                if (!seenSerializedItemIds.Add(serializedItemId))
                {
                    error =
                        $"Campaign inventory catalog entry '{binding.ItemId}' duplicates serialized itemID '{serializedItemId}'.";
                    return false;
                }

                seenIds.Add(binding.ItemId);
                seenDefinitions.Add(binding.ItemData);
            }

            CampaignInventoryItemBinding[] supplemental =
                safetyInventoryCatalog ??
                Array.Empty<CampaignInventoryItemBinding>();
            for (int index = 0; index < supplemental.Length; index++)
            {
                CampaignInventoryItemBinding binding = supplemental[index];
                ItemStats item = binding?.ItemData;
                string itemName = item?.itemName?.Trim() ?? string.Empty;
                string serializedItemId = item?.itemID?.Trim() ??
                                          string.Empty;
                if (binding == null || binding.ItemId.Length == 0 ||
                    binding.PickupPrefab == null || item == null ||
                    itemName.Length == 0 || serializedItemId.Length == 0 ||
                    item.quantity <= 0 ||
                    item.stackSize <= 0 || item.weight < 0f)
                {
                    error =
                        $"Safety inventory catalog entry {index} is invalid.";
                    return false;
                }

                if (!seenIds.Add(binding.ItemId))
                {
                    error =
                        $"Safety inventory catalog entry '{binding.ItemId}' duplicates an item ID.";
                    return false;
                }

                if (!ValidateStableBindingItemId(binding, out error))
                    return false;

                if (!seenSerializedItemIds.Add(serializedItemId))
                {
                    error =
                        $"Safety inventory catalog entry '{binding.ItemId}' duplicates serialized itemID '{serializedItemId}'.";
                    return false;
                }

                foreach (ItemStats existing in seenDefinitions)
                {
                    if (HaveSameStableItemFingerprint(existing, item))
                    {
                        error =
                            $"Safety inventory catalog entry '{binding.ItemId}' duplicates an authored item fingerprint.";
                        return false;
                    }
                }

                seenDefinitions.Add(item);
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateStableBindingItemId(
            CampaignInventoryItemBinding binding,
            out string error)
        {
            string stableItemId = binding?.ItemId ?? string.Empty;
            string serializedItemId =
                binding?.ItemData?.itemID?.Trim() ?? string.Empty;
            if (stableItemId.Length == 0 || serializedItemId.Length == 0)
            {
                error =
                    $"Stable inventory binding '{stableItemId}' requires a nonblank serialized ItemStats.itemID.";
                return false;
            }

            if (TryGetExpectedSerializedItemId(
                    stableItemId,
                    out string expectedItemId) &&
                !string.Equals(
                    serializedItemId,
                    expectedItemId,
                    StringComparison.Ordinal))
            {
                error =
                    $"Stable inventory binding '{stableItemId}' requires Safety itemID '{expectedItemId}', not '{serializedItemId}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryGetExpectedSerializedItemId(
            string stableItemId,
            out string expectedItemId)
        {
            switch (stableItemId?.Trim())
            {
                case SafetyRifleStableItemId:
                    expectedItemId = SafetyRifleItemId;
                    return true;
                case SafetyRifleAmmoStableItemId:
                    expectedItemId = SafetyRifleAmmoItemId;
                    return true;
                case SafetyRadarStableItemId:
                    expectedItemId = SafetyRadarItemId;
                    return true;
                case CursedRootShardStableItemId:
                    expectedItemId = CursedRootShardItemId;
                    return true;
                case HeartrootStableItemId:
                    expectedItemId = HeartrootItemId;
                    return true;
                case SafetyTruckKeyStableItemId:
                    expectedItemId = SafetyTruckKeyItemId;
                    return true;
                case SafetyLeatherStableItemId:
                    expectedItemId = SafetyLeatherItemId;
                    return true;
                default:
                    expectedItemId = string.Empty;
                    return false;
            }
        }

        internal bool TryCaptureStableInventoryCheckpoint(
            Inventory inventory,
            out CampaignStableInventoryCheckpoint checkpoint,
            out string error)
        {
            checkpoint = null;
            if (!TryGetInventoryCapacity(
                    inventory,
                    out int inventoryCapacity,
                    out error))
            {
                return false;
            }

            if (!ValidateStableInventoryCatalog(out error))
                return false;

            string[] itemIds = new string[inventoryCapacity];
            int[] quantities = new int[inventoryCapacity];
            float expectedWeight = 0f;
            for (int slot = 0; slot < inventory.inventoryItems.Length; slot++)
            {
                ItemStats item = inventory.inventoryItems[slot];
                if (item == null || string.IsNullOrWhiteSpace(item.itemName) ||
                    item.quantity <= 0)
                {
                    itemIds[slot] = string.Empty;
                    quantities[slot] = 0;
                    continue;
                }

                if (item.stackSize <= 0 || item.quantity > item.stackSize ||
                    !TryResolveStableInventoryItem(
                        item,
                        out string checkpointItemId,
                        out ItemStats definition))
                {
                    error =
                        $"Inventory slot {slot} contains unsupported or invalid item '{item.itemName}'.";
                    return false;
                }

                if (definition == null || definition.stackSize <= 0 ||
                    item.quantity > definition.stackSize)
                {
                    error =
                        $"Inventory slot {slot} cannot be reconstructed from stable item '{checkpointItemId}'.";
                    return false;
                }

                itemIds[slot] = checkpointItemId;
                quantities[slot] = item.quantity;
                expectedWeight += definition.weight;
            }

            if (Mathf.Abs(inventory.inventoryWeight - expectedWeight) > 0.001f)
            {
                error =
                    $"Safety Inventory weight {inventory.inventoryWeight} does not match its reconstructable slot weight {expectedWeight}.";
                return false;
            }

            checkpoint = new CampaignStableInventoryCheckpoint
            {
                isAuthoritative = true,
                itemIds = itemIds,
                quantities = quantities,
                inventoryWeight = expectedWeight
            };
            error = string.Empty;
            return true;
        }

        private bool TryReadStableInventoryForScene(
            string sceneName,
            out ItemStats[] items,
            out bool found,
            out string error)
        {
            items = Array.Empty<ItemStats>();
            found = false;
            if (!CampaignSafetySaveIntegration.TryReadStableInventoryCheckpoint(
                    sceneName,
                    out CampaignStableInventoryCheckpoint checkpoint,
                    out found,
                    out error))
            {
                return false;
            }

            if (!found)
                return true;

            return TryBuildInventoryFromStableCheckpoint(
                checkpoint,
                out items,
                out error);
        }

        private static bool TryRestoreStableGunsForScene(
            playerController controller,
            string sceneName,
            out string error)
        {
            CampaignLoadoutEquipmentBridge bridge =
                CampaignLoadoutEquipmentBridge.Instance;
            if (bridge == null)
            {
                error = "The stable Safety gun bridge is unavailable.";
                return false;
            }

            return bridge.TryRestoreStableGunCheckpoint(
                controller,
                sceneName,
                out _,
                out error);
        }

        private bool TryBuildInventoryFromStableCheckpoint(
            CampaignStableInventoryCheckpoint checkpoint,
            out ItemStats[] items,
            out string error)
        {
            items = Array.Empty<ItemStats>();
            if (!ValidateStableInventoryCatalog(out error) ||
                !CampaignSafetySaveIntegration.ValidateStableInventoryShape(
                    checkpoint,
                    out error))
            {
                return false;
            }

            var restored = new ItemStats[checkpoint.itemIds.Length];
            float expectedWeight = 0f;
            for (int slot = 0; slot < restored.Length; slot++)
            {
                string itemId = checkpoint.itemIds[slot]?.Trim() ??
                                string.Empty;
                int quantity = checkpoint.quantities[slot];
                if (itemId.Length == 0)
                    continue;

                if (!TryResolveStableCheckpointItem(
                        itemId,
                        out ItemStats definition))
                {
                    error =
                        $"Stable Safety inventory item ID '{itemId}' is not in the authored catalog or active Safety ItemDatabase.";
                    return false;
                }

                if (definition == null || quantity <= 0 ||
                    quantity > definition.stackSize)
                {
                    error =
                        $"Stable Safety inventory slot {slot} has an invalid quantity for '{itemId}'.";
                    return false;
                }

                restored[slot] =
                    CampaignInventoryTokenUtility.CloneItemStats(
                        definition,
                        quantity);
                expectedWeight += definition.weight;
            }

            if (Mathf.Abs(checkpoint.inventoryWeight - expectedWeight) >
                0.001f)
            {
                error =
                    "Stable Safety inventory weight does not match its authored slot topology.";
                return false;
            }

            items = restored;
            error = string.Empty;
            return true;
        }

        private bool TryFindStableInventoryBindingForItem(
            ItemStats item,
            out CampaignInventoryItemBinding binding)
        {
            binding = null;
            int matches = 0;
            string serializedItemId = item?.itemID?.Trim() ?? string.Empty;
            if (serializedItemId.Length > 0)
            {
                foreach (CampaignInventoryItemBinding entry in
                         EnumerateStableInventoryCatalog())
                {
                    if (string.Equals(
                            entry.ItemData?.itemID?.Trim(),
                            serializedItemId,
                            StringComparison.Ordinal))
                    {
                        binding = entry;
                        matches++;
                    }
                }

                // Nonblank d656 itemID is the durable Safety authority. A
                // wrong ID must never fall through to a similar primitive
                // fingerprint; catalog validation guarantees uniqueness.
                return matches == 1;
            }

            foreach (CampaignInventoryItemBinding entry in
                     EnumerateStableInventoryCatalog())
            {
                if (HaveSameStableItemFingerprint(item, entry.ItemData))
                {
                    binding = entry;
                    matches++;
                }
            }

            return matches == 1;
        }

        private static bool HaveSameStableItemFingerprint(
            ItemStats first,
            ItemStats second)
        {
            if (first == null || second == null ||
                !string.Equals(
                    first.itemName?.Trim(),
                    second.itemName?.Trim(),
                    StringComparison.Ordinal) ||
                first.stackSize != second.stackSize ||
                Mathf.Abs(first.weight - second.weight) > 0.001f)
            {
                return false;
            }

            string firstItemId = first.itemID?.Trim() ?? string.Empty;
            string secondItemId = second.itemID?.Trim() ?? string.Empty;
            if (firstItemId.Length > 0 && secondItemId.Length > 0 &&
                !string.Equals(
                    firstItemId,
                    secondItemId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            // Live authored items retain their Unity object references and
            // those references disambiguate legacy variants that share the
            // same display name. JsonUtility-loaded records lose them; in
            // that case only an otherwise unique primitive fingerprint may
            // migrate, while ambiguous old data fails closed.
            if (first.icon != null && second.icon != null &&
                first.icon != second.icon)
                return false;
            if (first.itemMesh != null && second.itemMesh != null &&
                first.itemMesh != second.itemMesh)
                return false;
            if (first.itemIncreases != null && second.itemIncreases != null &&
                first.itemIncreases != second.itemIncreases)
                return false;

            return true;
        }

        private bool TryFindStableInventoryBindingById(
            string itemId,
            out CampaignInventoryItemBinding binding)
        {
            string candidate = itemId?.Trim() ?? string.Empty;
            if (string.Equals(
                    candidate,
                    LegacySafetyPickupMasterStableItemId,
                    StringComparison.Ordinal))
            {
                candidate = SafetyLeatherStableItemId;
            }

            foreach (CampaignInventoryItemBinding entry in
                     EnumerateStableInventoryCatalog())
            {
                if (string.Equals(
                        entry.ItemId,
                        candidate,
                        StringComparison.Ordinal))
                {
                    binding = entry;
                    return true;
                }
            }

            binding = null;
            return false;
        }

        private bool TryResolveStableInventoryItem(
            ItemStats item,
            out string checkpointItemId,
            out ItemStats definition)
        {
            checkpointItemId = string.Empty;
            definition = null;
            if (item == null)
                return false;

            if (TryFindStableInventoryBindingForItem(
                    item,
                    out CampaignInventoryItemBinding binding))
            {
                checkpointItemId = binding.ItemId;
                definition = binding.ItemData;
                return true;
            }

            string serializedItemId = item.itemID?.Trim() ?? string.Empty;
            if (!TryResolveNativeSafetyItemDefinition(
                    serializedItemId,
                    out definition) ||
                !HaveSameStableItemFingerprint(item, definition))
            {
                definition = null;
                return false;
            }

            checkpointItemId = NativeSafetyInventoryItemIdPrefix +
                               serializedItemId;
            return true;
        }

        private bool TryResolveStableCheckpointItem(
            string checkpointItemId,
            out ItemStats definition)
        {
            definition = null;
            if (TryFindStableInventoryBindingById(
                    checkpointItemId,
                    out CampaignInventoryItemBinding binding))
            {
                definition = binding.ItemData;
                return definition != null;
            }

            string candidate = checkpointItemId?.Trim() ?? string.Empty;
            if (!candidate.StartsWith(
                    NativeSafetyInventoryItemIdPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return TryResolveNativeSafetyItemDefinition(
                candidate.Substring(NativeSafetyInventoryItemIdPrefix.Length),
                out definition);
        }

        private bool TryResolveNativeSafetyItemDefinition(
            string serializedItemId,
            out ItemStats definition)
        {
            definition = null;
            string candidate = serializedItemId?.Trim() ?? string.Empty;
            if (candidate.Length == 0)
                return false;

            ItemDatabase itemDatabase = gameManager.instance?.itemDatabase;
            ItemStats registered = itemDatabase?.GetByID(candidate);
            if (registered == null ||
                !string.Equals(
                    registered.itemID?.Trim(),
                    candidate,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(registered.itemName) ||
                registered.quantity <= 0 || registered.stackSize <= 0 ||
                registered.weight < 0f ||
                // Campaign-owned tokens must stay on their explicit stable
                // binding path; a crafted native namespace entry must not
                // bypass their campaign semantics.
                TryFindStableInventoryBindingForItem(registered, out _))
            {
                return false;
            }

            definition = registered;
            return true;
        }

        private IEnumerable<CampaignInventoryItemBinding>
            EnumerateStableInventoryCatalog()
        {
            foreach (CampaignInventoryItemBinding binding in
                     itemCatalog ?? Array.Empty<CampaignInventoryItemBinding>())
            {
                if (binding != null)
                    yield return binding;
            }

            foreach (CampaignInventoryItemBinding binding in
                     safetyInventoryCatalog ??
                     Array.Empty<CampaignInventoryItemBinding>())
            {
                if (binding != null)
                    yield return binding;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsPersistentAuthority())
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                sceneLoadedSubscribed = false;
                enabled = false;
                return;
            }

            if (restoreInProgress || !IsGameplayScene(scene.name))
            {
                return;
            }

            if (restoreFailedPending)
            {
                // A failed sanitizer or corrupt owned checkpoint must also
                // block Safety's automatic Start -> Load path. Otherwise the
                // unsanitized JsonUtility gun list can be indexed before this
                // adapter gets another frame to fail closed.
                Inventory failedInventory = FindPlayerInventory();
                playerController failedController = failedInventory != null
                    ? failedInventory.GetComponent<playerController>()
                    : null;
                if (failedController != null)
                {
                    failedController.gunInv = new List<gunStats>();
                    failedController.gunInvPos = 0;
                    BeginAutomaticLoadGuard(failedController);
                    restoreRoutine = StartCoroutine(
                        ReleaseAutomaticLoadGuardAfterStart());
                }


                CampaignEventUtility.Invoke(RestoreCompleted, false, this);
                return;
            }

            restoreInProgress = true;

            // sceneLoaded runs before Start. Suppress only the destination
            // Inventory so its unconditional Start allocation cannot race
            // playerController.Start -> gameManager.Load. The controller and
            // Safety load remain active.
            suppressedSceneInventory = FindPlayerInventory();
            if (suppressedSceneInventory != null)
            {
                suppressedSceneInventoryWasEnabled =
                    suppressedSceneInventory.enabled;
                suppressedSceneInventory.enabled = false;
            }

            Inventory sceneInventory = suppressedSceneInventory;
            gameManager sceneManager = gameManager.instance;
            playerController sceneController = sceneInventory != null
                ? sceneInventory.GetComponent<playerController>()
                : null;
            string preloadError = string.Empty;
            bool hasSceneAuthority = sceneInventory != null &&
                sceneManager != null && sceneController != null &&
                sceneManager.itemDatabase != null &&
                sceneManager.player == sceneInventory.gameObject &&
                sceneManager.playerController == sceneController;
            bool preloaded = hasSceneAuthority &&
                TryRunAutomaticLoadBeforePlayerStart(
                    scene.name,
                    sceneManager,
                    sceneInventory,
                    sceneController,
                    out preloadError);
            if (!preloaded)
            {
                if (sceneController != null)
                {
                    sceneController.gunInv = new List<gunStats>();
                    sceneController.gunInvPos = 0;
                    BeginAutomaticLoadGuard(sceneController);
                }

                restoreFailedPending = true;
                restoreInProgress = false;
                RestoreSuppressedSceneInventory();

                restoreRoutine = StartCoroutine(
                    ReleaseAutomaticLoadGuardAfterStart());
                CampaignEventUtility.Invoke(RestoreCompleted, false, this);
                return;
            }

            restoreRoutine = StartCoroutine(
                RestoreAfterSceneStart(scene.name));
        }

        private bool TryBeginPersistentAuthorityStartup()
        {
            if (startupInitializationAttempted)
                return false;

            if (!IsPersistentAuthority())
            {
                // A destination scene can briefly awaken its authored copy
                // after CampaignStateService has already rejected that copy.
                // It must never sanitize/load Safety data or survive long
                enabled = false;
                return false;
            }

            startupInitializationAttempted = true;
            return true;
        }

        private bool IsPersistentAuthority()
        {
            CampaignStateService authority = CampaignStateService.Instance;
            return authority != null && ReferenceEquals(
                authority.GetComponent<CampaignInventoryCarryover>(),
                this);
        }

        private bool TryRunAutomaticLoadBeforePlayerStart(
            string sceneName,
            gameManager manager,
            Inventory inventory,
            playerController controller,
            out string error)
        {
            int authoredMaximumHealth = controller.HP;
            float authoredMaximumStamina = controller.stam;
            try
            {
                // sceneLoaded runs after Awake/OnEnable and before Start. Run
                // Safety's public Load exactly once here, then guard its own
                // Start from repeating the ItemDatabase-backed rebuild. HP
                // and stamina are restored in finally so d656 Start captures
                // the authored maxima before spawnPlayer resets current HP.
                if (!ValidateSafetyNativeInventoryDatabase(
                        manager.itemDatabase,
                        out error))
                {
                    return false;
                }

                manager.Load();

                bool applyLoadedPosition = CampaignSafetySaveIntegration
                    .ShouldApplyLoadedPosition(
                        sceneName,
                        CampaignStateService.Instance);
                if (!applyLoadedPosition &&
                    !TryApplyAuthoredSpawnPose(
                        manager,
                        controller,
                        out error))
                {
                    return false;
                }

                if (!TryReadStableInventoryForScene(
                        sceneName,
                        out ItemStats[] stableItems,
                        out bool hasStableItems,
                        out error))
                {
                    return false;
                }

                if (hasStableItems)
                {
                    inventory.inventoryItems = stableItems;
                    float stableWeight = 0f;
                    foreach (ItemStats item in stableItems)
                    {
                        if (item != null)
                            stableWeight += item.weight;
                    }

                    inventory.inventoryWeight = stableWeight;
                }

                CampaignLoadoutEquipmentBridge bridge =
                    CampaignLoadoutEquipmentBridge.Instance;
                if (bridge == null ||
                    !bridge.TryRestoreStableGunCheckpoint(
                        controller,
                        sceneName,
                        out _,
                        out error))
                {
                    error = string.IsNullOrWhiteSpace(error)
                        ? "The stable Safety gun bridge is unavailable."
                        : error;
                    return false;
                }

                BeginAutomaticLoadGuard(controller);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                // d656 playerController.Start snapshots HP/stamina before it
                // calls spawnPlayer. A pre-Start Load must never turn the
                // saved current values into the session's maximum values.
                controller.HP = authoredMaximumHealth;
                controller.stam = authoredMaximumStamina;
            }
        }

        internal bool ValidateSafetyNativeInventoryDatabase(
            ItemDatabase itemDatabase,
            out string error)
        {
            if (itemDatabase == null)
            {
                error = "Safety automatic Load requires ItemDatabase.";
                return false;
            }

            if (!ValidateStableInventoryCatalog(out error))
                return false;

            foreach (CampaignInventoryItemBinding binding in
                     EnumerateStableInventoryCatalog())
            {
                if (!TryGetExpectedSerializedItemId(
                        binding.ItemId,
                        out string expectedItemId) ||
                    string.Equals(
                        binding.ItemId,
                        CursedRootShardStableItemId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        binding.ItemId,
                        HeartrootStableItemId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ItemStats registered = itemDatabase.GetByID(expectedItemId);
                // These four owned tokens intentionally adapt Safety items
                // with campaign-specific presentation or stack rules. Their
                // durable ID must resolve exactly through ItemDatabase, while
                // key/leather remain exact native fingerprints.
                bool campaignSemanticAlias =
                    string.Equals(
                        binding.ItemId,
                        SafetyRifleStableItemId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        binding.ItemId,
                        SafetyRifleAmmoStableItemId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        binding.ItemId,
                        SafetyRadarStableItemId,
                        StringComparison.Ordinal);
                if (registered == null ||
                    !string.Equals(
                        registered.itemID?.Trim(),
                        expectedItemId,
                        StringComparison.Ordinal) ||
                    (!campaignSemanticAlias &&
                     !HaveSameStableItemFingerprint(
                         binding.ItemData,
                         registered)))
                {
                    error =
                        $"ItemDatabase does not map Safety itemID '{expectedItemId}' to stable binding '{binding.ItemId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryApplyAuthoredSpawnPose(
            gameManager manager,
            playerController controller,
            out string error)
        {
            Transform spawn = manager?.playerSpawnPos != null
                ? manager.playerSpawnPos.transform
                : null;
            if (controller == null || spawn == null)
            {
                error =
                    "Cross-scene Safety Load requires the authored PlayerSpawnPos.";
                return false;
            }

            CharacterController characterController =
                controller.GetComponent<CharacterController>();
            bool controllerWasEnabled =
                characterController != null && characterController.enabled;
            try
            {
                if (controllerWasEnabled)
                    characterController.enabled = false;

                controller.transform.SetPositionAndRotation(
                    spawn.position,
                    spawn.rotation);
                Physics.SyncTransforms();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (controllerWasEnabled && characterController != null)
                    characterController.enabled = true;
            }
        }

        private void BeginAutomaticLoadGuard(playerController controller)
        {
            if (controller == null ||
                ReferenceEquals(automaticLoadGuardController, controller))
            {
                return;
            }

            ReleaseAutomaticLoadGuard();
            automaticLoadGuardController = controller;
            automaticLoadGuardPreviousNewGame = controller.newGame;
            controller.newGame = true;
        }

        private void ReleaseAutomaticLoadGuard()
        {
            if (automaticLoadGuardController == null)
                return;

            automaticLoadGuardController.newGame =
                automaticLoadGuardPreviousNewGame;
            automaticLoadGuardController = null;
            automaticLoadGuardPreviousNewGame = false;
        }

        private IEnumerator ReleaseAutomaticLoadGuardAfterStart()
        {
            yield return null;
            ReleaseAutomaticLoadGuard();
            restoreRoutine = null;
        }

        private IEnumerator RestoreAfterSceneStart(string sceneName)
        {
            // The sceneLoaded preflight already invoked Safety's public Load
            // once and installed a transient newGame guard. Let d656
            // playerController.Start capture the restored authored maxima,
            // initialize animation, and spawn at PlayerSpawnPos; then release
            // the guard and reapply saved current stats/same-scene position.
            yield return null;
            ReleaseAutomaticLoadGuard();

            float deadline = Time.unscaledTime + playerLookupTimeout;
            Inventory inventory;
            gameManager manager;
            playerController controller;
            do
            {
                inventory = FindPlayerInventory();
                manager = gameManager.instance;
                controller = inventory != null
                    ? inventory.GetComponent<playerController>()
                    : null;
                if (inventory != null && manager != null &&
                    manager.itemDatabase != null &&
                    manager.player == inventory.gameObject &&
                    controller != null &&
                    manager.playerController == controller)
                {
                    break;
                }

                yield return null;
            }
            while (Time.unscaledTime <= deadline);

            manager = gameManager.instance;
            controller = inventory != null
                ? inventory.GetComponent<playerController>()
                : null;
            if (inventory == null ||
                manager == null ||
                manager.itemDatabase == null ||
                manager.player != inventory.gameObject ||
                controller == null ||
                manager.playerController != controller)
            {
                CampaignLoadoutEquipmentBridge.Instance
                    ?.RollbackStableGunRestore();

                restoreFailedPending = true;
                RestoreSuppressedSceneInventory();
                restoreInProgress = false;
                restoreRoutine = null;
                CampaignEventUtility.Invoke(
                    RestoreCompleted,
                    false,
                    this);
                yield break;
            }

            List<ItemStats> reconciledItems;
            try
            {
                // Inventory.Start is Safety's public authority for the
                // currently authored capacity. Establish it before decoding
                // a checkpoint whose older array may have a different shape.
                inventory.Start();
                inventory.inventoryWeight = 0f;
                if (!TryGetInventoryCapacity(
                        inventory,
                        out int inventoryCapacity,
                        out string capacityError))
                {
                    throw new InvalidOperationException(capacityError);
                }

                if (!CampaignSafetySaveIntegration.TryLoadGameData(
                        out GameData loadedData,
                        out string loadError))
                {
                    throw new InvalidOperationException(loadError);
                }

                bool applyLoadedPosition =
                    CampaignSafetySaveIntegration.ShouldApplyLoadedPosition(
                        sceneName,
                        CampaignStateService.Instance);
                ApplyLoadedPlayerState(
                    controller,
                    loadedData,
                    applyLoadedPosition,
                    false);

                if (!TryReadStableInventoryForScene(
                        sceneName,
                        out ItemStats[] stableItems,
                        out bool hasStableItems,
                        out string stableInventoryError))
                {
                    throw new InvalidOperationException(
                        stableInventoryError);
                }

                ItemStats[] loadedInventory;
                if (hasStableItems)
                {
                    loadedInventory = stableItems;
                }
                else if (!TryDecodeSafetyInventory(
                             loadedData,
                             manager.itemDatabase,
                             inventoryCapacity,
                             out loadedInventory,
                             out string safetyInventoryError))
                {
                    throw new InvalidOperationException(
                        safetyInventoryError);
                }
                if (!TryRestoreStableGunsForScene(
                        controller,
                        sceneName,
                        out string stableGunError))
                {
                    throw new InvalidOperationException(
                        stableGunError);
                }

                if (!TryBuildReconciledInventory(
                        loadedInventory,
                        hasSnapshot,
                        inventoryCapacity,
                        out reconciledItems,
                        out string reconcileError))
                {
                    throw new InvalidOperationException(reconcileError);
                }

            }
            catch (Exception exception)
            {
                CampaignLoadoutEquipmentBridge.Instance
                    ?.RollbackStableGunRestore();

                restoreFailedPending = true;
                RestoreSuppressedSceneInventory();
                restoreInProgress = false;
                restoreRoutine = null;
                CampaignEventUtility.Invoke(RestoreCompleted, false, this);
                yield break;
            }

            RestoreSuppressedSceneInventory();
            yield return null;

            bool restored = false;
            try
            {
                // Start is public Safety API. Calling it here after the
                // enable boundary guarantees counters, weight, and storage
                // are rebuilt together through AddItem regardless of native
                // Start scheduling.
                if (!TryPopulateFreshInventory(
                        inventory,
                        reconciledItems,
                        out string reconcileError))
                {
                    throw new InvalidOperationException(reconcileError);
                }

                if (!hasSnapshot &&
                    !TryAdoptLiveInventoryDurably(
                        inventory,
                        out reconcileError))
                {
                    throw new InvalidOperationException(reconcileError);
                }

                restoreFailedPending = false;

                if (!TryReconcileHeartrootToken(
                        inventory,
                        out reconcileError))
                {
                    throw new InvalidOperationException(reconcileError);
                }

                //if (!TryRefreshSafetySaveAndInventoryPair(
                //        inventory,
                //        out reconcileError))
                //{
                //    throw new InvalidOperationException(reconcileError);
                //}

                CampaignSafetySaveIntegration.CompletePendingArrival(
                    sceneName);
                CampaignLoadoutEquipmentBridge.Instance
                    ?.CommitStableGunRestore();
                restored = true;
            }
            catch (Exception exception)
            {

                CampaignLoadoutEquipmentBridge.Instance
                    ?.RollbackStableGunRestore();
                restoreFailedPending = true;
                restored = false;
            }

            restoreInProgress = false;
            restoreRoutine = null;
            CampaignEventUtility.Invoke(RestoreCompleted, restored, this);
        }

        private bool TryMapExactCatalogSnapshot(
            string[] savedIds,
            int[] savedQuantities,
            out int[] mapped)
        {
            mapped = Array.Empty<int>();
            int catalogCount = itemCatalog?.Length ?? 0;
            if (savedIds == null || savedQuantities == null ||
                savedIds.Length == 0 ||
                savedIds.Length != savedQuantities.Length ||
                savedIds.Length < catalogCount - 1 ||
                savedIds.Length > catalogCount + 1)
            {
                return false;
            }

            var next = new int[catalogCount];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool discardedRetiredEntry = false;
            for (int savedIndex = 0;
                 savedIndex < savedIds.Length;
                 savedIndex++)
            {
                string id = savedIds[savedIndex]?.Trim() ?? string.Empty;
                if (id.Length == 0 || !seen.Add(id) ||
                    savedQuantities[savedIndex] < 0)
                    return false;

                if (string.Equals(
                        id,
                        RetiredNameStoneStableItemId,
                        StringComparison.Ordinal))
                {
                    if (discardedRetiredEntry)
                    {
                        return false;
                    }

                    discardedRetiredEntry = true;
                    continue;
                }

                bool matched = false;
                for (int catalogIndex = 0;
                     catalogIndex < catalogCount;
                     catalogIndex++)
                {
                    if (string.Equals(
                            id,
                            itemCatalog[catalogIndex]?.ItemId,
                            StringComparison.Ordinal))
                    {
                        next[catalogIndex] = savedQuantities[savedIndex];
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    return false;
            }

            for (int catalogIndex = 0;
                 catalogIndex < catalogCount;
                 catalogIndex++)
            {
                string catalogId = itemCatalog[catalogIndex]?.ItemId ?? string.Empty;
                if (!seen.Contains(catalogId) && !string.Equals(
                        catalogId,
                        HeartrootStableItemId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            mapped = next;
            return true;
        }

        private bool TryRefreshSafetySaveAndInventoryPair(
            Inventory inventory,
            out string error)
        {
            error = string.Empty;
            if (inventory == null)
            {
                error = "The active Player Inventory is missing.";
                return false;
            }

            if (!ValidateCatalog(out error))
            {
                return false;
            }

            string[] itemIds = GetCatalogIds();
            int[] quantities = new int[itemCatalog.Length];
            for (int index = 0; index < itemCatalog.Length; index++)
            {
                quantities[index] = Mathf.Max(
                    0,
                    inventory.FindItem(itemCatalog[index].ItemData).Value);
            }

            CampaignStateService state = CampaignStateService.Instance;
            if (state == null || !state.TryGetInventoryCarryover(
                    out string[] previousIds,
                    out int[] previousQuantities))
            {
                error =
                    "The normalized campaign inventory checkpoint is unavailable.";
                return false;
            }

            if (!state.UpdateInventoryCarryover(itemIds, quantities) ||
                !state.TryGetInventoryCarryover(
                    out string[] durableIds,
                    out int[] durableQuantities) ||
                !HaveExactInventorySnapshot(
                    itemIds,
                    quantities,
                    durableIds,
                    durableQuantities))
            {
                bool campaignRolledBack = state.UpdateInventoryCarryover(
                    previousIds,
                    previousQuantities);
                error =
                    "The live Inventory could not be committed to the campaign checkpoint before refreshing Safety.";
                if (!campaignRolledBack)
                {
                    MarkInventoryRecoveryPending(
                        error +
                        " Campaign rollback also failed; reload before traveling.");
                    error += " Campaign rollback also failed.";
                }

                return false;
            }

            if (!CampaignSafetySaveIntegration.TryBeginCurrentGameSave(
                    out CampaignSafetySaveIntegration.SaveTransaction
                        transaction,
                    out error))
            {
                bool campaignRolledBack = state.UpdateInventoryCarryover(
                    previousIds,
                    previousQuantities);
                if (!campaignRolledBack)
                {
                    MarkInventoryRecoveryPending(
                        "Safety refresh failed before its transaction began, and the campaign inventory rollback also failed. Reload before traveling.");
                    error += " Campaign rollback also failed.";
                }

                return false;
            }

            if (!CampaignSafetySaveIntegration
                    .TryWriteCampaignInventoryCheckpoint(
                        itemIds,
                        quantities,
                        out error))
            {
                bool safetyRolledBack = transaction.TryRollback(
                    out string rollbackError);
                bool campaignRolledBack = state.UpdateInventoryCarryover(
                    previousIds,
                    previousQuantities);
                if (!safetyRolledBack || !campaignRolledBack)
                {
                    MarkInventoryRecoveryPending(
                        "The refreshed Safety/campaign inventory pair failed and could not be rolled back exactly. Reload before traveling.");
                }

                if (!string.IsNullOrWhiteSpace(rollbackError))
                    error += " Safety rollback: " + rollbackError.Trim();
                if (!campaignRolledBack)
                    error += " Campaign rollback also failed.";
                return false;
            }

            transaction.Commit();
            carriedQuantities = (int[])quantities.Clone();
            hasSnapshot = true;
            restoreFailedPending = false;
            return true;
        }

        private static bool HaveExactInventorySnapshot(
            string[] expectedIds,
            int[] expectedQuantities,
            string[] actualIds,
            int[] actualQuantities)
        {
            if (expectedIds == null || expectedQuantities == null ||
                actualIds == null || actualQuantities == null ||
                expectedIds.Length != expectedQuantities.Length ||
                expectedIds.Length != actualIds.Length ||
                expectedIds.Length != actualQuantities.Length)
            {
                return false;
            }

            for (int index = 0; index < expectedIds.Length; index++)
            {
                if (!string.Equals(
                        expectedIds[index],
                        actualIds[index],
                        StringComparison.Ordinal) ||
                    expectedQuantities[index] != actualQuantities[index])
                {
                    return false;
                }
            }

            return true;
        }

        private void RestoreSuppressedSceneInventory()
        {
            if (suppressedSceneInventory == null)
                return;

            suppressedSceneInventory.enabled =
                suppressedSceneInventoryWasEnabled;
            suppressedSceneInventory = null;
            suppressedSceneInventoryWasEnabled = false;
        }

        private bool TryBuildReconciledInventory(
            ItemStats[] safetyLoadedItems,
            bool campaignSnapshotIsAuthoritative,
            int inventoryCapacity,
            out List<ItemStats> reconciledItems,
            out string error)
        {
            reconciledItems = new List<ItemStats>();
            if (!ValidateStableInventoryCatalog(out error))
                return false;

            if (!IsSupportedInventoryCapacity(inventoryCapacity))
            {
                error = "The current Safety Inventory capacity is invalid.";
                return false;
            }

            ItemStats[] loaded = safetyLoadedItems ?? Array.Empty<ItemStats>();
            if (loaded.Length > MaximumSupportedInventoryCapacity)
            {
                error = "Safety inventory checkpoint exceeds the bounded slot limit.";
                return false;
            }

            for (int slot = inventoryCapacity; slot < loaded.Length; slot++)
            {
                ItemStats overflow = loaded[slot];
                if (overflow != null &&
                    (!string.IsNullOrWhiteSpace(overflow.itemName) ||
                     overflow.quantity != 0))
                {
                    error =
                        $"Safety inventory contains occupied legacy slot {slot} beyond the current {inventoryCapacity}-slot Player contract.";
                    return false;
                }
            }

            var slots = new ItemStats[inventoryCapacity];
            int loadedSlots = Mathf.Min(loaded.Length, inventoryCapacity);
            for (int slot = 0; slot < loadedSlots; slot++)
            {
                ItemStats item = loaded[slot];
                string itemName = item?.itemName?.Trim() ?? string.Empty;
                if (itemName.Length == 0 || item == null ||
                    item.quantity <= 0)
                {
                    continue;
                }

                if (!TryResolveStableInventoryItem(
                        item,
                        out _,
                        out ItemStats definition))
                {
                    error =
                        $"Safety inventory item '{itemName}' is not in the authored stable catalog or active Safety ItemDatabase.";
                    return false;
                }

                if (definition == null || definition.stackSize <= 0 ||
                    item.quantity > definition.stackSize)
                {
                    error =
                        $"Safety inventory slot {slot} has an invalid '{itemName}' stack.";
                    return false;
                }

                slots[slot] =
                    CampaignInventoryTokenUtility.CloneItemStats(
                        definition,
                        item.quantity);
            }

            if (campaignSnapshotIsAuthoritative)
            {
                EnsureQuantityBuffer();
                for (int index = 0; index < itemCatalog.Length; index++)
                {
                    ItemStats definition = itemCatalog[index].ItemData;
                    string itemName = definition.itemName.Trim();
                    int target = Mathf.Max(0, carriedQuantities[index]);
                    int current = 0;
                    var preferredSlots = new List<int>();
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        if (!string.Equals(
                                slots[slot]?.itemName?.Trim(),
                                itemName,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        current += slots[slot].quantity;
                        preferredSlots.Add(slot);
                    }

                    if (current == target)
                        continue;

                    foreach (int slot in preferredSlots)
                        slots[slot] = null;

                    int remaining = target;
                    int preferredIndex = 0;
                    while (remaining > 0)
                    {
                        int destination = -1;
                        while (preferredIndex < preferredSlots.Count &&
                               destination < 0)
                        {
                            int candidate = preferredSlots[preferredIndex++];
                            if (slots[candidate] == null)
                                destination = candidate;
                        }

                        if (destination < 0)
                        {
                            for (int slot = 0; slot < slots.Length; slot++)
                            {
                                if (slots[slot] == null)
                                {
                                    destination = slot;
                                    break;
                                }
                            }
                        }

                        if (destination < 0)
                        {
                            error =
                                $"The reconciled Safety inventory cannot fit '{itemName}' in the current {inventoryCapacity}-slot Player contract.";
                            return false;
                        }

                        int stackQuantity = Mathf.Min(
                            remaining,
                            definition.stackSize);
                        slots[destination] =
                            CampaignInventoryTokenUtility.CloneItemStats(
                                definition,
                                stackQuantity);
                        remaining -= stackQuantity;
                    }
                }
            }

            reconciledItems = new List<ItemStats>(slots);
            error = string.Empty;
            return true;
        }

        private static bool TryDecodeSafetyInventory(
            GameData loadedData,
            ItemDatabase itemDatabase,
            int inventoryCapacity,
            out ItemStats[] decodedItems,
            out string error)
        {
            decodedItems = Array.Empty<ItemStats>();
            if (!IsSupportedInventoryCapacity(inventoryCapacity))
            {
                error = "The current Safety Inventory capacity is invalid.";
                return false;
            }

            if (loadedData == null)
            {
                error = "Safety GameData is missing.";
                return false;
            }

            if (itemDatabase == null)
            {
                error = "Safety ItemDatabase is missing.";
                return false;
            }

            ItemSaveData[] savedItems = loadedData._savInventory ??
                                        Array.Empty<ItemSaveData>();
            if (savedItems.Length > MaximumSupportedInventoryCapacity)
            {
                error = "Safety inventory save exceeds the bounded slot limit.";
                return false;
            }

            for (int slot = inventoryCapacity; slot < savedItems.Length; slot++)
            {
                ItemSaveData overflow = savedItems[slot];
                if (overflow != null &&
                    (!string.IsNullOrWhiteSpace(overflow.itemID) ||
                     overflow.quantity != 0))
                {
                    error =
                        $"Safety inventory contains saved data beyond the authored {inventoryCapacity}-slot Player contract.";
                    return false;
                }
            }

            var restored = new ItemStats[inventoryCapacity];
            int slotsToRead = Mathf.Min(savedItems.Length, inventoryCapacity);
            for (int slot = 0; slot < slotsToRead; slot++)
            {
                ItemSaveData saved = savedItems[slot];
                if (saved == null)
                    continue;

                string itemId = saved.itemID?.Trim() ?? string.Empty;
                if (itemId.Length == 0 && saved.quantity == 0)
                    continue;

                if (itemId.Length == 0 || saved.quantity <= 0)
                {
                    error =
                        $"Safety inventory slot {slot} has an invalid item ID or quantity.";
                    return false;
                }

                ItemStats definition = itemDatabase.GetByID(itemId);
                if (definition == null)
                {
                    error =
                        $"Safety ItemDatabase cannot resolve saved item ID '{itemId}'.";
                    return false;
                }

                if (definition.stackSize <= 0 ||
                    saved.quantity > definition.stackSize)
                {
                    error =
                        $"Safety inventory slot {slot} has an invalid quantity for '{itemId}'.";
                    return false;
                }

                restored[slot] =
                    CampaignInventoryTokenUtility.CloneItemStats(
                        definition,
                        saved.quantity);
                if (!string.Equals(
                        restored[slot].itemID,
                        itemId,
                        StringComparison.Ordinal))
                {
                    error =
                        $"Safety ItemDatabase resolved '{itemId}' to a mismatched item definition.";
                    return false;
                }
            }

            decodedItems = restored;
            error = string.Empty;
            return true;
        }

        private bool TryPopulateFreshInventory(
            Inventory inventory,
            IEnumerable<ItemStats> items,
            out string error)
        {
            inventory.Start();
            inventory.inventoryWeight = 0f;
            if (!TryGetInventoryCapacity(inventory, out _, out error))
            {
                return false;
            }

            if (TryPopulateInventory(inventory, items, out error))
                return true;

            string populateError = error;
            bool cleared = TryClearTrackedInventory(
                inventory,
                out string clearError);
            inventory.Start();
            inventory.inventoryWeight = 0f;
            error = cleared
                ? populateError
                : populateError + " Exact-slot rollback failed: " + clearError;
            return false;
        }

        private bool TryPopulateInventory(
            Inventory inventory,
            IEnumerable<ItemStats> items,
            out string error)
        {
            var expected = new Dictionary<string, int>(StringComparer.Ordinal);
            GameObject carrier = null;
            try
            {
                if (!ValidateStableInventoryCatalog(out error))
                    return false;

                if (!TryGetInventoryCapacity(
                        inventory,
                        out int inventoryCapacity,
                        out error))
                {
                    return false;
                }

                for (int slot = 0; slot < inventory.inventoryItems.Length; slot++)
                {
                    if (!inventory.IsSlotEmpty(slot))
                    {
                        error =
                            "Inventory exact-slot rebuild requires every destination slot to be empty.";
                        return false;
                    }
                }

                if (Mathf.Abs(inventory.inventoryWeight) > 0.001f)
                {
                    error =
                        "Inventory exact-slot rebuild requires zero starting weight.";
                    return false;
                }

                var desired = new ItemStats[inventoryCapacity];
                int sourceIndex = 0;
                foreach (ItemStats item in items ?? Array.Empty<ItemStats>())
                {
                    if (sourceIndex >= desired.Length)
                    {
                        error =
                            $"Inventory exact-slot rebuild received more than the current {inventoryCapacity} slots.";
                        return false;
                    }

                    if (item == null || string.IsNullOrWhiteSpace(item.itemName) ||
                        item.quantity <= 0 || item.stackSize <= 0)
                    {
                        sourceIndex++;
                        continue;
                    }

                    if (item.quantity > item.stackSize ||
                        !TryResolveStableInventoryItem(
                            item,
                            out _,
                            out ItemStats definition))
                    {
                        error =
                            $"Inventory slot {sourceIndex} contains an unsupported or invalid stable item.";
                        return false;
                    }

                    ItemStats desiredItem =
                        CampaignInventoryTokenUtility.CloneItemStats(
                            definition,
                            item.quantity);
                    desired[sourceIndex] = desiredItem;

                    carrier = Instantiate(safetyInventoryCarrierPrefab);
                    carrier.SetActive(false);
                    Item carrierItem = carrier.GetComponent<Item>();
                    carrierItem.item =
                        CampaignInventoryTokenUtility.CloneItemStats(
                            definition,
                            item.quantity);
                    inventory.AddItem(carrier, sourceIndex);
                    if (Application.isPlaying)
                        Destroy(carrier);
                    else
                        DestroyImmediate(carrier);
                    carrier = null;

                    expected.TryGetValue(
                        desiredItem.itemName,
                        out int previousQuantity);
                    long combined =
                        (long)previousQuantity + desiredItem.quantity;
                    if (combined > int.MaxValue)
                    {
                        error =
                            $"Inventory quantity for '{desiredItem.itemName}' overflowed during rebuild.";
                        return false;
                    }

                    expected[desiredItem.itemName] = (int)combined;
                    sourceIndex++;
                }

                for (int slot = 0; slot < desired.Length; slot++)
                {
                    ItemStats expectedSlot = desired[slot];
                    ItemStats actualSlot = inventory.inventoryItems[slot];
                    if (expectedSlot == null)
                    {
                        if (actualSlot != null &&
                            !string.IsNullOrWhiteSpace(actualSlot.itemName) &&
                            actualSlot.quantity > 0)
                        {
                            error =
                                $"Inventory exact-slot rebuild unexpectedly occupied slot {slot}.";
                            return false;
                        }

                        continue;
                    }

                    if (actualSlot == null ||
                        !string.Equals(
                            actualSlot.itemName,
                            expectedSlot.itemName,
                            StringComparison.Ordinal) ||
                        actualSlot.quantity != expectedSlot.quantity)
                    {
                        error =
                            $"Inventory exact-slot rebuild did not preserve slot {slot}.";
                        return false;
                    }
                }

                foreach (KeyValuePair<string, int> pair in expected)
                {
                    var probe = new ItemStats { itemName = pair.Key };
                    if (inventory.FindItem(probe).Value != pair.Value)
                    {
                        error =
                            $"Inventory rebuilt '{pair.Key}' to an unexpected quantity.";
                        return false;
                    }
                }

                float expectedWeight = 0f;
                foreach (ItemStats item in desired)
                {
                    if (item != null)
                        expectedWeight += item.weight;
                }

                if (Mathf.Abs(inventory.inventoryWeight - expectedWeight) >
                    0.001f)
                {
                    error =
                        "Inventory exact-slot rebuild produced an unexpected weight.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (carrier != null)
                {
                    if (Application.isPlaying)
                        Destroy(carrier);
                    else
                        DestroyImmediate(carrier);
                }
            }
        }

        private static bool TryClearTrackedInventory(
            Inventory inventory,
            out string error)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                foreach (ItemStats item in
                         inventory.inventoryItems ?? Array.Empty<ItemStats>())
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.itemName) ||
                        item.quantity <= 0)
                    {
                        continue;
                    }

                    totals.TryGetValue(item.itemName, out int quantity);
                    totals[item.itemName] = quantity + item.quantity;
                }

                foreach (KeyValuePair<string, int> pair in totals)
                    inventory.RemoveItem(pair.Key, pair.Value, false);

                foreach (ItemStats item in
                         inventory.inventoryItems ?? Array.Empty<ItemStats>())
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.itemName) &&
                        item.quantity > 0)
                    {
                        error =
                            $"Inventory item '{item.itemName}' could not be cleared.";
                        return false;
                    }
                }

                // Safety tracks weight separately from its private occupied
                // slot count. Once every slot was removed through public API,
                // zero is the exact recalculated weight even when an older
                // save supplied stale metadata.
                inventory.inventoryWeight = 0f;

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private bool TryAdoptLiveInventoryDurably(
            Inventory inventory,
            out string error)
        {
            string[] ids = GetCatalogIds();
            int[] quantities = new int[itemCatalog.Length];
            for (int index = 0; index < itemCatalog.Length; index++)
            {
                quantities[index] = Mathf.Max(
                    0,
                    inventory.FindItem(itemCatalog[index].ItemData).Value);
            }

            CampaignStateService state = CampaignStateService.Instance;
            if (state == null ||
                !state.UpdateInventoryCarryover(ids, quantities))
            {
                error =
                    "The normalized Safety inventory could not be adopted by the campaign save.";
                return false;
            }

            carriedQuantities = quantities;
            hasSnapshot = true;
            restoreFailedPending = false;
            error = string.Empty;
            return true;
        }

        private static void ApplyLoadedPlayerState(
            playerController controller,
            GameData loadedData,
            bool applyPosition,
            bool applyGuns)
        {
            controller.HP = loadedData._savHP;
            controller.stam = loadedData._savstam;
            if (controller.HP <= 0)
            {
                // Repair legacy death saves after Start has captured the
                // authored upgraded maxima. The next paired Safety refresh
                // persists the corrected current health and stamina.
                controller.UpdateUpgradedStats("all");
            }
            controller.hasFlashlight = loadedData._savhasFlashlight;
            if (applyGuns)
            {
                WeaponDatabase weaponDatabase =
                    gameManager.instance != null
                        ? gameManager.instance.weaponDatabase
                        : null;
                if (SafetyWeaponSaveUtility.TryRestoreRuntimeInventory(
                        loadedData._savgunInv,
                        loadedData._savgunInvPos,
                        weaponDatabase,
                        out List<gunStats> restoredGuns,
                        out int restoredGunSelection,
                        out string gunRestoreError))
                {
                    controller.gunInv = restoredGuns;
                    controller.gunInvPos = restoredGunSelection;
                }
            }

            if (applyPosition && loadedData._savplayerPosition != null &&
                loadedData._savplayerPosition.Length >= 3)
            {
                controller.transform.position = new Vector3(
                    loadedData._savplayerPosition[0],
                    loadedData._savplayerPosition[1],
                    loadedData._savplayerPosition[2]);
                Physics.SyncTransforms();
            }

            // spawnPlayer resets HP after Safety Load. Reapply it and refresh
            // all public HUD surfaces after the Start boundary.
            controller.updatePlayerUI();
            controller.updatePlayerAmmo();
            controller.updatePlayerWeight();
        }

        private int FindCatalogIndexByName(string itemName)
        {
            for (int index = 0; index < itemCatalog.Length; index++)
            {
                if (string.Equals(
                        itemCatalog[index]?.ItemData?.itemName?.Trim(),
                        itemName,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int GetInventorySlotCount(Inventory inventory)
        {
            int count = 0;
            while (count < MaximumSupportedInventoryCapacity &&
                   inventory.IsValidIndex(count))
                count++;
            return count;
        }

        private static bool IsSupportedInventoryCapacity(int capacity)
        {
            return capacity > 0 &&
                   capacity <= MaximumSupportedInventoryCapacity;
        }

        private static bool TryGetInventoryCapacity(
            Inventory inventory,
            out int capacity,
            out string error)
        {
            capacity = 0;
            if (inventory == null || inventory.inventoryItems == null)
            {
                error = "The Safety Player Inventory is unavailable.";
                return false;
            }

            capacity = GetInventorySlotCount(inventory);
            if (!IsSupportedInventoryCapacity(capacity) ||
                capacity != inventory.inventoryItems.Length)
            {
                error =
                    "The Safety Player Inventory did not initialize a valid bounded slot array.";
                capacity = 0;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsGameplayScene(string sceneName)
        {
            return sceneName == CampaignSceneNames.FarmPrologueHub ||
                   sceneName == CampaignSceneNames.OpenWorld;
        }

        private bool TryRestoreExactTotals(Inventory inventory)
        {
            int[] startingTotals = new int[itemCatalog.Length];
            int slotCount = 0;
            int occupiedSlots = 0;
            while (slotCount < 4096 && inventory.IsValidIndex(slotCount))
            {
                if (!inventory.IsSlotEmpty(slotCount))
                    occupiedSlots++;
                slotCount++;
            }

            int requiredSlots = 0;
            for (int index = 0; index < itemCatalog.Length; index++)
            {
                ItemStats item = itemCatalog[index].ItemData;
                startingTotals[index] = Mathf.Max(
                    0,
                    inventory.FindItem(item).Value);
                requiredSlots += Mathf.CeilToInt(
                    Mathf.Max(0, carriedQuantities[index]) /
                    (float)item.stackSize);
            }

            if (slotCount <= 0 || occupiedSlots != 0 ||
                requiredSlots > slotCount)
            {

                return false;
            }

            try
            {
                for (int index = 0; index < itemCatalog.Length; index++)
                {
                    ItemStats item = itemCatalog[index].ItemData;
                    int target = Mathf.Max(0, carriedQuantities[index]);
                    int current = Mathf.Max(
                        0,
                        inventory.FindItem(item).Value);
                    if (current < target)
                    {
                        inventory.AddItem(
                            CampaignInventoryTokenUtility.CloneItemStats(
                                item,
                                target - current));

                        current = Mathf.Max(
                            0,
                            inventory.FindItem(item).Value);
                        if (current != target)
                        {
                            throw new InvalidOperationException(
                                $"Campaign inventory carryover could not grant the exact '{item.itemName}' quantity in one bounded public-API operation.");
                        }
                    }

                    if (current != target)
                    {
                        throw new InvalidOperationException(
                            $"Campaign inventory carryover could not restore the exact '{item.itemName}' total.");
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                _ = TryRollbackToStartingTotals(
                    inventory,
                    startingTotals,
                    out _);

                return false;
            }
        }


        private bool TryRollbackToStartingTotals(
            Inventory inventory,
            int[] startingTotals,
            out string error)
        {
            bool succeeded = true;
            string failure = string.Empty;

            for (int index = 0; index < itemCatalog.Length; index++)
            {
                ItemStats item = itemCatalog[index].ItemData;
                string itemName = item.itemName;

                try
                {
                    int current = Mathf.Max(
                        0,
                        inventory.FindItem(item).Value);
                    int starting = Mathf.Max(0, startingTotals[index]);
                    if (current > starting)
                    {
                        inventory.RemoveItem(
                            itemName,
                            current - starting,
                            false);
                    }

                    int restored = Mathf.Max(
                        0,
                        inventory.FindItem(item).Value);
                    if (restored != starting)
                    {
                        succeeded = false;
                        failure +=
                            $" '{itemName}' expected {starting}, found {restored}.";
                    }
                }
                catch (Exception exception)
                {
                    succeeded = false;
                    failure += $" '{itemName}': {exception.Message}.";
                }
            }

            error = failure.Trim();
            return succeeded;
        }

        private Inventory FindPlayerInventory()
        {
            if (string.IsNullOrWhiteSpace(playerTag))
                return null;

            try
            {
                return GameObject.FindGameObjectWithTag(playerTag)
                    ?.GetComponent<Inventory>();
            }
            catch (UnityException exception)
            {

                return null;
            }
        }

        private void EnsureQuantityBuffer()
        {
            int count = itemCatalog?.Length ?? 0;
            if (carriedQuantities == null || carriedQuantities.Length != count)
            {
                carriedQuantities = new int[count];
            }
        }

        private void CaptureCurrentGameplayInventory()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (restoreInProgress ||
                restoreFailedPending ||
                !IsGameplayScene(activeScene.name))
            {
                return;
            }

            CaptureForTravel();
        }

        private void LoadDurableSnapshot()
        {
            CampaignStateService state = CampaignStateService.Instance;
            if (state == null)
            {
                return;
            }

            bool snapshotShapeValid = state.TryGetInventoryCarryover(
                out string[] ids,
                out int[] quantities);
            if (!snapshotShapeValid)
            {
                if ((ids?.Length ?? 0) > 0 ||
                    (quantities?.Length ?? 0) > 0)
                {
                    restoreFailedPending = true;
                    hasSnapshot = false;

                }

                return;
            }

            if (ids == null || quantities == null ||
                ids.Length == 0 || ids.Length != quantities.Length)
            {
                restoreFailedPending = true;
                hasSnapshot = false;

                return;
            }

            int[] validatedQuantities = new int[itemCatalog.Length];
            var seenSavedIds = new System.Collections.Generic.HashSet<string>(
                StringComparer.Ordinal);
            int matchedIds = 0;
            for (int savedIndex = 0; savedIndex < ids.Length; savedIndex++)
            {
                string savedId = ids[savedIndex]?.Trim() ?? string.Empty;
                if (savedId.Length == 0 || !seenSavedIds.Add(savedId) ||
                    quantities[savedIndex] < 0)
                {
                    restoreFailedPending = true;
                    hasSnapshot = false;

                    return;
                }

                for (int catalogIndex = 0;
                     catalogIndex < itemCatalog.Length;
                     catalogIndex++)
                {
                    if (!string.Equals(
                            savedId,
                            itemCatalog[catalogIndex]?.ItemId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    validatedQuantities[catalogIndex] =
                        quantities[savedIndex];
                    matchedIds++;
                    break;
                }
            }

            // IDs are the durable compatibility contract. Added catalog
            // entries restore as zero and removed legacy entries are ignored,
            // so catalog reordering or additive updates do not discard a
            // valid older snapshot.
            carriedQuantities = validatedQuantities;
            hasSnapshot = matchedIds > 0;
            restoreFailedPending = false;
        }

        private string[] GetCatalogIds()
        {
            string[] ids = new string[itemCatalog.Length];
            for (int index = 0; index < itemCatalog.Length; index++)
            {
                ids[index] = itemCatalog[index].ItemId;
            }

            return ids;
        }

        private void OnValidate()
        {
            playerTag = playerTag?.Trim() ?? string.Empty;
            playerLookupTimeout = Mathf.Max(0f, playerLookupTimeout);
            itemCatalog ??= Array.Empty<CampaignInventoryItemBinding>();
            safetyInventoryCatalog ??=
                Array.Empty<CampaignInventoryItemBinding>();
        }
    }

    /// <summary>
    /// Owned transaction boundary around Safety's public SaveSystem and
    /// gameManager APIs. The small sidecar records the scene in which the
    /// position was saved because Safety GameData has no scene identifier.
    /// </summary>
    internal static class CampaignSafetySaveIntegration
    {
        private const int ContextVersion = 5;
        private const string SafetySaveFileName = "gamesave.json";
        private const string ContextFileName =
            "bloodroot-safety-save-context.json";

        [Serializable]
        private sealed class SafetySaveContext
        {
            public int version = ContextVersion;
            public string saveSceneName = string.Empty;
            public string pendingArrivalSceneName = string.Empty;
            public string[] campaignInventoryItemIds = Array.Empty<string>();
            public int[] campaignInventoryQuantities = Array.Empty<int>();
            public string campaignCheckpointJson = string.Empty;
            public CampaignStableInventoryCheckpoint stableInventory =
                CampaignStableInventoryCheckpoint.CreateEmpty();
            public CampaignStableGunCheckpoint stableGuns =
                CampaignStableGunCheckpoint.CreateEmpty();
        }

        internal readonly struct FileCheckpoint
        {
            public FileCheckpoint(bool existed, byte[] bytes)
            {
                Existed = existed;
                Bytes = bytes ?? Array.Empty<byte>();
            }

            public bool Existed { get; }
            public byte[] Bytes { get; }
        }

        internal sealed class SaveTransaction
        {
            private readonly string savePath;
            private readonly FileCheckpoint saveBefore;
            private readonly string contextPath;
            private readonly FileCheckpoint contextBefore;
            private bool active = true;

            internal SaveTransaction(
                string safetyPath,
                FileCheckpoint safetyBefore,
                string ownedContextPath,
                FileCheckpoint ownedContextBefore)
            {
                savePath = safetyPath;
                saveBefore = safetyBefore;
                contextPath = ownedContextPath;
                contextBefore = ownedContextBefore;
            }

            public void Commit()
            {
                active = false;
            }

            public bool TryRollback(out string error)
            {
                if (!active)
                {
                    error = string.Empty;
                    return true;
                }

                active = false;
                error = RestoreCheckpoints(
                    savePath,
                    saveBefore,
                    contextPath,
                    contextBefore).Trim();
                return error.Length == 0;
            }
        }

        public static bool TrySaveCurrentGame(out string error)
        {
            if (!TryBeginCurrentGameSave(
                    out SaveTransaction transaction,
                    out error))
            {
                return false;
            }

            CampaignStateService state = CampaignStateService.Instance;
            if (state == null ||
                !state.TryGetInventoryCarryover(
                    out string[] itemIds,
                    out int[] quantities) ||
                !TryWriteCampaignInventoryCheckpoint(
                    itemIds,
                    quantities,
                    out error))
            {
                transaction.TryRollback(out string rollbackError);
                error = string.IsNullOrWhiteSpace(error)
                    ? "The current campaign inventory checkpoint is unavailable."
                    : error;
                if (!string.IsNullOrWhiteSpace(rollbackError))
                    error += " " + rollbackError;
                return false;
            }

            transaction.Commit();
            return true;
        }

        public static bool TryBeginCurrentGameSave(
            out SaveTransaction transaction,
            out string error)
        {
            transaction = null;
            gameManager manager = gameManager.instance;
            GameObject player = manager != null ? manager.player : null;
            playerController controller = player != null
                ? player.GetComponent<playerController>()
                : null;
            Inventory inventory = player != null
                ? player.GetComponent<Inventory>()
                : null;
            if (manager == null || player == null || controller == null ||
                manager.itemDatabase == null ||
                inventory == null || manager.playerController != controller ||
                inventory.inventoryItems == null ||
                inventory.inventoryItems.Length <= 0 ||
                inventory.inventoryItems.Length >
                    CampaignInventoryCarryover.MaximumSupportedInventoryCapacity)
            {
                error =
                    "The active Safety gameManager, ItemDatabase, Player, controller, and bounded Inventory are not fully bound.";
                return false;
            }

            CampaignInventoryCarryover carryover =
                CampaignStateService.Instance
                    ?.GetComponent<CampaignInventoryCarryover>();
            CampaignLoadoutEquipmentBridge equipmentBridge =
                CampaignLoadoutEquipmentBridge.Instance;
            if (carryover == null || equipmentBridge == null)
            {
                error =
                    "The authored stable Safety inventory/gun catalog is unavailable.";
                return false;
            }

            if (!carryover.ValidateSafetyNativeInventoryDatabase(
                    manager.itemDatabase,
                    out error) ||
                !carryover.TryCaptureStableInventoryCheckpoint(
                    inventory,
                    out CampaignStableInventoryCheckpoint stableInventory,
                    out error) ||
                !equipmentBridge.TryCaptureStableGunCheckpoint(
                    controller,
                    out CampaignStableGunCheckpoint stableGuns,
                    out error))
            {
                return false;
            }

            string savePath = GetSafetySavePath();
            string contextPath = GetContextPath();
            if (!TryCaptureCheckpoint(savePath, out FileCheckpoint saveBefore,
                    out error) ||
                !TryCaptureCheckpoint(
                    contextPath,
                    out FileCheckpoint contextBefore,
                    out error))
            {
                return false;
            }

            try
            {
                //manager.Save();
                if (!TrySanitizeSafetyGunSave(out string sanitizeError))
                    throw new InvalidOperationException(sanitizeError);

                SafetySaveContext context = ReadContext();
                context.saveSceneName =
                    SceneManager.GetActiveScene().name;
                context.pendingArrivalSceneName = string.Empty;
                context.stableInventory = stableInventory;
                context.stableGuns = stableGuns;
                WriteContext(context);
                transaction = new SaveTransaction(
                    savePath,
                    saveBefore,
                    contextPath,
                    contextBefore);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                string rollbackError = RestoreCheckpoints(
                    savePath,
                    saveBefore,
                    contextPath,
                    contextBefore);
                error = exception.Message + rollbackError;
                return false;
            }
        }

        public static bool TryResetForNewGame(
            Func<bool> resetCampaign,
            out string error)
        {
            if (resetCampaign == null)
            {
                error = "The campaign reset callback is missing.";
                return false;
            }

            string savePath = GetSafetySavePath();
            string contextPath = GetContextPath();
            if (!TryCaptureCheckpoint(savePath, out FileCheckpoint saveBefore,
                    out error) ||
                !TryCaptureCheckpoint(
                    contextPath,
                    out FileCheckpoint contextBefore,
                    out error))
            {
                return false;
            }

            try
            {
                SaveSystem.SaveGame(new GameData());
                var newGameContext = new SafetySaveContext
                {
                    pendingArrivalSceneName =
                        CampaignSceneNames.FarmPrologueHub,
                    stableInventory =
                        CampaignStableInventoryCheckpoint.CreateEmpty(),
                    stableGuns = CampaignStableGunCheckpoint.CreateEmpty()
                };
                WriteContext(newGameContext);
                if (resetCampaign())
                {
                    error = string.Empty;
                    return true;
                }

                string rollbackError = RestoreCheckpoints(
                    savePath,
                    saveBefore,
                    contextPath,
                    contextBefore);
                error =
                    "The campaign save rejected its New Game reset." +
                    rollbackError;
                return false;
            }
            catch (Exception exception)
            {
                string rollbackError = RestoreCheckpoints(
                    savePath,
                    saveBefore,
                    contextPath,
                    contextBefore);
                error = exception.Message + rollbackError;
                return false;
            }
        }

        public static bool TryLoadGameData(
            out GameData loadedData,
            out string error)
        {
            try
            {
                loadedData = SaveSystem.LoadGame() ?? new GameData();
                loadedData._savInventory ??= Array.Empty<ItemSaveData>();
                loadedData._savgunInv ??= Array.Empty<WeaponSaveData>();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                loadedData = null;
                error = exception.Message;
                return false;
            }
        }

        public static bool TrySanitizeSafetyGunSave(out string error)
        {
            string savePath = GetSafetySavePath();
            FileCheckpoint checkpoint = default;
            try
            {
                if (!File.Exists(savePath))
                {
                    error = string.Empty;
                    return true;
                }

                if (!TryCaptureCheckpoint(
                        savePath,
                        out checkpoint,
                        out error))
                {
                    return false;
                }

                GameData data = SaveSystem.LoadGame() ?? new GameData();
                data._savgunInv = Array.Empty<WeaponSaveData>();
                data._savgunInvPos = 0;
                SaveSystem.SaveGame(data);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                var rollbackFailures = new List<string>();
                TryRestoreCheckpoint(
                    savePath,
                    checkpoint,
                    rollbackFailures);
                error = exception.Message +
                        (rollbackFailures.Count == 0
                            ? string.Empty
                            : " Rollback also failed: " +
                              string.Join(" ", rollbackFailures));
                return false;
            }
        }

        internal static bool TryReadStableInventoryCheckpoint(
            string activeSceneName,
            out CampaignStableInventoryCheckpoint checkpoint,
            out bool found,
            out string error)
        {
            checkpoint = null;
            found = false;
            try
            {
                SafetySaveContext context = ReadContext();
                if (!ContextAppliesToScene(context, activeSceneName))
                {
                    error = string.Empty;
                    return true;
                }

                if (!ValidateStableInventoryShape(
                        context.stableInventory,
                        out error))
                {
                    return false;
                }

                checkpoint = CloneStableInventory(context.stableInventory);
                found = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool TryReadStableGunCheckpoint(
            string activeSceneName,
            out CampaignStableGunCheckpoint checkpoint,
            out bool found,
            out string error)
        {
            checkpoint = null;
            found = false;
            try
            {
                SafetySaveContext context = ReadContext();
                if (!ContextAppliesToScene(context, activeSceneName))
                {
                    error = string.Empty;
                    return true;
                }

                if (!ValidateStableGunShape(context.stableGuns, out error))
                    return false;

                checkpoint = CloneStableGuns(context.stableGuns);
                found = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool ValidateStableInventoryShape(
            CampaignStableInventoryCheckpoint checkpoint,
            out string error)
        {
            if (checkpoint == null || !checkpoint.isAuthoritative ||
                checkpoint.itemIds == null || checkpoint.quantities == null ||
                checkpoint.itemIds.Length >
                    CampaignInventoryCarryover.MaximumSupportedInventoryCapacity ||
                checkpoint.quantities.Length != checkpoint.itemIds.Length ||
                float.IsNaN(checkpoint.inventoryWeight) ||
                float.IsInfinity(checkpoint.inventoryWeight) ||
                checkpoint.inventoryWeight < 0f)
            {
                error =
                    "Stable Safety inventory checkpoint has an invalid shape.";
                return false;
            }

            if (checkpoint.itemIds.Length == 0 &&
                checkpoint.inventoryWeight > 0.001f)
            {
                error =
                    "An empty stable Safety inventory checkpoint cannot carry weight.";
                return false;
            }

            for (int slot = 0; slot < checkpoint.itemIds.Length; slot++)
            {
                bool emptyId = string.IsNullOrWhiteSpace(
                    checkpoint.itemIds[slot]);
                int quantity = checkpoint.quantities[slot];
                if ((emptyId && quantity != 0) ||
                    (!emptyId && quantity <= 0))
                {
                    error =
                        $"Stable Safety inventory slot {slot} is inconsistent.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        internal static bool ValidateStableGunShape(
            CampaignStableGunCheckpoint checkpoint,
            out string error)
        {
            if (checkpoint == null || !checkpoint.isAuthoritative ||
                checkpoint.gunIds == null || checkpoint.ammo == null ||
                checkpoint.gunIds.Length != checkpoint.ammo.Length)
            {
                error = "Stable Safety gun checkpoint has an invalid shape.";
                return false;
            }

            if ((checkpoint.gunIds.Length == 0 &&
                 checkpoint.selectedIndex != -1) ||
                (checkpoint.gunIds.Length > 0 &&
                 (checkpoint.selectedIndex < 0 ||
                  checkpoint.selectedIndex >= checkpoint.gunIds.Length)))
            {
                error = "Stable Safety gun selection is invalid.";
                return false;
            }

            for (int index = 0; index < checkpoint.gunIds.Length; index++)
            {
                string gunId = checkpoint.gunIds[index]?.Trim() ??
                               string.Empty;
                if ((gunId != CampaignLoadoutEquipmentBridge.PistolStableId &&
                     gunId != CampaignLoadoutEquipmentBridge.RifleStableId &&
                     gunId != CampaignLoadoutEquipmentBridge.ShotgunStableId) ||
                    checkpoint.ammo[index] < 0)
                {
                    error =
                        $"Stable Safety gun slot {index} is unsupported or invalid.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool ContextAppliesToScene(
            SafetySaveContext context,
            string activeSceneName)
        {
            string active = activeSceneName?.Trim() ?? string.Empty;
            return active.Length > 0 &&
                   (string.Equals(
                        context.saveSceneName,
                        active,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        context.pendingArrivalSceneName,
                        active,
                        StringComparison.Ordinal));
        }

        private static CampaignStableInventoryCheckpoint
            CloneStableInventory(CampaignStableInventoryCheckpoint source)
        {
            return new CampaignStableInventoryCheckpoint
            {
                isAuthoritative = source.isAuthoritative,
                itemIds = (string[])source.itemIds.Clone(),
                quantities = (int[])source.quantities.Clone(),
                inventoryWeight = source.inventoryWeight
            };
        }

        private static CampaignStableGunCheckpoint CloneStableGuns(
            CampaignStableGunCheckpoint source)
        {
            return new CampaignStableGunCheckpoint
            {
                isAuthoritative = source.isAuthoritative,
                gunIds = (string[])source.gunIds.Clone(),
                ammo = (int[])source.ammo.Clone(),
                selectedIndex = source.selectedIndex
            };
        }

        public static bool TryMarkPendingArrival(
            string destinationSceneName,
            out string error)
        {
            string destination = destinationSceneName?.Trim() ?? string.Empty;
            if (destination.Length == 0)
            {
                error = "The campaign arrival scene is empty.";
                return false;
            }

            string contextPath = GetContextPath();
            if (!TryCaptureCheckpoint(
                    contextPath,
                    out FileCheckpoint contextBefore,
                    out error))
            {
                return false;
            }

            try
            {
                SafetySaveContext context = ReadContext();
                context.pendingArrivalSceneName = destination;
                WriteContext(context);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                var failures = new List<string>();
                TryRestoreCheckpoint(
                    contextPath,
                    contextBefore,
                    failures);
                error = exception.Message +
                        (failures.Count == 0
                            ? string.Empty
                            : " Rollback also failed: " +
                              string.Join(" ", failures));
                return false;
            }
        }

        public static bool TryWriteCampaignInventoryCheckpoint(
            string[] itemIds,
            int[] quantities,
            out string error)
        {
            if (itemIds == null || quantities == null ||
                itemIds.Length != quantities.Length)
            {
                error = "Campaign inventory checkpoint arrays do not match.";
                return false;
            }

            try
            {
                CampaignStateService state = CampaignStateService.Instance;
                if (state == null)
                {
                    error = "Campaign state is unavailable.";
                    return false;
                }

                if (!state.TryGetInventoryCarryover(
                        out string[] normalizedIds,
                        out int[] normalizedQuantities) ||
                    !ArraysMatch(
                        itemIds,
                        quantities,
                        normalizedIds,
                        normalizedQuantities))
                {
                    error =
                        "Campaign inventory checkpoint does not match the normalized durable campaign snapshot.";
                    return false;
                }

                if (!state.TryExportCheckpoint(
                        out string campaignCheckpoint,
                        out error))
                {
                    return false;
                }

                SafetySaveContext context = ReadContext();
                if (!StableInventoryMatchesCampaignSnapshot(
                        context.stableInventory,
                        itemIds,
                        quantities,
                        out error))
                {
                    return false;
                }

                context.campaignInventoryItemIds =
                    (string[])itemIds.Clone();
                context.campaignInventoryQuantities =
                    (int[])quantities.Clone();
                context.campaignCheckpointJson = campaignCheckpoint;
                WriteContext(context);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryReadCampaignCheckpoint(
            string activeSceneName,
            out string checkpointJson,
            out string error)
        {
            checkpointJson = string.Empty;
            error = string.Empty;
            try
            {
                SafetySaveContext context = ReadContext();
                if (!string.Equals(
                        context.saveSceneName,
                        activeSceneName?.Trim(),
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(
                        context.campaignCheckpointJson))
                {
                    error =
                        "No full campaign checkpoint matches this Safety save scene.";
                    return false;
                }

                CampaignStateService state = CampaignStateService.Instance;
                string stablePairError = string.Empty;
                if (state == null || !state.TryReadCheckpointInventory(
                        context.campaignCheckpointJson,
                        out string[] checkpointIds,
                        out int[] checkpointQuantities,
                        out error) ||
                    !ArraysMatch(
                        context.campaignInventoryItemIds,
                        context.campaignInventoryQuantities,
                        checkpointIds,
                        checkpointQuantities) ||
                    !StableInventoryMatchesCampaignSnapshot(
                        context.stableInventory,
                        context.campaignInventoryItemIds,
                        context.campaignInventoryQuantities,
                        out stablePairError))
                {
                    error = string.IsNullOrWhiteSpace(error)
                        ? string.IsNullOrWhiteSpace(stablePairError)
                            ? "Campaign checkpoint inventory does not match its paired Safety context."
                            : stablePairError
                        : error;
                    return false;
                }

                checkpointJson = context.campaignCheckpointJson;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryGetContinueScene(out string sceneName)
        {
            sceneName = string.Empty;
            try
            {
                SafetySaveContext context = ReadContext();
                string candidate = context.saveSceneName?.Trim() ??
                                   string.Empty;
                if ((candidate != CampaignSceneNames.FarmPrologueHub &&
                     candidate != CampaignSceneNames.OpenWorld) ||
                    !string.IsNullOrWhiteSpace(
                        context.pendingArrivalSceneName))
                {
                    return false;
                }

                CampaignStateService state = CampaignStateService.Instance;
                if (state == null ||
                    !ValidateStableInventoryShape(
                        context.stableInventory,
                        out _) ||
                    !ValidateStableGunShape(context.stableGuns, out _) ||
                    !TryReadCampaignCheckpoint(
                        candidate,
                        out string checkpointJson,
                        out _) ||
                    !state.TryValidateCheckpoint(
                        checkpointJson,
                        out _))
                {
                    return false;
                }

                CampaignProgressSnapshot snapshot = state.Current;
                if (candidate == CampaignSceneNames.OpenWorld &&
                    (!snapshot.PrologueCompleted ||
                     !snapshot.BlackPinesUnlocked))
                {
                    return false;
                }

                sceneName = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryValidatePendingArrivalContext(
            string destinationSceneName)
        {
            try
            {
                string destination = destinationSceneName?.Trim() ??
                                     string.Empty;
                SafetySaveContext context = ReadContext();
                if ((destination != CampaignSceneNames.FarmPrologueHub &&
                     destination != CampaignSceneNames.OpenWorld) ||
                    !string.Equals(
                        context.pendingArrivalSceneName,
                        destination,
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(context.saveSceneName) ||
                    !ValidateStableInventoryShape(
                        context.stableInventory,
                        out _) ||
                    !ValidateStableGunShape(context.stableGuns, out _))
                {
                    return false;
                }

                CampaignStateService state = CampaignStateService.Instance;
                if (state == null ||
                    !state.TryReadCheckpointInventory(
                        context.campaignCheckpointJson,
                        out string[] checkpointIds,
                        out int[] checkpointQuantities,
                        out _) ||
                    !ArraysMatch(
                        context.campaignInventoryItemIds,
                        context.campaignInventoryQuantities,
                        checkpointIds,
                        checkpointQuantities) ||
                    !StableInventoryMatchesCampaignSnapshot(
                        context.stableInventory,
                        context.campaignInventoryItemIds,
                        context.campaignInventoryQuantities,
                        out _) ||
                    !state.TryValidateCheckpoint(
                        context.campaignCheckpointJson,
                        out _))
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryReadCampaignInventoryCheckpoint(
            string activeSceneName,
            out string[] itemIds,
            out int[] quantities)
        {
            itemIds = Array.Empty<string>();
            quantities = Array.Empty<int>();
            try
            {
                SafetySaveContext context = ReadContext();
                if (!string.Equals(
                        context.saveSceneName,
                        activeSceneName?.Trim(),
                        StringComparison.Ordinal) ||
                    context.campaignInventoryItemIds == null ||
                    context.campaignInventoryQuantities == null ||
                    context.campaignInventoryItemIds.Length !=
                        context.campaignInventoryQuantities.Length)
                {
                    return false;
                }

                itemIds =
                    (string[])context.campaignInventoryItemIds.Clone();
                quantities =
                    (int[])context.campaignInventoryQuantities.Clone();
                return true;
            }
            catch (Exception exception)
            {

                return false;
            }
        }

        private static bool ArraysMatch(
            string[] firstIds,
            int[] firstQuantities,
            string[] secondIds,
            int[] secondQuantities)
        {
            if (firstIds == null || firstQuantities == null ||
                secondIds == null || secondQuantities == null ||
                firstIds.Length == 0 ||
                firstIds.Length != firstQuantities.Length ||
                firstIds.Length != secondIds.Length ||
                secondIds.Length != secondQuantities.Length)
            {
                return false;
            }

            for (int index = 0; index < firstIds.Length; index++)
            {
                if (!string.Equals(
                        firstIds[index]?.Trim(),
                        secondIds[index]?.Trim(),
                        StringComparison.Ordinal) ||
                    firstQuantities[index] != secondQuantities[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StableInventoryMatchesCampaignSnapshot(
            CampaignStableInventoryCheckpoint stableInventory,
            string[] campaignIds,
            int[] campaignQuantities,
            out string error)
        {
            if (!ValidateStableInventoryShape(stableInventory, out error) ||
                campaignIds == null || campaignQuantities == null ||
                campaignIds.Length == 0 ||
                campaignIds.Length != campaignQuantities.Length)
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error =
                        "The campaign inventory pair has an invalid shape.";
                }

                return false;
            }

            var expected = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (int index = 0; index < campaignIds.Length; index++)
            {
                string id = campaignIds[index]?.Trim() ?? string.Empty;
                int quantity = campaignQuantities[index];
                if (id.Length == 0 || quantity < 0 ||
                    expected.ContainsKey(id))
                {
                    error =
                        "The campaign inventory pair contains blank, duplicate, or negative data.";
                    return false;
                }

                expected.Add(id, quantity);
            }

            var actual = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (string id in expected.Keys)
                actual.Add(id, 0);

            for (int slot = 0; slot < stableInventory.itemIds.Length; slot++)
            {
                string id = stableInventory.itemIds[slot]?.Trim() ??
                            string.Empty;
                if (id.Length == 0 || !actual.TryGetValue(id, out int total))
                    continue;

                long combined =
                    (long)total + stableInventory.quantities[slot];
                if (combined > int.MaxValue)
                {
                    error =
                        $"Stable Safety inventory quantity for '{id}' overflowed.";
                    return false;
                }

                actual[id] = (int)combined;
            }

            foreach (KeyValuePair<string, int> pair in expected)
            {
                if (actual[pair.Key] != pair.Value)
                {
                    error =
                        $"Stable Safety inventory quantity for '{pair.Key}' does not match its paired campaign checkpoint.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static void CancelPendingArrival(string destinationSceneName)
        {
            TryCompletePendingArrival(destinationSceneName);
        }

        public static void CompletePendingArrival(string destinationSceneName)
        {
            TryCompletePendingArrival(destinationSceneName);
        }

        public static bool ShouldApplyLoadedPosition(
            string activeSceneName,
            CampaignStateService campaignState)
        {
            try
            {
                SafetySaveContext context = ReadContext();
                string active = activeSceneName?.Trim() ?? string.Empty;
                bool campaignArrivalPending = campaignState != null &&
                    !string.IsNullOrWhiteSpace(
                        campaignState.Current.PendingSceneName);
                if (active.Length == 0 || campaignArrivalPending ||
                    !string.IsNullOrWhiteSpace(
                        context.pendingArrivalSceneName) ||
                    !string.Equals(
                        context.saveSceneName,
                        active,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                return campaignState != null &&
                       TryReadCampaignCheckpoint(
                           active,
                           out string checkpointJson,
                           out _) &&
                       campaignState.TryValidateCheckpoint(
                           checkpointJson,
                           out _);
            }
            catch (Exception exception)
            {

                return false;
            }
        }

        private static bool TryCompletePendingArrival(string sceneName)
        {
            try
            {
                SafetySaveContext context = ReadContext();
                if (!string.Equals(
                        context.pendingArrivalSceneName,
                        sceneName?.Trim(),
                        StringComparison.Ordinal))
                {
                    return true;
                }

                context.pendingArrivalSceneName = string.Empty;
                WriteContext(context);
                return true;
            }
            catch (Exception exception)
            {

                return false;
            }
        }

        private static SafetySaveContext ReadContext()
        {
            string path = GetContextPath();
            if (!File.Exists(path))
                return new SafetySaveContext();

            string json = File.ReadAllText(path);
            SafetySaveContext context =
                JsonUtility.FromJson<SafetySaveContext>(json);
            if (context == null || context.version != ContextVersion)
                return new SafetySaveContext();

            context.saveSceneName =
                context.saveSceneName?.Trim() ?? string.Empty;
            context.pendingArrivalSceneName =
                context.pendingArrivalSceneName?.Trim() ?? string.Empty;
            context.campaignInventoryItemIds ??= Array.Empty<string>();
            context.campaignInventoryQuantities ??= Array.Empty<int>();
            context.campaignCheckpointJson ??= string.Empty;
            context.stableInventory ??=
                CampaignStableInventoryCheckpoint.CreateEmpty();
            context.stableInventory.itemIds ??= Array.Empty<string>();
            context.stableInventory.quantities ??= Array.Empty<int>();
            context.stableGuns ??= CampaignStableGunCheckpoint.CreateEmpty();
            context.stableGuns.gunIds ??= Array.Empty<string>();
            context.stableGuns.ammo ??= Array.Empty<int>();
            return context;
        }

        private static void WriteContext(SafetySaveContext context)
        {
            File.WriteAllText(
                GetContextPath(),
                JsonUtility.ToJson(context ?? new SafetySaveContext(), true));
        }

        private static bool TryCaptureCheckpoint(
            string path,
            out FileCheckpoint checkpoint,
            out string error)
        {
            try
            {
                bool existed = File.Exists(path);
                checkpoint = new FileCheckpoint(
                    existed,
                    existed ? File.ReadAllBytes(path) : Array.Empty<byte>());
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                checkpoint = default;
                error = exception.Message;
                return false;
            }
        }

        private static string RestoreCheckpoints(
            string firstPath,
            FileCheckpoint first,
            string secondPath,
            FileCheckpoint second)
        {
            var failures = new List<string>();
            TryRestoreCheckpoint(firstPath, first, failures);
            TryRestoreCheckpoint(secondPath, second, failures);
            return failures.Count == 0
                ? string.Empty
                : " Rollback also failed: " + string.Join(" ", failures);
        }

        private static void TryRestoreCheckpoint(
            string path,
            FileCheckpoint checkpoint,
            ICollection<string> failures)
        {
            try
            {
                if (checkpoint.Existed)
                    File.WriteAllBytes(path, checkpoint.Bytes);
                else if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                failures.Add(exception.Message);
            }
        }

        private static string GetSafetySavePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                SafetySaveFileName);
        }

        private static string GetContextPath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                ContextFileName);
        }
    }
}
