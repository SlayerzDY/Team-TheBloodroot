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
                campaignState != null
                    ? campaignState
                    : CampaignStateService.Instance;

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

            if (!Application.CanStreamedLevelBeLoaded(trimmedSceneName))
            {
                Reject(CampaignTravelFailure.SceneNotInBuild);
                return false;
            }

            if (!stateService.PrepareSceneTravel(
                    trimmedSceneName,
                    trimmedDestinationId))
            {
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
                Reject(
                    canceled
                        ? CampaignTravelFailure.SceneLoadFailed
                        : CampaignTravelFailure.SaveFailed);
                return false;
            }

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

        private void OnValidate()
        {
            destinationSceneName = destinationSceneName?.Trim() ?? string.Empty;
            spawnDestinationId = spawnDestinationId?.Trim() ?? string.Empty;
        }
    }
}
