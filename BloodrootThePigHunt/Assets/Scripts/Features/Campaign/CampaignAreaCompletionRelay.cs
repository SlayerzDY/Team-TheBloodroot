using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Explicit, inspector-friendly bridge from an authored mission-complete
    /// event to the campaign progression authority. This component deliberately
    /// has no trigger, timer, or enemy-count logic; the owning mission decides
    /// when it is appropriate to call CompleteArea.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignAreaCompletionRelay : MonoBehaviour
    {
        [SerializeField]
        private CampaignOpenWorldProgression progression;

        [SerializeField]
        private CampaignAreaId area;

        [Header("Authored Callbacks")]
        [SerializeField]
        private UnityEvent areaCompleted = new();

        [SerializeField]
        private UnityEvent completionRejected = new();

        /// <summary>
        /// Raised only when this call changes the saved campaign state.
        /// Repeating a completed call is a successful no-op and does not
        /// invoke this callback again.
        /// </summary>
        public event Action<CampaignAreaId> AreaCompleted;

        /// <summary>
        /// Raised when the configured area cannot currently be completed.
        /// For example, its prerequisite may still be locked or the
        /// progression reference may be missing.
        /// </summary>
        public event Action<CampaignAreaId> CompletionRejected;

        public CampaignOpenWorldProgression Progression => progression;

        public CampaignAreaId Area => area;

        /// <summary>
        /// UnityEvent-friendly completion entry point.
        /// </summary>
        public void CompleteArea()
        {
            TryCompleteArea();
        }

        /// <summary>
        /// Attempts to mark the configured area complete. The operation is
        /// idempotent: a previously saved completion returns true without
        /// writing the save or firing completion callbacks a second time.
        /// </summary>
        public bool TryCompleteArea()
        {
            if (!Enum.IsDefined(typeof(CampaignAreaId), area) ||
                progression == null)
            {
                Reject();
                return false;
            }

            CampaignStateService state = CampaignStateService.Instance;

            if (state != null && state.IsAreaCompleted(area))
            {
                return true;
            }

            CampaignInventoryCarryover carryover = state != null
                ? state.GetComponent<CampaignInventoryCarryover>()
                : null;
            string[] capturedItemIds = Array.Empty<string>();
            int[] capturedQuantities = Array.Empty<int>();
            if (state == null || carryover == null ||
                !carryover.TryCaptureForAreaCompletion(
                    out capturedItemIds,
                    out capturedQuantities) ||
                !state.TryStageAreaCompletionInventory(
                    capturedItemIds,
                    capturedQuantities))
            {
                Reject();
                return false;
            }

            bool completed;
            try
            {
                completed = progression.TryCompleteArea(area);
            }
            finally
            {
                state.ClearStagedAreaCompletionInventory();
            }

            if (!completed)
            {
                Reject();
                return false;
            }

            carryover.AcceptAreaCompletionSnapshot(capturedQuantities);

            InvokeAuthoredCallback(areaCompleted);
            CampaignEventUtility.Invoke(AreaCompleted, area, this);
            return true;
        }

        /// <summary>
        /// Configuration API for editor tooling. The relay performs no work
        /// until a mission explicitly calls CompleteArea or TryCompleteArea.
        /// </summary>
        public void Configure(
            CampaignOpenWorldProgression campaignProgression,
            CampaignAreaId campaignArea)
        {
            progression = campaignProgression;
            area = campaignArea;
        }

        private void Reject()
        {
            InvokeAuthoredCallback(completionRejected);
            CampaignEventUtility.Invoke(CompletionRejected, area, this);
        }

        private void InvokeAuthoredCallback(UnityEvent callback)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {

            }
        }
    }
}
