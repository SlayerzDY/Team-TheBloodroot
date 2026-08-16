using System;
using Bloodroot.Campaign;
using TMPro;
using UnityEngine;

namespace Bloodroot.Features.Hub
{
    [Serializable]
    public sealed class HubAreaBoardEntry
    {
        [SerializeField] private CampaignAreaId area;
        [SerializeField] private string displayName = "Area";
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject lockedStateRoot;
        [SerializeField] private GameObject availableStateRoot;
        [SerializeField] private GameObject completedStateRoot;

        public CampaignAreaId Area => area;

        public void Configure(
            CampaignAreaId areaId,
            string label,
            TMP_Text authoredStatusText,
            GameObject lockedRoot,
            GameObject availableRoot,
            GameObject completedRoot)
        {
            area = areaId;
            displayName = string.IsNullOrWhiteSpace(label)
                ? area.ToString()
                : label.Trim();
            statusText = authoredStatusText;
            lockedStateRoot = lockedRoot;
            availableStateRoot = availableRoot;
            completedStateRoot = completedRoot;
        }

        internal void Apply(CampaignProgressSnapshot snapshot)
        {
            bool completed = snapshot.IsAreaCompleted(area);
            bool unlocked = snapshot.IsAreaUnlocked(area);
            string status = completed
                ? "COMPLETE"
                : unlocked
                    ? "AVAILABLE"
                    : "LOCKED";

            if (statusText != null)
            {
                statusText.text = $"{displayName}: {status}";
            }

            SetActive(lockedStateRoot, !unlocked && !completed);
            SetActive(availableStateRoot, unlocked && !completed);
            SetActive(completedStateRoot, completed);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }

    /// <summary>
    /// Binds campaign state to authored investigation-board text and markers.
    /// Placeholder marker meshes can be replaced independently of this code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubInvestigationBoard : MonoBehaviour
    {
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private HubAreaBoardEntry[] areaEntries =
            Array.Empty<HubAreaBoardEntry>();

        private CampaignStateService boundState;

        public void Configure(
            CampaignStateService state,
            TMP_Text authoredSummary,
            HubAreaBoardEntry[] entries)
        {
            Unbind();
            campaignState = state;
            summaryText = authoredSummary;
            areaEntries = entries ?? Array.Empty<HubAreaBoardEntry>();

            if (isActiveAndEnabled)
            {
                ResolveState();
                Bind();
                Refresh();
            }
        }

        private void OnEnable()
        {
            ResolveState();
            Bind();
            Refresh();
        }

        private void Start()
        {
            ResolveState();
            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnValidate()
        {
            areaEntries ??= Array.Empty<HubAreaBoardEntry>();
        }

        public void Refresh()
        {
            ResolveState();
            CampaignProgressSnapshot snapshot = campaignState != null
                ? campaignState.Current
                : default;

            int completed = 0;
            foreach (CampaignAreaId area in
                     Enum.GetValues(typeof(CampaignAreaId)))
            {
                if (snapshot.IsAreaCompleted(area))
                {
                    completed++;
                }
            }

            if (summaryText != null)
            {
                summaryText.text = snapshot.PrologueCompleted
                    ? $"Heartroot Investigation: {completed}/4 regions cleared"
                    : "Complete the Farm prologue to begin the investigation.";
            }

            if (areaEntries == null)
                return;

            foreach (HubAreaBoardEntry entry in areaEntries)
            {
                entry?.Apply(snapshot);
            }
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

            if (boundState != null)
            {
                boundState.ProgressLoaded += HandleProgress;
                boundState.ProgressChanged += HandleProgress;
            }
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
            Refresh();
        }
    }
}
