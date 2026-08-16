using System;
using System.Collections.Generic;
using Bloodroot.Campaign;
using Bloodroot.Features.FarmPrologue;
using UnityEngine;

namespace Bloodroot.Features.Hub
{
    [Serializable]
    public sealed class HubStationUnlockRule
    {
        [SerializeField] private HubStationId station;
        [SerializeField] private GameObject stationRoot;
        [SerializeField] private bool requireCompletedPrologue = true;
        [SerializeField, Range(0, 4)] private int completedAreasRequired;

        public HubStationId Station => station;
        public GameObject StationRoot => stationRoot;
        public bool RequireCompletedPrologue => requireCompletedPrologue;
        public int CompletedAreasRequired =>
            Mathf.Clamp(completedAreasRequired, 0, 4);

        public void Configure(
            HubStationId id,
            GameObject root,
            int completedAreaCount,
            bool requirePrologue = true)
        {
            station = id;
            stationRoot = root;
            completedAreasRequired =
                Mathf.Clamp(completedAreaCount, 0, 4);
            requireCompletedPrologue = requirePrologue;
        }
    }

    /// <summary>
    /// Applies campaign progress to authored hub station roots. This component
    /// belongs on the always-present Hub state root, never on a station root it
    /// may deactivate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubStationProgression : MonoBehaviour
    {
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private FarmPrologueDirector prologueDirector;
        [SerializeField] private HubStationUnlockRule[] stationRules =
            Array.Empty<HubStationUnlockRule>();
        [SerializeField] private HubStationStateUnityEvent stationStateChanged =
            new();

        private readonly Dictionary<HubStationId, bool> states = new();
        private CampaignStateService boundState;
        private FarmPrologueDirector boundDirector;

        public IReadOnlyDictionary<HubStationId, bool> StationStates => states;

        public event Action<HubStationId, bool> StationStateChanged;

        private void OnEnable()
        {
            ResolveReferences();
            BindEvents();
            ApplyCurrentProgress();
        }

        private void Start()
        {
            ResolveReferences();
            BindEvents();
            ApplyCurrentProgress();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void OnValidate()
        {
            stationRules ??= Array.Empty<HubStationUnlockRule>();
        }

        public bool IsStationUnlocked(HubStationId station)
        {
            return states.TryGetValue(station, out bool unlocked) && unlocked;
        }

        public void ApplyCurrentProgress()
        {
            ResolveReferences();

            CampaignProgressSnapshot snapshot = campaignState != null
                ? campaignState.Current
                : default;

            HashSet<HubStationId> uniqueStations = new();
            foreach (HubStationUnlockRule rule in stationRules)
            {
                if (rule == null || !uniqueStations.Add(rule.Station))
                {
                    continue;
                }

                bool unlocked = Evaluate(rule, snapshot);
                bool changed = !states.TryGetValue(
                    rule.Station,
                    out bool previous) || previous != unlocked;

                states[rule.Station] = unlocked;

                if (rule.StationRoot != null &&
                    rule.StationRoot.activeSelf != unlocked)
                {
                    rule.StationRoot.SetActive(unlocked);
                }

                if (changed)
                {
                    HubEventUtility.Invoke(
                        StationStateChanged,
                        rule.Station,
                        unlocked,
                        this);
                    HubEventUtility.Invoke(
                        stationStateChanged,
                        rule.Station,
                        unlocked,
                        this);
                }
            }
        }

        public void Configure(
            CampaignStateService state,
            FarmPrologueDirector director,
            HubStationUnlockRule[] rules)
        {
            UnbindEvents();
            campaignState = state;
            prologueDirector = director;
            stationRules = rules ?? Array.Empty<HubStationUnlockRule>();
            states.Clear();

            if (isActiveAndEnabled)
            {
                ResolveReferences();
                BindEvents();
                ApplyCurrentProgress();
            }
        }

        private static bool Evaluate(
            HubStationUnlockRule rule,
            CampaignProgressSnapshot snapshot)
        {
            if (rule.RequireCompletedPrologue &&
                !snapshot.PrologueCompleted)
            {
                return false;
            }

            return CountCompletedAreas(snapshot) >=
                   rule.CompletedAreasRequired;
        }

        private static int CountCompletedAreas(
            CampaignProgressSnapshot snapshot)
        {
            int completed = 0;

            foreach (CampaignAreaId area in
                     Enum.GetValues(typeof(CampaignAreaId)))
            {
                if (snapshot.IsAreaCompleted(area))
                {
                    completed++;
                }
            }

            return completed;
        }

        private void ResolveReferences()
        {
            if (CampaignStateService.Instance != null)
            {
                campaignState = CampaignStateService.Instance;
            }
        }

        private void BindEvents()
        {
            if (boundState != campaignState)
            {
                if (boundState != null)
                {
                    boundState.ProgressLoaded -= HandleProgress;
                    boundState.ProgressChanged -= HandleProgress;
                }

                boundState = campaignState;

                if (boundState != null)
                {
                    boundState.ProgressLoaded += HandleProgress;
                    boundState.ProgressChanged += HandleProgress;
                }
            }

            if (boundDirector != prologueDirector)
            {
                if (boundDirector != null)
                {
                    boundDirector.HubUnlocked -= HandleHubUnlocked;
                }

                boundDirector = prologueDirector;

                if (boundDirector != null)
                {
                    boundDirector.HubUnlocked += HandleHubUnlocked;
                }
            }
        }

        private void UnbindEvents()
        {
            if (boundState != null)
            {
                boundState.ProgressLoaded -= HandleProgress;
                boundState.ProgressChanged -= HandleProgress;
                boundState = null;
            }

            if (boundDirector != null)
            {
                boundDirector.HubUnlocked -= HandleHubUnlocked;
                boundDirector = null;
            }
        }

        private void HandleProgress(CampaignProgressSnapshot _)
        {
            ApplyCurrentProgress();
        }

        private void HandleHubUnlocked()
        {
            ApplyCurrentProgress();
        }
    }
}
