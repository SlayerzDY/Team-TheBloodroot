using System;
using System.Collections;
using System.Reflection;
using Bloodroot.Features.FarmPrologue;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Campaign-owned interaction proxy for the cursed object revealed after
    /// the Farm chores. An owned token prefab supplies immutable item data;
    /// Safety's public ItemStats API receives only an owned runtime copy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FarmPrologueCursedObjectPickup :
        MonoBehaviour,
        global::IInteract
    {
        private const string CursedItemName = "CursedItem";

        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private CampaignInventoryCarryover inventoryCarryover;
        [SerializeField] private GameObject cursedItemTemplate;
        [SerializeField] private global::Inventory playerInventory;
        [SerializeField] private FarmPrologueDirector prologueDirector;

        [Header("Authored Presentation")]
        [SerializeField] private GameObject presentationRoot;
        [SerializeField] private Collider interactionCollider;

        [Header("Authored Events")]
        [SerializeField] private CampaignRootOfferingStringEvent pickupAccepted =
            new CampaignRootOfferingStringEvent();
        [SerializeField] private CampaignRootOfferingStringEvent pickupRejected =
            new CampaignRootOfferingStringEvent();

        private CampaignStateService subscribedState;
        private CampaignInventoryCarryover subscribedCarryover;
        private Coroutine refreshRoutine;
        private Coroutine pickupDissolveRoutine;
        private Coroutine safetyDissolveRoutine;
        private global::Dissolver activeDissolver;
        private bool transactionInProgress;
        private bool inventoryRecoveryPending;
        private bool pickupDissolveInProgress;
        private string lastFailureReason = string.Empty;

        public bool IsTransactionInProgress => transactionInProgress;
        public bool HasPendingInventoryRecovery => inventoryRecoveryPending;
        public string LastFailureReason => lastFailureReason;
        public bool IsPresentationVisible =>
            presentationRoot != null && presentationRoot.activeSelf;
        public GameObject CursedItemTemplate => cursedItemTemplate;
        public GameObject PresentationRoot => presentationRoot;
        public Collider InteractionCollider => interactionCollider;
        public bool HasExclusiveInteractionAuthority =>
            GetComponents<MonoBehaviour>()
                .CountEnabledInteractables() == 1;
        public CampaignRootOfferingStringEvent PickupRejectedEvent =>
            pickupRejected;

        private void Awake()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();

            SetPresentationActive(false);
        }

        private void OnEnable()
        {
            BindState();
            BindCarryover();
            SetPresentationActive(false);
            refreshRoutine = StartCoroutine(RefreshWhenInventoryReady());
        }

        private void OnDisable()
        {
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }

            StopPickupDissolve(true);
            UnbindState();
            UnbindCarryover();
        }

        private void OnDestroy()
        {
            StopPickupDissolve(false);
            UnbindState();
            UnbindCarryover();
        }

        private void OnValidate()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }

        public void Configure(
            CampaignStateService state,
            CampaignInventoryCarryover carryover,
            GameObject authoredCursedItemTemplate,
            global::Inventory inventory,
            FarmPrologueDirector director,
            GameObject authoredPresentationRoot,
            Collider authoredInteractionCollider)
        {
            StopPickupDissolve(true);
            UnbindState();
            UnbindCarryover();
            campaignState = state;
            inventoryCarryover = carryover;
            cursedItemTemplate = authoredCursedItemTemplate;
            playerInventory = inventory;
            prologueDirector = director;
            presentationRoot = authoredPresentationRoot;
            interactionCollider = authoredInteractionCollider;
            BindState();
            BindCarryover();
            RefreshPresentation();
        }

        public void SendInteract(Collider target)
        {
            TryClaimCursedObject();
        }

        public bool TryClaimCursedObject()
        {
            if (transactionInProgress)
                return Reject("The cursed object is already being secured.");

            if (!HasExclusiveInteractionAuthority)
            {
                return Reject(
                    "The campaign cursed-object proxy has another IInteract component attached.");
            }

            CampaignStateService state = ResolveState();
            global::Inventory inventory = ResolveInventory();
            global::ItemStats item = ResolveItem();
            CampaignInventoryCarryover carryover = ResolveCarryover();
            if (state == null || inventory == null || item == null ||
                carryover == null)
            {
                return Reject(
                    "The cursed object cannot be secured because campaign inventory is unavailable.");
            }

            if (!state.PrologueCursedObjectRevealed)
                return Reject("Finish the Farm chores before searching for the cursed object.");

            if (state.PrologueCursedObjectOffered)
                return Reject("The prologue cursed object has already been offered.");

            if (carryover.IsRestoreInProgress ||
                carryover.HasPendingRestoreFailure)
            {
                return Reject(
                    "Inventory recovery must finish before the cursed object can be collected.");
            }

            if (!ValidateCursedItem(item, out string itemError))
                return Reject(itemError);

            if (inventoryRecoveryPending &&
                !TryResolvePendingInventoryRecovery(carryover, item))
            {
                return Reject(
                    "The previous cursed-object inventory transaction is still waiting for a durable recovery save.");
            }

            CursedItemFingerprint templateFingerprint =
                CursedItemFingerprint.Capture(item);

            int quantityBefore = GetQuantity(inventory, item);
            if (quantityBefore > 0)
            {
                RefreshPresentation();
                return Reject("Take the cursed object already in your inventory to the Root Tree.");
            }

            if (!CanFitExactlyOne(inventory, item))
                return Reject("Make one inventory slot available for the cursed object.");

            transactionInProgress = true;
            try
            {
                inventory.AddItem(
                    CampaignInventoryTokenUtility.CloneItemStats(
                        item,
                        1));

                int quantityAfter = GetQuantity(inventory, item);
                if (quantityAfter != quantityBefore + 1 ||
                    !templateFingerprint.Matches(item))
                {
                    bool durablyCollected = TryRecoverFailedClaim(
                        carryover,
                        inventory,
                        item,
                        templateFingerprint,
                        quantityBefore);
                    if (durablyCollected)
                        return FinishSuccessfulClaim();

                    return Reject(
                        inventoryRecoveryPending
                            ? "The inventory did not accept exactly one cursed object and its recovery is still pending."
                            : "The inventory did not accept exactly one cursed object.");
                }

                if (!carryover.CaptureForTravel())
                {
                    bool durablyCollected = TryRecoverFailedClaim(
                        carryover,
                        inventory,
                        item,
                        templateFingerprint,
                        quantityBefore);
                    if (durablyCollected)
                        return FinishSuccessfulClaim();

                    return Reject(
                        inventoryRecoveryPending
                            ? "The cursed object inventory save failed and recovery is still pending."
                            : "The cursed object was not collected because its inventory save failed.");
                }

                if (!templateFingerprint.Matches(item))
                {
                    TryRecoverFailedClaim(
                        carryover,
                        inventory,
                        item,
                        templateFingerprint,
                        quantityBefore);
                    return Reject(
                        "The immutable cursed-object template changed during collection; the pickup was rejected safely.");
                }

                return FinishSuccessfulClaim();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                bool durablyCollected = TryRecoverFailedClaim(
                    carryover,
                    inventory,
                    item,
                    templateFingerprint,
                    quantityBefore);
                if (durablyCollected)
                    return FinishSuccessfulClaim();

                return Reject(
                    inventoryRecoveryPending
                        ? "An unexpected error interrupted the cursed object pickup and inventory recovery is still pending."
                        : "An unexpected error interrupted the cursed object pickup.");
            }
            finally
            {
                transactionInProgress = false;
            }
        }

        private bool FinishSuccessfulClaim()
        {
            inventoryRecoveryPending = false;
            lastFailureReason = string.Empty;
            SetInteractionColliderEnabled(false);
            prologueDirector?.PublishCampaignObjective(
                "Carry the cursed object to the Root Tree.",
                0,
                1);
            CampaignEventUtility.Invoke(
                pickupAccepted,
                CampaignRootOfferingIds.PrologueCursedObject,
                this);
            BeginSuccessfulPickupDissolve();
            return true;
        }

        private bool TryRecoverFailedClaim(
            CampaignInventoryCarryover carryover,
            global::Inventory inventory,
            global::ItemStats item,
            CursedItemFingerprint templateFingerprint,
            int quantityBefore)
        {
            bool restored = RollBackToQuantity(
                inventory,
                item,
                quantityBefore);
            bool templateUnchanged =
                item != null && templateFingerprint.Matches(item);
            bool captured = carryover != null &&
                            carryover.CaptureForTravel();
            int finalQuantity = GetQuantity(inventory, item);

            bool stableBaseline = restored && templateUnchanged && captured &&
                                  finalQuantity == quantityBefore;
            bool durablyCollected = templateUnchanged && captured &&
                                    finalQuantity == quantityBefore + 1;
            inventoryRecoveryPending = !stableBaseline && !durablyCollected;
            RefreshPresentation();
            return durablyCollected;
        }

        private bool TryResolvePendingInventoryRecovery(
            CampaignInventoryCarryover carryover,
            global::ItemStats item)
        {
            if (!inventoryRecoveryPending)
                return true;

            if (carryover == null || carryover.IsRestoreInProgress ||
                carryover.HasPendingRestoreFailure ||
                !ValidateCursedItem(item, out _) ||
                !carryover.CaptureForTravel())
            {
                return false;
            }

            inventoryRecoveryPending = false;
            RefreshPresentation();
            return true;
        }

        public void RefreshPresentation()
        {
            CampaignStateService state = ResolveState();
            CampaignInventoryCarryover carryover = ResolveCarryover();
            global::Inventory inventory = ResolveInventory();
            global::ItemStats item = ResolveItem();
            if (isActiveAndEnabled && subscribedCarryover != carryover)
            {
                BindCarryover();
            }

            bool restoreBlocked = carryover != null &&
                                  (carryover.IsRestoreInProgress ||
                                   carryover.HasPendingRestoreFailure);
            bool held = inventory != null && item != null &&
                        GetQuantity(inventory, item) > 0;
            bool visible = state != null &&
                           state.PrologueCursedObjectRevealed &&
                           !state.PrologueCursedObjectOffered &&
                           string.IsNullOrEmpty(state.PendingRootOfferingId) &&
                           !held &&
                           !restoreBlocked;

            if (pickupDissolveInProgress)
            {
                if (presentationRoot != null &&
                    !presentationRoot.activeSelf)
                {
                    presentationRoot.SetActive(true);
                }

                SetInteractionColliderEnabled(false);
            }
            else
            {
                SetPresentationActive(visible);
            }

            if (state == null || !state.PrologueCursedObjectRevealed ||
                state.PrologueCursedObjectOffered)
            {
                return;
            }

            if (held || string.Equals(
                    state.PendingRootOfferingId,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                prologueDirector?.PublishCampaignObjective(
                    "Carry the cursed object to the Root Tree.",
                    0,
                    1);
            }
            else if (visible)
            {
                prologueDirector?.PublishCampaignObjective(
                    "A cursed object surfaced near the water trough. Recover it.",
                    0,
                    1);
            }
        }

        private IEnumerator RefreshWhenInventoryReady()
        {
            yield return null;
            yield return null;

            while (isActiveAndEnabled)
            {
                BindState();
                BindCarryover();
                CampaignInventoryCarryover carryover = ResolveCarryover();
                if (carryover == null || !carryover.IsRestoreInProgress)
                    break;

                yield return null;
            }

            RefreshPresentation();
            refreshRoutine = null;
        }

        private void HandleProgressChanged(CampaignProgressSnapshot snapshot)
        {
            BindCarryover();
            RefreshPresentation();
        }

        private void HandleInventoryRestoreCompleted(bool restored)
        {
            RefreshPresentation();
        }

        private void BindState()
        {
            CampaignStateService state = ResolveState();
            if (subscribedState == state)
                return;

            UnbindState();
            subscribedState = state;
            if (subscribedState != null)
            {
                subscribedState.ProgressChanged += HandleProgressChanged;
            }
        }

        private void UnbindState()
        {
            if (subscribedState != null)
            {
                subscribedState.ProgressChanged -= HandleProgressChanged;
                subscribedState = null;
            }
        }

        private void BindCarryover()
        {
            CampaignInventoryCarryover carryover = ResolveCarryover();
            if (subscribedCarryover == carryover)
                return;

            UnbindCarryover();
            subscribedCarryover = carryover;
            if (subscribedCarryover != null)
            {
                subscribedCarryover.RestoreCompleted +=
                    HandleInventoryRestoreCompleted;
            }
        }

        private void UnbindCarryover()
        {
            if (subscribedCarryover != null)
            {
                subscribedCarryover.RestoreCompleted -=
                    HandleInventoryRestoreCompleted;
                subscribedCarryover = null;
            }
        }

        private CampaignStateService ResolveState()
        {
            CampaignStateService persistentState =
                CampaignStateService.Instance;
            if (persistentState != null && campaignState != persistentState)
            {
                campaignState = persistentState;
            }
            else if (campaignState == null)
            {
                campaignState = persistentState;
            }

            return campaignState;
        }

        private CampaignInventoryCarryover ResolveCarryover()
        {
            CampaignStateService authority = ResolveState();
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

            if (global::gameManager.instance != null &&
                global::gameManager.instance.player != null)
            {
                playerInventory = global::gameManager.instance.player
                    .GetComponent<global::Inventory>();
            }

            return playerInventory;
        }

        private global::ItemStats ResolveItem()
        {
            return CampaignInventoryTokenUtility.GetItemStats(
                cursedItemTemplate);
        }

        private void BeginSuccessfulPickupDissolve()
        {
            SetInteractionColliderEnabled(false);

            try
            {
                if (!TryGetConfiguredDissolver(
                        out global::Dissolver dissolver,
                        out string failureReason) ||
                    !isActiveAndEnabled)
                {
                    FailPickupDissolve(
                        string.IsNullOrEmpty(failureReason)
                            ? "the owned pickup proxy is not active"
                            : failureReason);
                    return;
                }

                presentationRoot.SetActive(true);
                activeDissolver = dissolver;
                dissolver.enabled = true;
                if (!dissolver.isActiveAndEnabled)
                {
                    FailPickupDissolve(
                        "Safety's Dissolver could not be enabled at runtime");
                    return;
                }

                pickupDissolveInProgress = true;
                pickupDissolveRoutine = StartCoroutine(
                    RunSuccessfulPickupDissolve(dissolver));
                if (pickupDissolveRoutine == null)
                {
                    FailPickupDissolve(
                        "the owned dissolve adapter could not start");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                FailPickupDissolve(
                    "Safety's Dissolver could not be prepared");
            }
        }

        private IEnumerator RunSuccessfulPickupDissolve(
            global::Dissolver dissolver)
        {
            // Safety's Dissolver initializes its original color in Start.
            // It is authored disabled, so allow one frame after enabling it.
            yield return null;

            string failureReason = string.Empty;
            if (!pickupDissolveInProgress ||
                activeDissolver != dissolver ||
                dissolver == null ||
                presentationRoot == null ||
                !presentationRoot.activeInHierarchy ||
                !dissolver.isActiveAndEnabled ||
                !IsDissolverConfigured(dissolver, out failureReason))
            {
                FailPickupDissolve(
                    string.IsNullOrEmpty(failureReason)
                        ? "Safety's Dissolver became unavailable before pickup presentation"
                        : failureReason);
                yield break;
            }

            try
            {
                safetyDissolveRoutine = dissolver.StartCoroutine(
                    dissolver.dissolve());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                FailPickupDissolve(
                    "Safety's dissolve coroutine could not start");
                yield break;
            }

            if (safetyDissolveRoutine == null)
            {
                FailPickupDissolve(
                    "Safety's dissolve coroutine was unavailable");
                yield break;
            }

            yield return safetyDissolveRoutine;

            safetyDissolveRoutine = null;
            activeDissolver = null;
            pickupDissolveRoutine = null;
            pickupDissolveInProgress = false;
            SetPresentationActive(false);
        }

        private bool TryGetConfiguredDissolver(
            out global::Dissolver dissolver,
            out string failureReason)
        {
            dissolver = null;
            if (presentationRoot == null)
            {
                failureReason = "the authored cursed-object presentation is missing";
                return false;
            }

            global::Dissolver[] dissolvers = presentationRoot
                .GetComponentsInChildren<global::Dissolver>(true);
            if (dissolvers.Length != 1 || dissolvers[0] == null)
            {
                failureReason =
                    "the authored Safety presentation does not contain exactly one Dissolver";
                return false;
            }

            dissolver = dissolvers[0];
            return IsDissolverConfigured(dissolver, out failureReason);
        }

        private static bool IsDissolverConfigured(
            global::Dissolver dissolver,
            out string failureReason)
        {
            if (dissolver == null)
            {
                failureReason = "Safety's Dissolver component is missing";
                return false;
            }

            const BindingFlags fields =
                BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo materialField = typeof(global::Dissolver).GetField(
                "dissolveMaterial",
                fields);
            FieldInfo modelField = typeof(global::Dissolver).GetField(
                "model",
                fields);
            FieldInfo durationField = typeof(global::Dissolver).GetField(
                "dissolveDuration",
                fields);
            Material material = materialField?.GetValue(dissolver) as Material;
            Renderer model = modelField?.GetValue(dissolver) as Renderer;
            object durationValue = durationField?.GetValue(dissolver);
            float duration = durationValue is float authoredDuration
                ? authoredDuration
                : 0f;
            if (material == null || model == null || model.sharedMaterial == null)
            {
                failureReason =
                    "Safety's Dissolver is missing its inherited material or model configuration";
                return false;
            }

            if (duration <= 0f || float.IsNaN(duration) ||
                float.IsInfinity(duration))
            {
                failureReason =
                    "Safety's Dissolver requires a finite positive duration";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private void FailPickupDissolve(string reason)
        {
            Debug.LogWarning(
                $"The cursed object was secured, but its Safety dissolve " +
                $"presentation was hidden immediately because {reason}.",
                this);

            pickupDissolveInProgress = false;
            pickupDissolveRoutine = null;
            if (activeDissolver != null && safetyDissolveRoutine != null)
            {
                activeDissolver.StopCoroutine(safetyDissolveRoutine);
            }

            safetyDissolveRoutine = null;
            if (activeDissolver != null)
            {
                activeDissolver.enabled = false;
                activeDissolver = null;
            }

            SetPresentationActive(false);
        }

        private void StopPickupDissolve(bool hidePresentation)
        {
            pickupDissolveInProgress = false;
            if (pickupDissolveRoutine != null)
            {
                StopCoroutine(pickupDissolveRoutine);
                pickupDissolveRoutine = null;
            }

            if (activeDissolver != null && safetyDissolveRoutine != null)
            {
                activeDissolver.StopCoroutine(safetyDissolveRoutine);
            }

            safetyDissolveRoutine = null;
            if (activeDissolver != null)
            {
                activeDissolver.enabled = false;
                activeDissolver = null;
            }

            if (hidePresentation)
                SetPresentationActive(false);
            else
                SetInteractionColliderEnabled(false);
        }

        private void SetPresentationActive(bool active)
        {
            bool hasPresentation = presentationRoot != null;
            if (hasPresentation &&
                presentationRoot.activeSelf != active)
            {
                presentationRoot.SetActive(active);
            }

            SetInteractionColliderEnabled(active && hasPresentation);
        }

        private void SetInteractionColliderEnabled(bool active)
        {
            if (interactionCollider != null)
                interactionCollider.enabled = active;
        }

        private bool Reject(string reason)
        {
            lastFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "The cursed object cannot be collected right now."
                : reason.Trim();
            CampaignEventUtility.Invoke(
                pickupRejected,
                lastFailureReason,
                this);
            return false;
        }

        private readonly struct CursedItemFingerprint
        {
            private readonly global::ItemStats source;
            private readonly string itemName;
            private readonly string itemDescription;
            private readonly Sprite icon;
            private readonly float weight;
            private readonly int quantity;
            private readonly int stackSize;
            private readonly GameObject itemMesh;
            private readonly AudioClip[] pickupSounds;
            private readonly global::ItemHeroStats itemIncreases;

            private CursedItemFingerprint(global::ItemStats item)
            {
                source = item;
                itemName = item?.itemName;
                itemDescription = item?.itemDescription;
                icon = item?.icon;
                weight = item?.weight ?? 0f;
                quantity = item?.quantity ?? 0;
                stackSize = item?.stackSize ?? 0;
                itemMesh = item?.itemMesh;
                pickupSounds = item?.pickupSound == null
                    ? null
                    : (AudioClip[])item.pickupSound.Clone();
                itemIncreases = item?.itemIncreases;
            }

            public static CursedItemFingerprint Capture(
                global::ItemStats item)
            {
                return new CursedItemFingerprint(item);
            }

            public bool Matches(global::ItemStats item)
            {
                if (!ReferenceEquals(source, item) || item == null ||
                    !string.Equals(
                        itemName,
                        item.itemName,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        itemDescription,
                        item.itemDescription,
                        StringComparison.Ordinal) ||
                    icon != item.icon ||
                    !Mathf.Approximately(weight, item.weight) ||
                    quantity != item.quantity ||
                    stackSize != item.stackSize ||
                    itemMesh != item.itemMesh ||
                    itemIncreases != item.itemIncreases)
                {
                    return false;
                }

                AudioClip[] currentSounds = item.pickupSound;
                if (pickupSounds == null || currentSounds == null)
                    return pickupSounds == null && currentSounds == null;
                if (pickupSounds.Length != currentSounds.Length)
                    return false;

                for (int index = 0; index < pickupSounds.Length; index++)
                {
                    if (pickupSounds[index] != currentSounds[index])
                        return false;
                }

                return true;
            }
        }

        private static bool ValidateCursedItem(
            global::ItemStats item,
            out string error)
        {
            if (item == null ||
                !string.Equals(
                    item.itemName?.Trim(),
                    CursedItemName,
                    StringComparison.Ordinal) ||
                item.quantity != 1 || item.stackSize != 1)
            {
                error =
                    "The authored prologue object must contain exactly one non-stacking CursedItem.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool CanFitExactlyOne(
            global::Inventory inventory,
            global::ItemStats item)
        {
            if (inventory?.inventoryItems == null || item == null)
                return false;

            for (int index = 0; index < inventory.inventoryItems.Length; index++)
            {
                global::ItemStats existing = inventory.inventoryItems[index];
                if (existing == null || string.IsNullOrEmpty(existing.itemName))
                    return true;

                if (string.Equals(
                        existing.itemName,
                        item.itemName,
                        StringComparison.Ordinal) &&
                    existing.quantity < existing.stackSize)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetQuantity(
            global::Inventory inventory,
            global::ItemStats item)
        {
            return inventory == null || item == null
                ? 0
                : Mathf.Max(0, inventory.FindItem(item).Value);
        }

        private static bool RollBackToQuantity(
            global::Inventory inventory,
            global::ItemStats item,
            int expected)
        {
            if (inventory == null || item == null)
                return false;

            int current = GetQuantity(inventory, item);
            int excess = Mathf.Max(0, current - Mathf.Max(0, expected));
            if (excess > 0)
            {
                inventory.RemoveItem(item.itemName, excess, false);
            }

            return GetQuantity(inventory, item) == Mathf.Max(0, expected);
        }
    }

    internal static class FarmPrologueCursedObjectInteractionUtility
    {
        internal static int CountEnabledInteractables(
            this MonoBehaviour[] behaviours)
        {
            int count = 0;
            if (behaviours == null)
                return count;

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null && behaviour.enabled &&
                    behaviour is global::IInteract)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
