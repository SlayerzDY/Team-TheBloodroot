using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Campaign
{
    [Serializable]
    public sealed class CampaignRootOfferingStringEvent : UnityEvent<string>
    {
    }

    /// <summary>
    /// Campaign-owned Root Tree interaction. It offers the prologue cursed
    /// object first, then Esther, Ruth, Naomi, and Nell in story order. The
    /// protected Tree scripts are presentation assets only; all identity,
    /// inventory recovery, and emergence obligations are durable campaign
    /// state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CampaignRootTreeOffering :
        MonoBehaviour,
        global::IInteract
    {
        private const string GenericCursedItemName = "CursedItem";

        private static readonly string[] OrderedOfferingIds =
        {
            CampaignRootOfferingIds.PrologueCursedObject,
            CampaignNameStoneIds.Esther,
            CampaignNameStoneIds.Ruth,
            CampaignNameStoneIds.Naomi,
            CampaignNameStoneIds.Nell
        };

        [Header("Campaign Authority")]
        [SerializeField] private CampaignStateService stateService;
        [SerializeField] private CampaignInventoryCarryover inventoryCarryover;

        [Header("Authored Generic Cursed Item")]
        [SerializeField] private GameObject cursedItemPickupObject;
        [SerializeField] private global::Inventory playerInventory;
        [SerializeField] private bool resolveInventoryFromGameManager = true;
        [SerializeField] private Collider interactionCollider;

        [Header("Authored Offered-Object Presentation")]
        [Tooltip("Order: prologue object, Esther, Ruth, Naomi, Nell.")]
        [SerializeField] private GameObject[] offeredObjectVisuals =
            new GameObject[5];

        [Header("Reload Reconciliation")]
        [SerializeField, Min(0f)] private float initialReconcileDelay = 0.75f;
        [SerializeField, Min(1)] private int reconcileAttempts = 5;
        [SerializeField, Min(0.05f)] private float reconcileRetryDelay = 1f;

        [Header("Authored Events")]
        [SerializeField] private CampaignRootOfferingStringEvent offerAccepted =
            new CampaignRootOfferingStringEvent();
        [SerializeField] private CampaignRootOfferingStringEvent offerRejected =
            new CampaignRootOfferingStringEvent();
        [SerializeField] private CampaignRootOfferingStringEvent
            recoveryStillPending = new CampaignRootOfferingStringEvent();

        private CampaignStateService subscribedState;
        private Coroutine reconcileRoutine;
        private bool transactionInProgress;
        private string lastFailureReason = string.Empty;

        public bool IsTransactionInProgress => transactionInProgress;
        public string LastFailureReason => lastFailureReason;
        public int OfferedCount => CountCommittedOfferings(ResolveStateService());
        public bool HasExclusiveInteractionAuthority =>
            !HasCompetingInteractable();
        public CampaignRootOfferingStringEvent OfferAcceptedEvent =>
            offerAccepted;
        public CampaignRootOfferingStringEvent OfferRejectedEvent =>
            offerRejected;
        public GameObject CursedItemPickupObject => cursedItemPickupObject;
        public Collider InteractionCollider => interactionCollider;
        public IReadOnlyList<GameObject> OfferedObjectVisuals =>
            offeredObjectVisuals;

        private void Awake()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            BindStateService();
            RefreshPresentation();

            if (Application.isPlaying)
                StartReconcileRoutine();
        }

        private void OnDisable()
        {
            if (reconcileRoutine != null)
            {
                StopCoroutine(reconcileRoutine);
                reconcileRoutine = null;
            }

            UnbindStateService();
        }

        private void OnValidate()
        {
            initialReconcileDelay = Mathf.Max(0f, initialReconcileDelay);
            reconcileAttempts = Mathf.Max(1, reconcileAttempts);
            reconcileRetryDelay = Mathf.Max(0.05f, reconcileRetryDelay);
            offeredObjectVisuals ??= new GameObject[5];
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }

        public void Configure(
            CampaignStateService campaignState,
            CampaignInventoryCarryover carryover,
            GameObject authoredCursedItemPickup,
            global::Inventory inventory,
            GameObject[] offeringVisuals)
        {
            UnbindStateService();
            stateService = campaignState;
            inventoryCarryover = carryover;
            cursedItemPickupObject = authoredCursedItemPickup;
            playerInventory = inventory;
            offeredObjectVisuals = offeringVisuals ?? new GameObject[5];
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
            BindStateService();
            RefreshPresentation();
        }

        /// <summary>
        /// Migration-only overload for the previous V6 authorer. The safety
        /// Tree component is deliberately ignored; this owned adapter no
        /// longer reads or mutates protected Tree state.
        /// </summary>
        public void Configure(
            CampaignStateService campaignState,
            CampaignInventoryCarryover carryover,
            GameObject authoredCursedItemPickup,
            global::Inventory inventory,
            global::TreeRootInteraction protectedTreePresentation,
            GameObject[] nameStoneVisuals)
        {
            var migratedVisuals = new GameObject[5];
            if (nameStoneVisuals != null)
            {
                int count = Mathf.Min(4, nameStoneVisuals.Length);
                Array.Copy(nameStoneVisuals, 0, migratedVisuals, 1, count);
            }

            Configure(
                campaignState,
                carryover,
                authoredCursedItemPickup,
                inventory,
                migratedVisuals);
        }

        public void SendInteract(Collider target)
        {
            TryOfferNextCursedObject();
        }

        /// <summary>
        /// Compatibility entry point retained for existing scene events and
        /// tests. The prologue object is now part of the same ordered flow.
        /// </summary>
        public bool TryOfferNextNameStone()
        {
            return TryOfferNextCursedObject();
        }

        public bool TryOfferNextCursedObject()
        {
            if (transactionInProgress)
                return Reject("A cursed-object offering is already being saved.");

            if (!HasExclusiveInteractionAuthority)
            {
                return Reject(
                    "The campaign Root Tree proxy has another IInteract component attached.");
            }

            CampaignStateService state = ResolveStateService();
            if (state == null)
                return Reject("Campaign progression is unavailable.");

            BindStateService();
            CampaignInventoryCarryover carryover =
                ResolveInventoryCarryover();
            if (!IsCarryoverReady(carryover, out string carryoverError))
                return Reject(carryoverError);

            if (!string.IsNullOrEmpty(state.PendingRootOfferingId))
                return TryReconcilePendingOffer();

            if (state.HasUnresolvedFarmEmergence)
            {
                return Reject(
                    "Clear the current Farm emergence before offering another cursed object.");
            }

            global::Inventory inventory = ResolveInventory();
            global::ItemStats item = ResolveCursedItem();
            if (!ValidateRuntimeDependencies(
                    state,
                    inventory,
                    item,
                    carryover,
                    out string dependencyError))
            {
                return Reject(dependencyError);
            }

            if (!carryover.TryReconcileExtractedNameStoneTokens(
                    cursedItemPickupObject,
                    inventory))
            {
                return Reject(
                    "Extracted cursed-object inventory could not be reconciled safely.");
            }

            string offeringId = FindNextOfferableOfferingId(state);
            if (string.IsNullOrEmpty(offeringId))
            {
                return Reject(AllOfferingsCommitted(state)
                    ? "Every cursed object has already been offered."
                    : "No recovered cursed object is ready to offer.");
            }

            int quantityBefore = GetItemQuantity(inventory, item);
            if (quantityBefore < 1)
            {
                return Reject(
                    "The recovered cursed object is not in the player inventory.");
            }

            transactionInProgress = true;
            try
            {
                if (!state.TryBeginRootOffering(offeringId))
                {
                    return Reject(
                        $"Could not begin the durable offering for '{offeringId}'.");
                }

                if (!TryRemoveExactlyOne(
                        inventory,
                        item,
                        quantityBefore))
                {
                    state.TryCancelPendingRootOffering(offeringId);
                    return Reject(
                        $"Inventory did not remove exactly one '{item.itemName}'.");
                }

                if (!carryover.CaptureForTravel())
                {
                    TryRollbackAndCancel(
                        state,
                        carryover,
                        inventory,
                        item,
                        offeringId,
                        quantityBefore);
                    return Reject(
                        "The cursed object was not offered because the inventory snapshot could not be saved.");
                }

                if (!state.TryCommitPendingRootOffering(offeringId))
                {
                    TryRollbackAndCancel(
                        state,
                        carryover,
                        inventory,
                        item,
                        offeringId,
                        quantityBefore);
                    return Reject(
                        $"The offering for '{offeringId}' remains pending because campaign progress could not be saved.");
                }

                lastFailureReason = string.Empty;
                RefreshPresentation();
                CampaignEventUtility.Invoke(
                    offerAccepted,
                    offeringId,
                    this);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return Reject(
                    "An unexpected error interrupted the cursed-object offering; durable recovery will retry it.");
            }
            finally
            {
                transactionInProgress = false;
            }
        }

        public bool TryReconcilePendingOffer()
        {
            if (transactionInProgress)
                return false;

            CampaignStateService state = ResolveStateService();
            string offeringId = state?.PendingRootOfferingId ?? string.Empty;
            if (state == null || string.IsNullOrEmpty(offeringId))
            {
                RefreshPresentation();
                return state != null;
            }

            if (!IsKnownOfferingId(offeringId))
            {
                return Reject(
                    $"The pending Root offering ID '{offeringId}' is not recognized.");
            }

            global::Inventory inventory = ResolveInventory();
            global::ItemStats item = ResolveCursedItem();
            CampaignInventoryCarryover carryover = ResolveInventoryCarryover();
            if (!IsCarryoverReady(carryover, out string carryoverError))
                return Reject(carryoverError);

            if (!ValidateRuntimeDependencies(
                    state,
                    inventory,
                    item,
                    carryover,
                    out string dependencyError))
            {
                return Reject(dependencyError);
            }

            transactionInProgress = true;
            bool removedDuringRecovery = false;
            int quantityBefore = GetItemQuantity(inventory, item);
            try
            {
                if (IsOfferingCommitted(state, offeringId))
                {
                    bool canceled =
                        state.TryCancelPendingRootOffering(offeringId);
                    RefreshPresentation();
                    return canceled ||
                           string.IsNullOrEmpty(state.PendingRootOfferingId);
                }

                if (!IsOfferingAvailable(state, offeringId))
                {
                    state.TryCancelPendingRootOffering(offeringId);
                    return Reject(
                        $"Pending cursed object '{offeringId}' is not available.");
                }

                int expectedQuantityAfterPendingConsumption =
                    CountExpectedTokensAfterPendingConsumption(
                        state,
                        offeringId);
                if (!TryPlanPendingRecoveryRemoval(
                        quantityBefore,
                        expectedQuantityAfterPendingConsumption,
                        out int removalCount))
                {
                    if (quantityBefore >
                        expectedQuantityAfterPendingConsumption + 1)
                    {
                        return Reject(
                            $"Pending cursed object '{offeringId}' found an unexplained inventory surplus ({quantityBefore}; expected {expectedQuantityAfterPendingConsumption} or {expectedQuantityAfterPendingConsumption + 1}). Recovery will not consume or commit it.");
                    }

                    return Reject(
                        $"Pending cursed object '{offeringId}' is waiting for inventory recovery.");
                }

                if (removalCount == 1)
                {
                    removedDuringRecovery = TryRemoveExactlyOne(
                        inventory,
                        item,
                        quantityBefore);
                    if (!removedDuringRecovery)
                    {
                        return Reject(
                            "Pending offering recovery could not consume exactly one cursed object.");
                    }
                }

                if (!carryover.CaptureForTravel())
                {
                    if (removedDuringRecovery)
                    {
                        TryRollbackAndCancel(
                            state,
                            carryover,
                            inventory,
                            item,
                            offeringId,
                            quantityBefore);
                    }

                    return Reject(
                        $"Pending cursed object '{offeringId}' is waiting for inventory persistence.");
                }

                if (!state.TryCommitPendingRootOffering(offeringId))
                {
                    if (removedDuringRecovery)
                    {
                        TryRollbackAndCancel(
                            state,
                            carryover,
                            inventory,
                            item,
                            offeringId,
                            quantityBefore);
                    }

                    return Reject(
                        $"Pending cursed object '{offeringId}' is waiting for campaign persistence.");
                }

                lastFailureReason = string.Empty;
                RefreshPresentation();
                CampaignEventUtility.Invoke(
                    offerAccepted,
                    offeringId,
                    this);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return Reject(
                    $"Pending cursed object '{offeringId}' could not be reconciled.");
            }
            finally
            {
                transactionInProgress = false;
            }
        }

        public void RefreshPresentation()
        {
            CampaignStateService state = ResolveStateService();
            for (int index = 0; index < OrderedOfferingIds.Length; index++)
            {
                GameObject visual = offeredObjectVisuals != null &&
                                    index < offeredObjectVisuals.Length
                    ? offeredObjectVisuals[index]
                    : null;
                if (visual == null)
                    continue;

                bool offered = state != null &&
                               IsOfferingCommitted(
                                   state,
                                   OrderedOfferingIds[index]);
                if (visual.activeSelf != offered)
                    visual.SetActive(offered);
            }

            bool hasPending = state != null &&
                              !string.IsNullOrEmpty(
                                  state.PendingRootOfferingId);
            bool hasAvailable = state != null &&
                                !string.IsNullOrEmpty(
                                    FindNextOfferableOfferingId(state));
            CampaignInventoryCarryover carryover =
                ResolveInventoryCarryover();
            bool restoreInProgress = carryover != null &&
                                     carryover.IsRestoreInProgress;
            bool restoreFailedPending = carryover != null &&
                                        carryover.HasPendingRestoreFailure;
            bool interactionAvailable = state != null &&
                                        !state.HasUnresolvedFarmEmergence &&
                                        !restoreInProgress &&
                                        !restoreFailedPending &&
                                        (hasPending || hasAvailable);
            if (interactionCollider != null)
                interactionCollider.enabled = interactionAvailable;
        }

        private IEnumerator ReconcileAfterSceneReady()
        {
            yield return new WaitForSecondsRealtime(initialReconcileDelay);

            CampaignInventoryCarryover carryover =
                ResolveInventoryCarryover();
            while (isActiveAndEnabled && carryover != null &&
                   carryover.IsRestoreInProgress)
            {
                yield return null;
                carryover = ResolveInventoryCarryover();
            }

            if (!isActiveAndEnabled)
            {
                reconcileRoutine = null;
                yield break;
            }

            if (!IsCarryoverReady(carryover, out string carryoverError))
            {
                Reject(carryoverError);
                string blockedPending =
                    ResolveStateService()?.PendingRootOfferingId ??
                    string.Empty;
                if (!string.IsNullOrEmpty(blockedPending))
                {
                    CampaignEventUtility.Invoke(
                        recoveryStillPending,
                        blockedPending,
                        this);
                }

                reconcileRoutine = null;
                yield break;
            }

            for (int attempt = 0;
                 attempt < reconcileAttempts && isActiveAndEnabled;
                 attempt++)
            {
                CampaignStateService state = ResolveStateService();
                if (state != null &&
                    string.IsNullOrEmpty(state.PendingRootOfferingId))
                {
                    RefreshPresentation();
                    reconcileRoutine = null;
                    yield break;
                }

                TryReconcilePendingOffer();
                state = ResolveStateService();
                if (state != null &&
                    string.IsNullOrEmpty(state.PendingRootOfferingId))
                {
                    reconcileRoutine = null;
                    yield break;
                }

                if (attempt + 1 < reconcileAttempts)
                {
                    yield return new WaitForSecondsRealtime(
                        reconcileRetryDelay);
                }
            }

            CampaignStateService unresolved = ResolveStateService();
            string pending = unresolved?.PendingRootOfferingId ?? string.Empty;
            if (!string.IsNullOrEmpty(pending))
            {
                CampaignEventUtility.Invoke(
                    recoveryStillPending,
                    pending,
                    this);
            }

            reconcileRoutine = null;
        }

        private void StartReconcileRoutine()
        {
            if (reconcileRoutine == null && isActiveAndEnabled)
                reconcileRoutine = StartCoroutine(ReconcileAfterSceneReady());
        }

        private void HandleProgressChanged(CampaignProgressSnapshot snapshot)
        {
            RefreshPresentation();
            if (Application.isPlaying &&
                !string.IsNullOrEmpty(snapshot.PendingRootOfferingId))
            {
                StartReconcileRoutine();
            }
        }

        private void BindStateService()
        {
            CampaignStateService state = ResolveStateService();
            if (subscribedState == state)
                return;

            UnbindStateService();
            subscribedState = state;
            if (subscribedState != null)
                subscribedState.ProgressChanged += HandleProgressChanged;
        }

        private void UnbindStateService()
        {
            if (subscribedState != null)
            {
                subscribedState.ProgressChanged -= HandleProgressChanged;
                subscribedState = null;
            }
        }

        private CampaignStateService ResolveStateService()
        {
            CampaignStateService persistentState =
                CampaignStateService.Instance;
            if (persistentState != null && stateService != persistentState)
            {
                stateService = persistentState;
            }
            else if (stateService == null)
            {
                stateService = persistentState;
            }

            return stateService;
        }

        private CampaignInventoryCarryover ResolveInventoryCarryover()
        {
            CampaignStateService authority = ResolveStateService();
            CampaignInventoryCarryover persistentCarryover =
                authority != null
                    ? authority.GetComponent<CampaignInventoryCarryover>()
                    : null;
            if (persistentCarryover != null &&
                inventoryCarryover != persistentCarryover)
            {
                inventoryCarryover = persistentCarryover;
            }
            else if (inventoryCarryover == null)
            {
                inventoryCarryover = persistentCarryover;
            }

            return inventoryCarryover;
        }

        private global::Inventory ResolveInventory()
        {
            if (playerInventory != null)
                return playerInventory;

            if (!resolveInventoryFromGameManager ||
                global::gameManager.instance == null ||
                global::gameManager.instance.player == null)
            {
                return null;
            }

            playerInventory = global::gameManager.instance.player
                .GetComponent<global::Inventory>();
            return playerInventory;
        }

        private global::ItemStats ResolveCursedItem()
        {
            return CampaignInventoryTokenUtility.GetItemStats(
                cursedItemPickupObject);
        }

        private static bool ValidateRuntimeDependencies(
            CampaignStateService state,
            global::Inventory inventory,
            global::ItemStats item,
            CampaignInventoryCarryover carryover,
            out string error)
        {
            if (state == null || inventory == null || carryover == null ||
                item == null ||
                !string.Equals(
                    item.itemName?.Trim(),
                    GenericCursedItemName,
                    StringComparison.Ordinal) ||
                item.quantity != 1 || item.stackSize != 1)
            {
                error =
                    "Root Tree offering requires campaign state, carryover, player inventory, and exactly one non-stacking CursedItem template.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsCarryoverReady(
            CampaignInventoryCarryover carryover,
            out string error)
        {
            if (carryover == null)
            {
                error = "Campaign inventory carryover is unavailable.";
                return false;
            }

            if (carryover.IsRestoreInProgress)
            {
                error =
                    "Inventory recovery must finish before the Root Tree can accept a cursed object.";
                return false;
            }

            if (carryover.HasPendingRestoreFailure)
            {
                error =
                    "Inventory recovery failed and must be retried before the Root Tree can accept a cursed object.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string FindNextOfferableOfferingId(
            CampaignStateService state)
        {
            if (state == null)
                return string.Empty;

            foreach (string offeringId in OrderedOfferingIds)
            {
                if (IsOfferingAvailable(state, offeringId) &&
                    !IsOfferingCommitted(state, offeringId))
                {
                    return offeringId;
                }
            }

            return string.Empty;
        }

        private static int CountCommittedOfferings(
            CampaignStateService state)
        {
            if (state == null)
                return 0;

            int count = 0;
            foreach (string offeringId in OrderedOfferingIds)
            {
                if (IsOfferingCommitted(state, offeringId))
                    count++;
            }

            return count;
        }

        private static bool AllOfferingsCommitted(
            CampaignStateService state)
        {
            return CountCommittedOfferings(state) == OrderedOfferingIds.Length;
        }

        private static int CountExpectedTokensAfterPendingConsumption(
            CampaignStateService state,
            string pendingOfferingId)
        {
            if (state == null || !IsKnownOfferingId(pendingOfferingId))
                return 0;

            int expected = 0;
            if (!string.Equals(
                    pendingOfferingId,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal) &&
                state.PrologueCursedObjectRevealed &&
                !state.PrologueCursedObjectOffered)
            {
                expected++;
            }

            foreach (string stoneId in CampaignNameStoneIds.All)
            {
                if (!string.Equals(
                        stoneId,
                        pendingOfferingId,
                        StringComparison.Ordinal) &&
                    state.IsNameStoneExtracted(stoneId) &&
                    !state.IsNameStoneOffered(stoneId))
                {
                    expected++;
                }
            }

            return expected;
        }

        private static bool TryPlanPendingRecoveryRemoval(
            int actualQuantity,
            int expectedQuantityAfterPendingConsumption,
            out int removalCount)
        {
            int actual = Mathf.Max(0, actualQuantity);
            int expected = Mathf.Max(
                0,
                expectedQuantityAfterPendingConsumption);
            if (actual == expected)
            {
                removalCount = 0;
                return true;
            }

            if (actual == expected + 1)
            {
                removalCount = 1;
                return true;
            }

            removalCount = 0;
            return false;
        }

        private static bool IsKnownOfferingId(string offeringId)
        {
            foreach (string known in OrderedOfferingIds)
            {
                if (string.Equals(
                        offeringId,
                        known,
                        StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsOfferingAvailable(
            CampaignStateService state,
            string offeringId)
        {
            if (state == null)
                return false;

            if (string.Equals(
                    offeringId,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                return state.PrologueCursedObjectRevealed;
            }

            return state.IsNameStoneExtracted(offeringId);
        }

        private static bool IsOfferingCommitted(
            CampaignStateService state,
            string offeringId)
        {
            if (state == null)
                return false;

            if (string.Equals(
                    offeringId,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                return state.PrologueCursedObjectOffered;
            }

            return state.IsNameStoneOffered(offeringId);
        }

        private static int GetItemQuantity(
            global::Inventory inventory,
            global::ItemStats item)
        {
            return inventory == null || item == null
                ? 0
                : Mathf.Max(0, inventory.FindItem(item).Value);
        }

        private static bool TryRemoveExactlyOne(
            global::Inventory inventory,
            global::ItemStats item,
            int quantityBefore)
        {
            inventory.RemoveItem(item.itemName, 1, false);
            return GetItemQuantity(inventory, item) == quantityBefore - 1;
        }

        private bool TryRollbackAndCancel(
            CampaignStateService state,
            CampaignInventoryCarryover carryover,
            global::Inventory inventory,
            global::ItemStats item,
            string offeringId,
            int expectedQuantity)
        {
            bool restored = TryRestoreExactQuantity(
                inventory,
                item,
                expectedQuantity);
            bool captured = restored && carryover.CaptureForTravel();
            bool canceled = captured &&
                            state.TryCancelPendingRootOffering(offeringId);
            return restored && captured && canceled;
        }

        private bool TryRestoreExactQuantity(
            global::Inventory inventory,
            global::ItemStats item,
            int expectedQuantity)
        {
            if (inventory == null || item == null)
                return false;

            int current = GetItemQuantity(inventory, item);
            while (current < expectedQuantity)
            {
                inventory.AddItem(
                    CampaignInventoryTokenUtility.CloneItemStats(
                        item,
                        1));

                int next = GetItemQuantity(inventory, item);
                if (next != current + 1)
                    return false;
                current = next;
            }

            if (current > expectedQuantity)
            {
                inventory.RemoveItem(
                    item.itemName,
                    current - expectedQuantity,
                    false);
            }

            return GetItemQuantity(inventory, item) == expectedQuantity;
        }

        private bool Reject(string reason)
        {
            lastFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "The Root Tree cannot accept that offering right now."
                : reason.Trim();
            Debug.LogWarning(lastFailureReason, this);
            CampaignEventUtility.Invoke(
                offerRejected,
                lastFailureReason,
                this);
            return false;
        }

        private bool HasCompetingInteractable()
        {
            int count = 0;
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.enabled &&
                    behaviour is global::IInteract)
                {
                    count++;
                }
            }

            return count != 1;
        }
    }
}
