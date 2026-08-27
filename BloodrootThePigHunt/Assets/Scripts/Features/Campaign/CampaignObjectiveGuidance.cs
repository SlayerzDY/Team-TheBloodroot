using System;
using Bloodroot.Features.FarmPrologue;
using Bloodroot.Features.WorldMissions;
using UnityEngine;

namespace Bloodroot.Campaign
{
    [Serializable]
    public sealed class FarmChoreGuidanceTarget
    {
        [SerializeField] private FarmChoreInteractable chore;
        [SerializeField] private Transform markerAnchor;
        [SerializeField] private string displayName;

        public FarmChoreInteractable Chore => chore;
        public Transform MarkerAnchor => markerAnchor;
        public string DisplayName => displayName?.Trim() ?? string.Empty;

        public void Configure(
            FarmChoreInteractable authoredChore,
            Transform authoredMarkerAnchor,
            string authoredDisplayName)
        {
            chore = authoredChore;
            markerAnchor = authoredMarkerAnchor;
            displayName = authoredDisplayName?.Trim() ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class ProgressionTowerGuidanceTarget
    {
        [SerializeField] private CampaignProgressionTower tower;
        [SerializeField] private Transform markerAnchor;
        [SerializeField] private string displayName;

        public CampaignProgressionTower Tower => tower;
        public Transform MarkerAnchor => markerAnchor;
        public string DisplayName => displayName?.Trim() ?? string.Empty;

        public void Configure(
            CampaignProgressionTower authoredTower,
            Transform authoredMarkerAnchor,
            string authoredDisplayName)
        {
            tower = authoredTower;
            markerAnchor = authoredMarkerAnchor;
            displayName = authoredDisplayName?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// Resolves one waypoint from the campaign's existing authoritative state.
    /// Farm chores follow the director's available chore. Open World guidance
    /// follows the active area's progression tower and then the Hollow thorn
    /// veil. This class never advances campaign progress itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignObjectiveGuidance : MonoBehaviour
    {
        [Header("Farm Prologue")]
        [SerializeField] private FarmPrologueDirector farmDirector;
        [SerializeField] private FarmChoreGuidanceTarget[] farmTargets =
            Array.Empty<FarmChoreGuidanceTarget>();

        [Header("Open World")]
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private ProgressionTowerGuidanceTarget[] towerTargets =
            Array.Empty<ProgressionTowerGuidanceTarget>();
        [SerializeField] private HollowWitchSpawner hollowWitchSpawner;
        [SerializeField] private Transform thornVeilMarkerAnchor;
        [SerializeField] private string thornVeilDisplayName = "Thorn veil";

        [Header("Runtime Recovery")]
        [SerializeField, Min(0.05f)] private float refreshIntervalSeconds =
            0.2f;

        private CampaignStateService boundCampaignState;
        private Transform currentTarget;
        private string currentTargetLabel = string.Empty;
        private string currentInstruction = string.Empty;
        private float nextRefreshTime;

        public Transform CurrentTarget => currentTarget;
        public string CurrentTargetLabel => currentTargetLabel;
        public string CurrentInstruction => currentInstruction;
        public bool HasTarget => currentTarget != null;
        public bool IsFarmGuidance => farmDirector != null;
        public bool IsOpenWorldGuidance => farmDirector == null;

        public event Action<CampaignObjectiveGuidance> TargetChanged;

        private void OnEnable()
        {
            BindSources();
            nextRefreshTime = 0f;
            RefreshCurrentTarget();
        }

        private void OnDisable()
        {
            UnbindSources();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            RefreshCurrentTarget();
        }

        private void OnValidate()
        {
            farmTargets ??= Array.Empty<FarmChoreGuidanceTarget>();
            towerTargets ??= Array.Empty<ProgressionTowerGuidanceTarget>();
            refreshIntervalSeconds = Mathf.Max(0.05f, refreshIntervalSeconds);
            thornVeilDisplayName = string.IsNullOrWhiteSpace(
                thornVeilDisplayName)
                ? "Thorn veil"
                : thornVeilDisplayName.Trim();
        }

        public void ConfigureFarm(
            FarmPrologueDirector authoredDirector,
            FarmChoreGuidanceTarget[] authoredTargets)
        {
            bool rebind = Application.isPlaying && isActiveAndEnabled;
            if (rebind)
                UnbindSources();

            farmDirector = authoredDirector;
            farmTargets = authoredTargets ??
                Array.Empty<FarmChoreGuidanceTarget>();
            campaignState = null;
            towerTargets = Array.Empty<ProgressionTowerGuidanceTarget>();
            hollowWitchSpawner = null;
            thornVeilMarkerAnchor = null;

            if (rebind)
            {
                BindSources();
                RefreshCurrentTarget();
            }
        }

        public void ConfigureOpenWorld(
            CampaignStateService authoredCampaignState,
            ProgressionTowerGuidanceTarget[] authoredTowerTargets,
            HollowWitchSpawner authoredHollowWitchSpawner,
            Transform authoredThornVeilMarkerAnchor,
            string authoredThornVeilDisplayName)
        {
            bool rebind = Application.isPlaying && isActiveAndEnabled;
            if (rebind)
                UnbindSources();

            farmDirector = null;
            farmTargets = Array.Empty<FarmChoreGuidanceTarget>();
            campaignState = authoredCampaignState;
            towerTargets = authoredTowerTargets ??
                Array.Empty<ProgressionTowerGuidanceTarget>();
            hollowWitchSpawner = authoredHollowWitchSpawner;
            thornVeilMarkerAnchor = authoredThornVeilMarkerAnchor;
            thornVeilDisplayName = string.IsNullOrWhiteSpace(
                authoredThornVeilDisplayName)
                ? "Thorn veil"
                : authoredThornVeilDisplayName.Trim();

            if (rebind)
            {
                BindSources();
                RefreshCurrentTarget();
            }
        }

        public bool ValidateConfiguration(out string failureReason)
        {
            bool hasFarm = farmDirector != null;
            bool hasOpenWorld = campaignState != null ||
                                towerTargets.Length > 0 ||
                                hollowWitchSpawner != null ||
                                thornVeilMarkerAnchor != null;
            if (hasFarm == hasOpenWorld)
            {
                failureReason =
                    "Objective guidance must author exactly one Farm or Open World mode.";
                return false;
            }

            if (hasFarm)
            {
                if (farmTargets.Length == 0)
                {
                    failureReason =
                        "Farm objective guidance requires authored chore targets.";
                    return false;
                }

                foreach (FarmChoreGuidanceTarget target in farmTargets)
                {
                    if (target == null || target.Chore == null ||
                        target.MarkerAnchor == null ||
                        string.IsNullOrWhiteSpace(target.DisplayName))
                    {
                        failureReason =
                            "Every Farm guidance target requires a chore, marker anchor, and display name.";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;
            }

            if (campaignState == null || towerTargets.Length == 0 ||
                hollowWitchSpawner == null || thornVeilMarkerAnchor == null)
            {
                failureReason =
                    "Open World objective guidance requires campaign state, towers, the Hollow spawner, and a thorn-veil anchor.";
                return false;
            }

            foreach (ProgressionTowerGuidanceTarget target in towerTargets)
            {
                if (target == null || target.Tower == null ||
                    target.MarkerAnchor == null ||
                    string.IsNullOrWhiteSpace(target.DisplayName))
                {
                    failureReason =
                        "Every Open World guidance target requires a tower, marker anchor, and display name.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        public void RefreshCurrentTarget()
        {
            if (farmDirector != null)
            {
                RefreshFarmTarget();
                return;
            }

            RefreshOpenWorldTarget();
        }

        private void RefreshFarmTarget()
        {
            if (farmDirector.CurrentPhase != FarmProloguePhase.Chores)
            {
                SetTarget(null, string.Empty, string.Empty);
                return;
            }

            FarmChoreGuidanceTarget fallback = null;
            foreach (FarmChoreGuidanceTarget target in farmTargets)
            {
                if (target?.Chore == null || target.Chore.IsComplete)
                    continue;

                fallback ??= target;
                if (target.Chore.IsAvailable)
                {
                    SetFarmTarget(target);
                    return;
                }
            }

            if (fallback != null)
            {
                // The chore director changes progress and availability in the
                // same frame. This fallback prevents a one-frame marker blink.
                SetFarmTarget(fallback);
                return;
            }

            SetTarget(null, string.Empty, string.Empty);
        }

        private void SetFarmTarget(FarmChoreGuidanceTarget target)
        {
            SetTarget(
                target.MarkerAnchor,
                target.DisplayName,
                target.Chore.ObjectiveText);
        }

        private void RefreshOpenWorldTarget()
        {
            ResolveCampaignState();

            foreach (ProgressionTowerGuidanceTarget target in towerTargets)
            {
                CampaignProgressionTower tower = target?.Tower;
                if (tower == null || !tower.gameObject.activeInHierarchy ||
                    tower.IsActivated)
                {
                    continue;
                }

                bool objectiveIsCurrent = tower.Objective != null &&
                    tower.Objective.IsAvailable &&
                    tower.Objective.Director != null &&
                    tower.Objective.Director.CurrentObjective ==
                    tower.Objective;
                if (!objectiveIsCurrent)
                    continue;

                string instruction = tower.Objective != null
                    ? tower.Objective.ObjectiveText
                    : "Reach and activate the progression tower.";
                SetTarget(
                    target.MarkerAnchor,
                    target.DisplayName,
                    instruction);
                return;
            }

            WorldMissionInteractionObjective crossingObjective =
                hollowWitchSpawner != null
                    ? hollowWitchSpawner.CrossingObjective
                    : null;
            bool crossingIsCurrent = crossingObjective != null &&
                crossingObjective.IsAvailable &&
                crossingObjective.Director != null &&
                crossingObjective.Director.CurrentObjective ==
                crossingObjective;

            if (crossingIsCurrent && thornVeilMarkerAnchor != null)
            {
                SetTarget(
                    thornVeilMarkerAnchor,
                    thornVeilDisplayName,
                    crossingObjective != null
                        ? crossingObjective.ObjectiveText
                        : "Follow the marker and cross the thorn veil.");
                return;
            }

            SetTarget(null, string.Empty, string.Empty);
        }

        private CampaignStateService ResolveCampaignState()
        {
            CampaignStateService resolved = CampaignStateService.Instance != null
                ? CampaignStateService.Instance
                : campaignState;
            if (resolved != campaignState)
                campaignState = resolved;

            if (boundCampaignState != resolved && isActiveAndEnabled)
            {
                BindCampaignState(resolved);
            }

            return resolved;
        }

        private void SetTarget(
            Transform nextTarget,
            string nextLabel,
            string nextInstruction)
        {
            string safeLabel = nextLabel?.Trim() ?? string.Empty;
            string safeInstruction = nextInstruction?.Trim() ?? string.Empty;
            if (currentTarget == nextTarget &&
                string.Equals(currentTargetLabel, safeLabel,
                    StringComparison.Ordinal) &&
                string.Equals(currentInstruction, safeInstruction,
                    StringComparison.Ordinal))
            {
                return;
            }

            currentTarget = nextTarget;
            currentTargetLabel = safeLabel;
            currentInstruction = safeInstruction;
            TargetChanged?.Invoke(this);
        }

        private void BindSources()
        {
            if (farmDirector != null)
            {
                farmDirector.PhaseChanged += HandleFarmPhaseChanged;
                farmDirector.ObjectiveProgressChanged +=
                    HandleFarmObjectiveProgressChanged;
            }

            foreach (FarmChoreGuidanceTarget target in farmTargets)
            {
                if (target?.Chore != null)
                    target.Chore.ProgressChanged += HandleFarmChoreProgressChanged;
            }

            BindCampaignState(ResolveCampaignStateWithoutRebind());

            foreach (ProgressionTowerGuidanceTarget target in towerTargets)
            {
                if (target?.Tower?.Objective != null)
                    target.Tower.Objective.StateChanged +=
                        HandleWorldObjectiveStateChanged;
            }

            if (hollowWitchSpawner?.CrossingObjective != null)
            {
                hollowWitchSpawner.CrossingObjective.StateChanged +=
                    HandleWorldObjectiveStateChanged;
            }
        }

        private void UnbindSources()
        {
            if (farmDirector != null)
            {
                farmDirector.PhaseChanged -= HandleFarmPhaseChanged;
                farmDirector.ObjectiveProgressChanged -=
                    HandleFarmObjectiveProgressChanged;
            }

            foreach (FarmChoreGuidanceTarget target in farmTargets)
            {
                if (target?.Chore != null)
                    target.Chore.ProgressChanged -= HandleFarmChoreProgressChanged;
            }

            foreach (ProgressionTowerGuidanceTarget target in towerTargets)
            {
                if (target?.Tower?.Objective != null)
                    target.Tower.Objective.StateChanged -=
                        HandleWorldObjectiveStateChanged;
            }

            if (hollowWitchSpawner?.CrossingObjective != null)
            {
                hollowWitchSpawner.CrossingObjective.StateChanged -=
                    HandleWorldObjectiveStateChanged;
            }

            BindCampaignState(null);
        }

        private CampaignStateService ResolveCampaignStateWithoutRebind()
        {
            return CampaignStateService.Instance != null
                ? CampaignStateService.Instance
                : campaignState;
        }

        private void BindCampaignState(CampaignStateService nextState)
        {
            if (boundCampaignState == nextState)
                return;

            if (boundCampaignState != null)
            {
                boundCampaignState.ProgressLoaded -= HandleCampaignProgress;
                boundCampaignState.ProgressChanged -= HandleCampaignProgress;
            }

            boundCampaignState = nextState;
            if (boundCampaignState != null)
            {
                boundCampaignState.ProgressLoaded += HandleCampaignProgress;
                boundCampaignState.ProgressChanged += HandleCampaignProgress;
            }
        }

        private void HandleFarmPhaseChanged(FarmProloguePhase _)
        {
            RefreshCurrentTarget();
        }

        private void HandleFarmObjectiveProgressChanged(
            string _,
            int __,
            int ___)
        {
            RefreshCurrentTarget();
        }

        private void HandleFarmChoreProgressChanged(
            string _,
            int __,
            int ___)
        {
            RefreshCurrentTarget();
        }

        private void HandleWorldObjectiveStateChanged(WorldMissionObjective _)
        {
            RefreshCurrentTarget();
        }

        private void HandleCampaignProgress(CampaignProgressSnapshot _)
        {
            RefreshCurrentTarget();
        }
    }
}
