using System;
using Bloodroot.Campaign;
using TMPro;
using UnityEngine;

namespace Bloodroot.Features.Hub
{
    /// <summary>
    /// Converts campaign state into authored world-space station feedback.
    /// It is a presentation hook only and never creates UI at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubStationStatusPresenter : MonoBehaviour
    {
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private HubStationId station;
        [SerializeField] private HubInvestigationBoard investigationBoard;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private HubStringUnityEvent statusShown = new();

        private CampaignStateService boundState;

        public event Action<string> StatusShown;

        public void Configure(
            CampaignStateService state,
            HubStationId stationId,
            HubInvestigationBoard board,
            TMP_Text authoredStatusText)
        {
            Unbind();
            campaignState = state;
            station = stationId;
            investigationBoard = board;
            statusText = authoredStatusText;

            if (isActiveAndEnabled)
            {
                ResolveState();
                Bind();
                RefreshStatus(false);
            }
        }

        private void OnEnable()
        {
            ResolveState();
            Bind();
            RefreshStatus(false);
        }

        private void Start()
        {
            ResolveState();
            Bind();
            RefreshStatus(false);
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>
        /// Parameterless UnityEvent target for an authored station collider.
        /// </summary>
        public void ShowStatus()
        {
            RefreshStatus(true);
        }

        public void RefreshStatus()
        {
            RefreshStatus(false);
        }

        private void RefreshStatus(bool publish)
        {
            ResolveState();
            CampaignProgressSnapshot snapshot = campaignState != null
                ? campaignState.Current
                : default;

            if (station == HubStationId.MissionBoard ||
                station == HubStationId.Investigation)
            {
                investigationBoard?.Refresh();
            }

            string message = BuildStatus(snapshot);
            if (statusText != null)
            {
                statusText.text = message;
            }

            if (!publish)
                return;

            HubEventUtility.Invoke(StatusShown, message, this);
            HubEventUtility.Invoke(statusShown, message, this);
        }

        private string BuildStatus(CampaignProgressSnapshot snapshot)
        {
            int completed = CountCompletedAreas(snapshot);

            return station switch
            {
                HubStationId.MissionBoard => BuildMissionStatus(snapshot),
                HubStationId.Storage =>
                    "STORAGE READY\nAUTHORING HOOK AVAILABLE",
                HubStationId.Upgrade =>
                    $"UPGRADES AVAILABLE\n{completed}/4 REGIONS CLEARED",
                HubStationId.Investigation =>
                    $"INVESTIGATION UPDATED\n{completed}/4 REGIONS CLEARED",
                HubStationId.Loadout => "USE LOADOUT CONTROLS",
                _ => "STATION READY"
            };
        }

        private static string BuildMissionStatus(
            CampaignProgressSnapshot snapshot)
        {
            if (!snapshot.PrologueCompleted)
            {
                return "MISSION BOARD LOCKED\nCOMPLETE THE FARM PROLOGUE";
            }

            foreach (CampaignAreaId area in
                     Enum.GetValues(typeof(CampaignAreaId)))
            {
                if (snapshot.IsAreaUnlocked(area) &&
                    !snapshot.IsAreaCompleted(area))
                {
                    return $"NEXT: {GetAreaLabel(area)}\n" +
                           "USE THE TRUCK TO DEPLOY";
                }
            }

            return "ALL INVESTIGATIONS COMPLETE";
        }

        private static int CountCompletedAreas(
            CampaignProgressSnapshot snapshot)
        {
            int completed = 0;
            foreach (CampaignAreaId area in
                     Enum.GetValues(typeof(CampaignAreaId)))
            {
                if (snapshot.IsAreaCompleted(area))
                    completed++;
            }

            return completed;
        }

        private static string GetAreaLabel(CampaignAreaId area)
        {
            return area switch
            {
                CampaignAreaId.BlackPines => "BLACK PINES",
                CampaignAreaId.StillwaterFeedMill => "STILLWATER",
                CampaignAreaId.HarrowEstate => "HARROW ESTATE",
                CampaignAreaId.BloodrootHollow => "BLOODROOT HOLLOW",
                _ => "UNKNOWN AREA"
            };
        }

        private void ResolveState()
        {
            if (CampaignStateService.Instance != null)
            {
                campaignState = CampaignStateService.Instance;
            }
        }

        private void Bind()
        {
            if (boundState == campaignState)
                return;

            Unbind();
            boundState = campaignState;
            if (boundState == null)
                return;

            boundState.ProgressLoaded += HandleProgress;
            boundState.ProgressChanged += HandleProgress;
        }

        private void Unbind()
        {
            if (boundState == null)
                return;

            boundState.ProgressLoaded -= HandleProgress;
            boundState.ProgressChanged -= HandleProgress;
            boundState = null;
        }

        private void HandleProgress(CampaignProgressSnapshot _)
        {
            RefreshStatus(false);
        }
    }
}
