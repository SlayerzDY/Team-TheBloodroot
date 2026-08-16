using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bloodroot.Campaign
{
    public enum CampaignTravelFailure
    {
        AlreadyTraveling,
        MissingCampaignState,
        InvalidSceneOrDestination,
        AreaLocked,
        RootOfferingPending,
        FarmEmergencePending,
        InventoryRestorePending,
        SceneNotInBuild,
        SaveFailed,
        SceneLoadFailed
    }

    /// <summary>
    /// Inspector-friendly travel hook for trucks, doors, and mission exits.
    /// It persists the destination before loading and creates no presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignSceneTravel : MonoBehaviour
    {
        [SerializeField]
        private CampaignStateService campaignState;

        [SerializeField]
        private string destinationSceneName = CampaignSceneNames.OpenWorld;

        [SerializeField]
        private string spawnDestinationId = string.Empty;

        [SerializeField]
        private bool requireUnlockedArea;

        [SerializeField]
        private CampaignAreaId requiredArea = CampaignAreaId.BlackPines;

        private bool isTraveling;

        public bool IsTraveling => isTraveling;

        public string DestinationSceneName => destinationSceneName;

        public string SpawnDestinationId => spawnDestinationId;

        public event Action<string, string> TravelStarted;
        public event Action<CampaignTravelFailure> TravelRejected;

        public void Travel()
        {
            TryTravel();
        }

        /// <summary>
        /// UnityEvent bridge for the online-safety extraction menu. Safety
        /// remains responsible for the key check and opening the menu; this
        /// owned backend only performs the durable campaign handoff and closes
        /// the Safety menu after the scene load has been accepted.
        /// </summary>
        public void TravelFromSafetyExtractionMenu()
        {
            if (!TryTravel())
                return;

            gameManager manager = gameManager.instance;
            if (manager != null)
                manager.ExtractionMenu(false);
        }

        public bool TryTravel()
        {
            return TryTravelTo(
                destinationSceneName,
                spawnDestinationId,
                requireUnlockedArea,
                requiredArea);
        }

        public bool TryTravelTo(
            string sceneName,
            string destinationId)
        {
            return TryTravelTo(
                sceneName,
                destinationId,
                false,
                CampaignAreaId.BlackPines);
        }

        public bool TryTravelToArea(
            CampaignAreaId area,
            string sceneName,
            string destinationId)
        {
            return TryTravelTo(sceneName, destinationId, true, area);
        }

        public void ConfigureDestination(
            string sceneName,
            string destinationId,
            bool requiresAreaUnlock,
            CampaignAreaId area)
        {
            destinationSceneName = sceneName?.Trim() ?? string.Empty;
            spawnDestinationId = destinationId?.Trim() ?? string.Empty;
            requireUnlockedArea = requiresAreaUnlock;
            requiredArea = area;
        }

        private bool TryTravelTo(
            string sceneName,
            string destinationId,
            bool checkAreaUnlock,
            CampaignAreaId area)
        {
            if (isTraveling)
            {
                Reject(CampaignTravelFailure.AlreadyTraveling);
                return false;
            }

            CampaignStateService stateService =
                CampaignStateService.Instance != null
                    ? CampaignStateService.Instance
                    : campaignState;

            if (stateService == null)
            {
                Reject(CampaignTravelFailure.MissingCampaignState);
                return false;
            }

            string trimmedSceneName = sceneName?.Trim() ?? string.Empty;
            string trimmedDestinationId =
                destinationId?.Trim() ?? string.Empty;

            if (trimmedSceneName.Length == 0 ||
                trimmedDestinationId.Length == 0)
            {
                Reject(CampaignTravelFailure.InvalidSceneOrDestination);
                return false;
            }

            if (checkAreaUnlock && !stateService.IsAreaUnlocked(area))
            {
                Reject(CampaignTravelFailure.AreaLocked);
                return false;
            }

            if (!string.IsNullOrEmpty(stateService.PendingRootOfferingId))
            {
                Reject(CampaignTravelFailure.RootOfferingPending);
                return false;
            }

            if (stateService.HasUnresolvedFarmEmergence)
            {
                Reject(CampaignTravelFailure.FarmEmergencePending);
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(trimmedSceneName))
            {
                Reject(CampaignTravelFailure.SceneNotInBuild);
                return false;
            }

            CampaignInventoryCarryover inventoryCarryover =
                stateService.GetComponent<CampaignInventoryCarryover>();
            if (inventoryCarryover == null)
            {
                Debug.LogError(
                    "Campaign travel requires the persistent campaign inventory carryover authority.",
                    this);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            if (inventoryCarryover.IsRestoreInProgress ||
                inventoryCarryover.HasPendingRestoreFailure)
            {
                Reject(CampaignTravelFailure.InventoryRestorePending);
                return false;
            }

            if (!stateService.TryGetInventoryCarryover(
                    out _,
                    out int[] previousInventoryQuantities))
            {
                Debug.LogError(
                    "Campaign travel requires the normalized pre-travel " +
                    "inventory checkpoint established on scene entry.",
                    this);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            if (!stateService.TryExportCheckpoint(
                    out string previousCampaignCheckpoint,
                    out string checkpointError))
            {
                Debug.LogError(
                    "Campaign travel could not capture its pre-travel " +
                    $"checkpoint: {checkpointError}",
                    this);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            // Safety's public save is synchronized before the campaign
            // transaction. A disk failure therefore cannot leave a newly
            // captured campaign snapshot or pending arrival behind.
            if (!CampaignSafetySaveIntegration.TryBeginCurrentGameSave(
                    out CampaignSafetySaveIntegration.SaveTransaction
                        safetySaveTransaction,
                    out string safetySaveError))
            {
                Debug.LogError(
                    "Campaign travel could not synchronize the Safety " +
                    $"gamesave: {safetySaveError}",
                    this);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            if (!inventoryCarryover.CaptureForTravel())
            {
                RollbackSafetySave(safetySaveTransaction);
                Debug.LogError(
                    "Campaign travel requires a valid inventory carryover snapshot.",
                    this);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            if (!stateService.PrepareSceneTravel(
                    trimmedSceneName,
                    trimmedDestinationId))
            {
                RollbackCampaignInventory(
                    stateService,
                    inventoryCarryover,
                    previousCampaignCheckpoint,
                    previousInventoryQuantities);
                RollbackSafetySave(safetySaveTransaction);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            string pairError = string.Empty;
            if (!stateService.TryGetInventoryCarryover(
                    out string[] travelInventoryIds,
                    out int[] travelInventoryQuantities) ||
                !CampaignSafetySaveIntegration
                    .TryWriteCampaignInventoryCheckpoint(
                        travelInventoryIds,
                        travelInventoryQuantities,
                        out pairError))
            {
                RollbackCampaignInventory(
                    stateService,
                    inventoryCarryover,
                    previousCampaignCheckpoint,
                    previousInventoryQuantities);
                RollbackSafetySave(safetySaveTransaction);
                Debug.LogError(
                    "Campaign travel could not pair the campaign checkpoint " +
                    $"with the Safety save: {pairError}",
                    this);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            if (!CampaignSafetySaveIntegration.TryMarkPendingArrival(
                    trimmedSceneName,
                    out string contextError))
            {
                stateService.CancelPendingTravel(
                    trimmedSceneName,
                    trimmedDestinationId);
                RollbackCampaignInventory(
                    stateService,
                    inventoryCarryover,
                    previousCampaignCheckpoint,
                    previousInventoryQuantities);
                RollbackSafetySave(safetySaveTransaction);
                Debug.LogError(
                    "Campaign travel could not persist the Safety save " +
                    $"arrival context: {contextError}",
                    this);
                Reject(CampaignTravelFailure.SaveFailed);
                return false;
            }

            AsyncOperation loadOperation;

            try
            {
                loadOperation = SceneManager.LoadSceneAsync(
                    trimmedSceneName,
                    LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                bool canceled = stateService.CancelPendingTravel(
                    trimmedSceneName,
                    trimmedDestinationId);
                CampaignSafetySaveIntegration.CancelPendingArrival(
                    trimmedSceneName);
                RollbackCampaignInventory(
                    stateService,
                    inventoryCarryover,
                    previousCampaignCheckpoint,
                    previousInventoryQuantities);
                RollbackSafetySave(safetySaveTransaction);
                Debug.LogError(
                    $"Could not load scene '{trimmedSceneName}': " +
                    exception.Message,
                    this);
                Reject(
                    canceled
                        ? CampaignTravelFailure.SceneLoadFailed
                        : CampaignTravelFailure.SaveFailed);
                return false;
            }

            if (loadOperation == null)
            {
                bool canceled = stateService.CancelPendingTravel(
                    trimmedSceneName,
                    trimmedDestinationId);
                CampaignSafetySaveIntegration.CancelPendingArrival(
                    trimmedSceneName);
                RollbackCampaignInventory(
                    stateService,
                    inventoryCarryover,
                    previousCampaignCheckpoint,
                    previousInventoryQuantities);
                RollbackSafetySave(safetySaveTransaction);
                Reject(
                    canceled
                        ? CampaignTravelFailure.SceneLoadFailed
                        : CampaignTravelFailure.SaveFailed);
                return false;
            }

            safetySaveTransaction.Commit();
            isTraveling = true;
            CampaignEventUtility.Invoke(
                TravelStarted,
                trimmedSceneName,
                trimmedDestinationId,
                this);
            return true;
        }

        private void Reject(CampaignTravelFailure failure)
        {
            CampaignEventUtility.Invoke(TravelRejected, failure, this);
        }

        private void RollbackSafetySave(
            CampaignSafetySaveIntegration.SaveTransaction transaction)
        {
            if (transaction != null &&
                !transaction.TryRollback(out string rollbackError))
            {
                Debug.LogError(
                    "Campaign travel also failed to roll back the Safety " +
                    $"save transaction: {rollbackError}",
                    this);
            }
        }

        private void RollbackCampaignInventory(
            CampaignStateService stateService,
            CampaignInventoryCarryover inventoryCarryover,
            string campaignCheckpoint,
            int[] quantities)
        {
            string rollbackError = string.Empty;
            if (stateService == null || inventoryCarryover == null ||
                !stateService.TryRestoreCheckpoint(
                    campaignCheckpoint,
                    out rollbackError))
            {
                inventoryCarryover?.MarkInventoryRecoveryPending(
                    "Campaign travel could not restore its pre-travel " +
                    "inventory checkpoint.");
                Debug.LogError(
                    "Campaign travel could not restore its pre-travel " +
                    $"inventory checkpoint: {rollbackError}",
                    this);
                return;
            }

            inventoryCarryover.AcceptAreaCompletionSnapshot(quantities);
        }

        private void OnValidate()
        {
            destinationSceneName = destinationSceneName?.Trim() ?? string.Empty;
            spawnDestinationId = spawnDestinationId?.Trim() ?? string.Empty;
        }
    }
}
