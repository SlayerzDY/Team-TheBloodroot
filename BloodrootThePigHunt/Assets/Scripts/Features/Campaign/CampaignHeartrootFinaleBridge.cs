using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Bloodroot.Campaign
{
    public enum CampaignHeartrootPresentationMode
    {
        ExposedInHollow = 0,
        CarriedByPlayer = 1,
        FarmBurnSite = 2
    }

    /// <summary>
    /// Owned transaction and presentation boundary for the exposed
    /// Heartroot. It adapts only through Safety's public Inventory/Save APIs,
    /// keeps campaign state authoritative, and never changes Safety schemas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignHeartrootFinaleBridge : MonoBehaviour
    {
        [Header("Persistence")]
        [SerializeField] private CampaignStateService stateService;
        [SerializeField] private CampaignInventoryCarryover inventoryCarryover;
        [SerializeField] private global::Inventory playerInventory;
        [SerializeField] private GameObject heartrootInventoryTokenPickup;

        [Header("Production Presentation")]
        [SerializeField] private CampaignHeartrootPresentationMode
            presentationMode;
        [SerializeField] private GameObject productionHeartrootVisualPrefab;
        [SerializeField] private Transform presentationSocket;
        [SerializeField] private GameObject authoredPresentationInstance;
        [SerializeField] private bool restrictToAuthoredCampaignScene = true;

        [Header("Authored Events")]
        [SerializeField] private UnityEvent recoveryAccepted = new();
        [SerializeField] private UnityEvent recoveryRejected = new();
        [SerializeField] private UnityEvent burnAccepted = new();
        [SerializeField] private UnityEvent burnRejected = new();

        private GameObject presentationInstance;
        private CampaignStateService subscribedState;
        private bool ownsPresentationInstance;
        private bool transactionInProgress;
        private string lastFailureReason = string.Empty;

#if UNITY_EDITOR
        private bool bypassSafetyCheckpointForTests;
        private bool bypassSceneRequirementForTests;
#endif

        public event Action HeartrootRecovered;
        public event Action HeartrootBurned;

        public CampaignHeartrootPresentationMode PresentationMode =>
            presentationMode;
        public GameObject PresentationInstance => presentationInstance;
        public GameObject AuthoredPresentationInstance =>
            authoredPresentationInstance;
        public bool HasPresentationInstance => presentationInstance != null;
        public bool IsTransactionInProgress => transactionInProgress;
        public string LastFailureReason => lastFailureReason;

        private void OnEnable()
        {
            BindStateService();
            RefreshPresentation();
        }

        private void OnDisable()
        {
            UnbindStateService();
        }

        private void OnDestroy()
        {
            UnbindStateService();
            DestroyPresentationInstance();
        }

        public void Configure(
            CampaignStateService campaignState,
            CampaignInventoryCarryover carryover,
            global::Inventory inventory,
            GameObject authoredTokenPickup,
            GameObject authoredProductionVisual,
            Transform authoredPresentationSocket,
            CampaignHeartrootPresentationMode mode,
            bool restrictToCampaignScene = true,
            GameObject authoredVisualInstance = null)
        {
            UnbindStateService();
            stateService = campaignState;
            inventoryCarryover = carryover;
            playerInventory = inventory;
            heartrootInventoryTokenPickup = authoredTokenPickup;
            productionHeartrootVisualPrefab = authoredProductionVisual;
            presentationSocket = authoredPresentationSocket;
            authoredPresentationInstance = authoredVisualInstance;
            presentationMode = mode;
            restrictToAuthoredCampaignScene = restrictToCampaignScene;
            BindStateService();
            RefreshPresentation();
        }

#if UNITY_EDITOR
        public void ConfigureEditorTestOverrides(
            bool bypassSafetyCheckpoint,
            bool bypassSceneRequirement)
        {
            bypassSafetyCheckpointForTests = bypassSafetyCheckpoint;
            bypassSceneRequirementForTests = bypassSceneRequirement;
        }
#endif

        public bool ValidateRuntimeContract(out string error)
        {
            error = string.Empty;
            CampaignStateService state = ResolveStateService();
            CampaignInventoryCarryover carryover =
                ResolveInventoryCarryover();
            global::ItemStats token = ResolveTokenDefinition();
            if (state == null)
            {
                error = "Campaign Heartroot persistence is unavailable.";
                return false;
            }

            if (carryover == null || carryover.CatalogCount !=
                CampaignHeartrootInventoryIds.RequiredCatalogCount ||
                !carryover.ValidateCatalog(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Heartroot persistence requires the exact seven-entry campaign inventory catalog."
                    : error;
                return false;
            }

            if (!ValidateTokenDefinition(token, out error))
                return false;

            bool hasReusableAuthoredInstance =
                authoredPresentationInstance != null;
            bool hasPrefabInstantiationContract =
                productionHeartrootVisualPrefab != null &&
                presentationSocket != null;
            if (hasReusableAuthoredInstance ==
                hasPrefabInstantiationContract)
            {
                error =
                    "Provide exactly one Heartroot presentation topology: either the single authored production HEARTROOT_CORE instance, or its production prefab plus presentation socket.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryRecoverHeartroot(GameObject requester = null)
        {
            if (transactionInProgress)
                return Reject(false, "A Heartroot transaction is already being saved.");

            CampaignStateService state = ResolveStateService();
            CampaignProgressSnapshot progress = state != null
                ? state.Current
                : default;
            if (state == null || !progress.HollowVeilCrossed ||
                progress.DefeatedWitchCount != 3 ||
                !progress.HeartrootExposed || progress.HeartrootCarried ||
                progress.HeartrootBurned || progress.CampaignCompleted)
            {
                return Reject(
                    false,
                    "The Heartroot cannot be recovered before all three witches are durably defeated.");
            }

            if (!IsRequiredScene(CampaignSceneNames.OpenWorld))
            {
                return Reject(
                    false,
                    "The exposed Heartroot can be recovered only in the Open World Hollow.");
            }

            return TryChangeHeartrootCargo(
                requester,
                expectedStartingQuantity: 0,
                expectedEndingQuantity: 1,
                recover: true);
        }

        public bool TryBurnHeartroot(GameObject requester = null)
        {
            if (transactionInProgress)
                return Reject(true, "A Heartroot transaction is already being saved.");

            CampaignStateService state = ResolveStateService();
            CampaignProgressSnapshot progress = state != null
                ? state.Current
                : default;
            if (state == null || !progress.HollowCompleted ||
                !progress.HeartrootExposed || !progress.HeartrootCarried ||
                progress.HeartrootBurned || progress.CampaignCompleted)
            {
                return Reject(
                    true,
                    "The exposed Heartroot must be current cargo before it can be burned.");
            }

            if (!IsRequiredScene(CampaignSceneNames.FarmPrologueHub))
            {
                return Reject(
                    true,
                    "The exposed Heartroot can be burned only at the Farm ritual site.");
            }

            return TryChangeHeartrootCargo(
                requester,
                expectedStartingQuantity: 1,
                expectedEndingQuantity: 0,
                recover: false);
        }

        private bool TryChangeHeartrootCargo(
            GameObject requester,
            int expectedStartingQuantity,
            int expectedEndingQuantity,
            bool recover)
        {
            if (!ValidateRuntimeContract(out string contractError))
                return Reject(!recover, contractError);

            CampaignStateService state = ResolveStateService();
            CampaignInventoryCarryover carryover =
                ResolveInventoryCarryover();
            global::Inventory inventory = ResolveInventory(requester);
            global::ItemStats token = ResolveTokenDefinition();
            if (inventory == null)
            {
                return Reject(
                    !recover,
                    "The active Player Inventory is unavailable.");
            }

            int startingQuantity = GetTokenQuantity(inventory, token);
            if (startingQuantity != expectedStartingQuantity)
            {
                return Reject(
                    !recover,
                    $"Stable Heartroot token quantity was {startingQuantity}; expected {expectedStartingQuantity}.");
            }

            if (recover && !HasOneFreeInventorySlot(inventory))
            {
                return Reject(
                    false,
                    "The Player Inventory has no free slot for the exposed Heartroot.");
            }

            if (!state.TryExportCheckpoint(
                    out string previousCampaignCheckpoint,
                    out string checkpointError))
            {
                return Reject(
                    !recover,
                    "The pre-transaction campaign checkpoint is unavailable: " +
                    checkpointError);
            }

            transactionInProgress = true;
            bool campaignCommitted = false;
            bool pairCommitted = false;
            try
            {
                if (!TrySetExactTokenQuantity(
                        inventory,
                        token,
                        expectedEndingQuantity))
                {
                    return Reject(
                        !recover,
                        "The public Inventory API did not apply exactly one Heartroot token change.");
                }

                string inventoryError = string.Empty;
                if (!carryover.TryCaptureForAreaCompletionFromInventory(
                        inventory,
                        out string[] itemIds,
                        out int[] quantities) ||
                    !CampaignStateService.TryValidateHeartrootInventorySnapshot(
                        itemIds,
                        quantities,
                        expectedEndingQuantity,
                        out inventoryError))
                {
                    TrySetExactTokenQuantity(
                        inventory,
                        token,
                        startingQuantity);
                    return Reject(
                        !recover,
                        string.IsNullOrWhiteSpace(inventoryError)
                            ? "The seven-entry Heartroot inventory snapshot could not be captured."
                            : inventoryError);
                }

                campaignCommitted = recover
                    ? state.TryRecoverExposedHeartroot(itemIds, quantities)
                    : state.TryBurnExposedHeartroot(itemIds, quantities);
                if (!campaignCommitted)
                {
                    TrySetExactTokenQuantity(
                        inventory,
                        token,
                        startingQuantity);
                    return Reject(
                        !recover,
                        "The Heartroot campaign transition could not be saved.");
                }

                if (!TryCommitPairedSafetyCheckpoint(
                        state,
                        carryover,
                        inventory,
                        token,
                        itemIds,
                        quantities,
                        previousCampaignCheckpoint,
                        startingQuantity,
                        out string persistenceError))
                {
                    return Reject(!recover, persistenceError);
                }

                pairCommitted = true;
                lastFailureReason = string.Empty;
                try
                {
                    carryover.AcceptAreaCompletionSnapshot(quantities);
                    if (recover)
                    {
                        state.PublishHeartrootRecoveryCommitted();
                        CampaignEventUtility.Invoke(recoveryAccepted, this);
                        CampaignEventUtility.Invoke(HeartrootRecovered, this);
                    }
                    else
                    {
                        state.PublishHeartrootBurnCommitted();
                        CampaignEventUtility.Invoke(burnAccepted, this);
                        CampaignEventUtility.Invoke(HeartrootBurned, this);
                    }

                    RefreshPresentation();
                }
                catch (Exception presentationException)
                {
                    // The campaign and paired Safety files are already the
                    // durable authority. Never roll them back after commit;
                    // presentation reconstructs from those facts on reload.
                    Debug.LogException(presentationException, this);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (pairCommitted)
                {
                    lastFailureReason = string.Empty;
                    return true;
                }

                if (campaignCommitted)
                {
                    bool campaignRolledBack = state.TryRestoreCheckpoint(
                        previousCampaignCheckpoint,
                        out _);
                    if (campaignRolledBack)
                    {
                        TrySetExactTokenQuantity(
                            inventory,
                            token,
                            startingQuantity);
                    }
                    else
                    {
                        CampaignProgressSnapshot durable = state.Current;
                        int authoritativeQuantity =
                            durable.HeartrootCarried &&
                            !durable.HeartrootBurned
                                ? 1
                                : 0;
                        TrySetExactTokenQuantity(
                            inventory,
                            token,
                            authoritativeQuantity);
                        carryover.MarkInventoryRecoveryPending(
                            "An unexpected Heartroot persistence exception could not roll campaign state back. Reload before traveling.");
                    }
                }
                else
                {
                    TrySetExactTokenQuantity(
                        inventory,
                        token,
                        startingQuantity);
                }

                return Reject(
                    !recover,
                    "An unexpected error interrupted the Heartroot transaction.");
            }
            finally
            {
                transactionInProgress = false;
            }
        }

        private bool TryCommitPairedSafetyCheckpoint(
            CampaignStateService state,
            CampaignInventoryCarryover carryover,
            global::Inventory inventory,
            global::ItemStats token,
            string[] itemIds,
            int[] quantities,
            string previousCampaignCheckpoint,
            int startingTokenQuantity,
            out string error)
        {
#if UNITY_EDITOR
            if (bypassSafetyCheckpointForTests)
            {
                error = string.Empty;
                return true;
            }
#endif

            CampaignSafetySaveIntegration.SaveTransaction transaction = null;
            string safetyError = string.Empty;
            bool safetyStarted =
                CampaignSafetySaveIntegration.TryBeginCurrentGameSave(
                    out transaction,
                    out safetyError);
            bool checkpointWritten = safetyStarted &&
                                     CampaignSafetySaveIntegration
                                         .TryWriteCampaignInventoryCheckpoint(
                                             itemIds,
                                             quantities,
                                             out safetyError);
            if (checkpointWritten)
            {
                transaction.Commit();
                error = string.Empty;
                return true;
            }

            string safetyRollbackError = string.Empty;
            bool safetyRolledBack = transaction == null ||
                                    transaction.TryRollback(
                                        out safetyRollbackError);
            bool campaignRolledBack = state.TryRestoreCheckpoint(
                previousCampaignCheckpoint,
                out string campaignRollbackError);
            bool inventoryRolledBack = false;
            if (campaignRolledBack)
            {
                inventoryRolledBack = TrySetExactTokenQuantity(
                    inventory,
                    token,
                    startingTokenQuantity);
            }

            if (!campaignRolledBack || !inventoryRolledBack ||
                !safetyRolledBack)
            {
                carryover.MarkInventoryRecoveryPending(
                    "Heartroot persistence rollback was incomplete. The durable campaign fact remains authoritative; reload before traveling.");
            }

            error =
                "The paired Safety save/checkpoint rejected the Heartroot transaction.";
            if (!string.IsNullOrWhiteSpace(safetyError))
                error += " " + safetyError.Trim();
            if (!string.IsNullOrWhiteSpace(safetyRollbackError))
                error += " Safety rollback: " + safetyRollbackError.Trim();
            if (!string.IsNullOrWhiteSpace(campaignRollbackError))
                error += " Campaign rollback: " +
                         campaignRollbackError.Trim();
            return false;
        }

        public void RefreshPresentation()
        {
            CampaignStateService state = ResolveStateService();
            CampaignProgressSnapshot progress = state != null
                ? state.Current
                : default;
            bool shouldPresent = state != null &&
                                 IsPresentationSceneAllowed() &&
                                 (presentationMode switch
                                 {
                                     CampaignHeartrootPresentationMode
                                         .ExposedInHollow =>
                                         progress.HeartrootExposed &&
                                         !progress.HeartrootCarried &&
                                         !progress.HeartrootBurned,
                                     CampaignHeartrootPresentationMode
                                         .CarriedByPlayer =>
                                         progress.HeartrootCarried &&
                                         !progress.HeartrootBurned,
                                     CampaignHeartrootPresentationMode
                                         .FarmBurnSite =>
                                         progress.HeartrootCarried &&
                                         !progress.HeartrootBurned,
                                     _ => false
                                 });

            if (authoredPresentationInstance != null)
            {
                if (presentationInstance != null &&
                    ownsPresentationInstance)
                {
                    DestroyPresentationInstance();
                }

                presentationInstance = shouldPresent
                    ? authoredPresentationInstance
                    : null;
                ownsPresentationInstance = false;
                if (authoredPresentationInstance.activeSelf != shouldPresent)
                    authoredPresentationInstance.SetActive(shouldPresent);
                if (shouldPresent)
                    MakePresentationVisualOnly(authoredPresentationInstance);
                return;
            }

            if (!shouldPresent || productionHeartrootVisualPrefab == null ||
                presentationSocket == null)
            {
                DestroyPresentationInstance();
                return;
            }

            if (presentationInstance != null)
                return;

            presentationInstance = Instantiate(
                productionHeartrootVisualPrefab,
                presentationSocket,
                false);
            presentationInstance.name =
                productionHeartrootVisualPrefab.name +
                "_CampaignPresentation";
            ownsPresentationInstance = true;
            MakePresentationVisualOnly(presentationInstance);
        }

        private static void MakePresentationVisualOnly(GameObject visual)
        {
            if (visual == null)
                return;

            foreach (Collider presentationCollider in
                     visual.GetComponentsInChildren<Collider>(true))
            {
                presentationCollider.enabled = false;
            }

            foreach (Rigidbody body in
                     visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private void HandleProgressChanged(CampaignProgressSnapshot _)
        {
            RefreshPresentation();
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
                subscribedState.ProgressChanged -= HandleProgressChanged;

            subscribedState = null;
        }

        private CampaignStateService ResolveStateService()
        {
            CampaignStateService persistent =
                CampaignStateService.Instance;
            if (persistent != null && persistent != stateService)
                stateService = persistent;

            return stateService;
        }

        private CampaignInventoryCarryover ResolveInventoryCarryover()
        {
            if (inventoryCarryover == null)
            {
                inventoryCarryover = ResolveStateService()
                    ?.GetComponent<CampaignInventoryCarryover>();
            }

            return inventoryCarryover;
        }

        private global::Inventory ResolveInventory(GameObject requester)
        {
            if (playerInventory != null)
                return playerInventory;

            if (requester != null)
            {
                playerInventory =
                    requester.GetComponentInParent<global::Inventory>();
            }

            if (playerInventory == null && global::gameManager.instance != null &&
                global::gameManager.instance.player != null)
            {
                playerInventory = global::gameManager.instance.player
                    .GetComponent<global::Inventory>();
            }

            return playerInventory;
        }

        private global::ItemStats ResolveTokenDefinition()
        {
            return CampaignInventoryTokenUtility.GetItemStats(
                heartrootInventoryTokenPickup);
        }

        private static bool ValidateTokenDefinition(
            global::ItemStats token,
            out string error)
        {
            if (token == null ||
                !string.Equals(
                    token.itemID?.Trim(),
                    CampaignHeartrootInventoryIds.SerializedItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    token.itemName?.Trim(),
                    CampaignHeartrootInventoryIds.ItemName,
                    StringComparison.Ordinal) ||
                token.quantity != 1 || token.stackSize != 1)
            {
                error =
                    $"Heartroot token must use stable binding '{CampaignHeartrootInventoryIds.StableId}', itemID '{CampaignHeartrootInventoryIds.SerializedItemId}', itemName '{CampaignHeartrootInventoryIds.ItemName}', quantity 1, and stack size 1.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static int GetTokenQuantity(
            global::Inventory inventory,
            global::ItemStats token)
        {
            return inventory == null || token == null
                ? 0
                : Mathf.Max(0, inventory.FindItem(token).Value);
        }

        private static bool TrySetExactTokenQuantity(
            global::Inventory inventory,
            global::ItemStats token,
            int targetQuantity)
        {
            if (inventory == null || token == null || targetQuantity < 0 ||
                targetQuantity > 1)
            {
                return false;
            }

            try
            {
                int current = GetTokenQuantity(inventory, token);
                if (current < targetQuantity)
                {
                    inventory.AddItem(
                        CampaignInventoryTokenUtility.CloneItemStats(
                            token,
                            targetQuantity - current));
                }
                else if (current > targetQuantity)
                {
                    inventory.RemoveItem(
                        token.itemName,
                        current - targetQuantity,
                        false);
                }

                return GetTokenQuantity(inventory, token) == targetQuantity;
            }
            catch (Exception)
            {
                return GetTokenQuantity(inventory, token) == targetQuantity;
            }
        }

        private static bool HasOneFreeInventorySlot(
            global::Inventory inventory)
        {
            if (inventory == null)
                return false;

            try
            {
                for (int index = 0;
                     index < 4096 && inventory.IsValidIndex(index);
                     index++)
                {
                    if (inventory.IsSlotEmpty(index))
                        return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private bool IsRequiredScene(string requiredScene)
        {
#if UNITY_EDITOR
            if (bypassSceneRequirementForTests)
                return true;
#endif
            return string.Equals(
                SceneManager.GetActiveScene().name,
                requiredScene,
                StringComparison.Ordinal);
        }

        private bool IsPresentationSceneAllowed()
        {
            if (!restrictToAuthoredCampaignScene)
                return true;

            string required = presentationMode ==
                              CampaignHeartrootPresentationMode.FarmBurnSite
                ? CampaignSceneNames.FarmPrologueHub
                : CampaignSceneNames.OpenWorld;
            return string.Equals(
                SceneManager.GetActiveScene().name,
                required,
                StringComparison.Ordinal);
        }

        private bool Reject(bool burning, string reason)
        {
            lastFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "The Heartroot transaction was rejected."
                : reason.Trim();
            Debug.LogWarning(lastFailureReason, this);
            CampaignEventUtility.Invoke(
                burning ? burnRejected : recoveryRejected,
                this);
            return false;
        }

        private void DestroyPresentationInstance()
        {
            if (presentationInstance == null)
                return;

            if (!ownsPresentationInstance)
            {
                if (presentationInstance == authoredPresentationInstance)
                    presentationInstance.SetActive(false);
                presentationInstance = null;
                return;
            }

            if (Application.isPlaying)
                Destroy(presentationInstance);
            else
                DestroyImmediate(presentationInstance);
            presentationInstance = null;
            ownsPresentationInstance = false;
        }

        private void OnValidate()
        {
            if (heartrootInventoryTokenPickup != null)
            {
                ValidateTokenDefinition(
                    ResolveTokenDefinition(),
                    out _);
            }
        }
    }
}
