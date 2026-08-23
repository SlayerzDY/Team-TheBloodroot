using System;
using System.Collections.Generic;
using Bloodroot.OpenWorld;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Applies saved unlock state to explicitly authored open-world barriers.
    /// It never searches the scene for barriers and keeps every referenced
    /// barrier locked when the authored set is incomplete or ambiguous.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignOpenWorldProgression : MonoBehaviour
    {
        private static readonly OpenWorldAreaId[] RequiredBarrierIds =
        {
            OpenWorldAreaId.StillwaterFeedMill,
            OpenWorldAreaId.HarrowEstate,
            OpenWorldAreaId.BloodrootHollow
        };

        private static readonly CampaignAreaId[] MissionAreaOrder =
        {
            CampaignAreaId.BlackPines,
            CampaignAreaId.StillwaterFeedMill,
            CampaignAreaId.HarrowEstate,
            CampaignAreaId.BloodrootHollow
        };

        [SerializeField]
        private CampaignStateService campaignState;

        [SerializeField]
        private OpenWorldAreaBarrier[] areaBarriers =
            Array.Empty<OpenWorldAreaBarrier>();

        [SerializeField]
        private GameObject[] areaMissionRoots = Array.Empty<GameObject>();

        private readonly Dictionary<OpenWorldAreaId, OpenWorldAreaBarrier>
            barrierById = new();

        private CampaignStateService subscribedState;
        private bool hasValidBarrierSet;
        private bool hasValidMissionRootSet;
        private bool configurationErrorLogged;
        private Transform guardedPlayer;
        private CharacterController guardedCharacterController;
        private float nextBoundaryCorrectionWarningTime;

        public bool HasValidBarrierSet => hasValidBarrierSet;
        public bool HasValidMissionRootSet => hasValidMissionRootSet;

        public IReadOnlyList<GameObject> AreaMissionRoots =>
            areaMissionRoots ?? Array.Empty<GameObject>();

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            // Start runs after every barrier's Awake, so saved state wins over
            // the barrier's authored startsUnlocked default.
            TrySubscribe();
            RebuildBarrierMap();
            ApplyCurrentProgress();
        }

        private void OnDisable()
        {
            Unsubscribe();
            guardedPlayer = null;
            guardedCharacterController = null;
        }

        private void LateUpdate()
        {
            bool ready = EnsureReady();
            CampaignProgressSnapshot progress = ready
                ? subscribedState.Current
                : default;

            EnforceBarrierIntegrity(progress, !ready);
            ConstrainPlayerToLockedAreas(progress, !ready);
        }

        public void CompleteArea(CampaignAreaId area)
        {
            TryCompleteArea(area);
        }

        public bool TryCompleteArea(CampaignAreaId area)
        {
            if (!EnsureReady())
            {
                return false;
            }

            return subscribedState.MarkAreaCompleted(area);
        }

        public void ApplyCurrentProgress()
        {
            if (!EnsureReady())
            {
                LockAllReferencedBarriers();
                SetAllMissionRootsActive(false);
                return;
            }

            CampaignProgressSnapshot progress = subscribedState.Current;

            foreach (KeyValuePair<OpenWorldAreaId, OpenWorldAreaBarrier> pair
                     in barrierById)
            {
                pair.Value.SetUnlocked(
                    progress.IsAreaUnlocked(ToCampaignArea(pair.Key)));
            }

            ApplyMissionRootState(progress);
        }

        public void Configure(
            CampaignStateService stateService,
            OpenWorldAreaBarrier[] barriers,
            GameObject[] missionRoots)
        {
            Unsubscribe();
            campaignState = stateService;
            areaBarriers = barriers ?? Array.Empty<OpenWorldAreaBarrier>();
            areaMissionRoots = missionRoots ?? Array.Empty<GameObject>();
            configurationErrorLogged = false;
            TrySubscribe();
            RebuildBarrierMap();
            ApplyCurrentProgress();
        }

        private bool EnsureReady()
        {
            TrySubscribe();

            if (!hasValidBarrierSet || !hasValidMissionRootSet)
            {
                RebuildBarrierMap();
            }

            return subscribedState != null &&
                   hasValidBarrierSet &&
                   hasValidMissionRootSet;
        }

        private void TrySubscribe()
        {
            CampaignStateService targetState =
                CampaignStateService.Instance != null
                    ? CampaignStateService.Instance
                    : campaignState;

            if (targetState == subscribedState)
            {
                return;
            }

            Unsubscribe();
            subscribedState = targetState;

            if (subscribedState == null)
            {
                return;
            }

            subscribedState.ProgressChanged += HandleProgressChanged;
            subscribedState.AreaUnlocked += HandleAreaUnlocked;
        }

        private void Unsubscribe()
        {
            if (subscribedState != null)
            {
                subscribedState.ProgressChanged -= HandleProgressChanged;
                subscribedState.AreaUnlocked -= HandleAreaUnlocked;
            }

            subscribedState = null;
        }

        private void RebuildBarrierMap()
        {
            barrierById.Clear();
            hasValidBarrierSet = true;
            hasValidMissionRootSet = HasExactMissionRootSet();

            OpenWorldAreaBarrier[] authoredBarriers =
                areaBarriers ?? Array.Empty<OpenWorldAreaBarrier>();

            if (authoredBarriers.Length != RequiredBarrierIds.Length)
            {
                hasValidBarrierSet = false;
            }

            foreach (OpenWorldAreaBarrier barrier in authoredBarriers)
            {
                if (barrier == null)
                {
                    hasValidBarrierSet = false;
                    continue;
                }

                if (Array.IndexOf(RequiredBarrierIds, barrier.Area) < 0)
                {
                    hasValidBarrierSet = false;
                    continue;
                }

                if (barrierById.ContainsKey(barrier.Area))
                {
                    hasValidBarrierSet = false;
                    continue;
                }

                barrierById.Add(barrier.Area, barrier);
            }

            foreach (OpenWorldAreaId requiredId in RequiredBarrierIds)
            {
                if (!barrierById.ContainsKey(requiredId))
                {
                    hasValidBarrierSet = false;
                }
            }

            if (!hasValidBarrierSet || !hasValidMissionRootSet)
            {
                LockAllReferencedBarriers();
                SetAllMissionRootsActive(false);

                if (!configurationErrorLogged)
                {
                    configurationErrorLogged = true;

                }
            }
            else
            {
                configurationErrorLogged = false;
            }
        }

        private void LockAllReferencedBarriers()
        {
            OpenWorldAreaBarrier[] authoredBarriers =
                areaBarriers ?? Array.Empty<OpenWorldAreaBarrier>();

            foreach (OpenWorldAreaBarrier barrier in authoredBarriers)
            {
                if (barrier != null)
                {
                    barrier.SetUnlocked(false);
                }
            }
        }

        private void EnforceBarrierIntegrity(
            CampaignProgressSnapshot progress,
            bool lockAll)
        {
            OpenWorldAreaBarrier[] authoredBarriers =
                areaBarriers ?? Array.Empty<OpenWorldAreaBarrier>();

            foreach (OpenWorldAreaBarrier barrier in authoredBarriers)
            {
                if (barrier == null)
                {
                    continue;
                }

                bool shouldBeUnlocked = !lockAll &&
                    progress.IsAreaUnlocked(ToCampaignArea(barrier.Area));

                if (!barrier.MatchesUnlockedState(shouldBeUnlocked))
                {
                    barrier.SetUnlocked(shouldBeUnlocked);
                }
            }
        }

        private void ConstrainPlayerToLockedAreas(
            CampaignProgressSnapshot progress,
            bool lockAll)
        {
            if (!TryResolveGuardedPlayer())
            {
                return;
            }

            Vector3 currentPosition = guardedPlayer.position;
            Vector3 constrainedPosition = currentPosition;
            OpenWorldAreaId constrainedArea = default;
            bool wasConstrained = false;
            float clearance = ResolvePlayerBoundaryClearance();

            foreach (OpenWorldAreaId barrierId in RequiredBarrierIds)
            {
                if (!barrierById.TryGetValue(
                        barrierId,
                        out OpenWorldAreaBarrier barrier))
                {
                    continue;
                }

                bool shouldBeUnlocked = !lockAll &&
                    progress.IsAreaUnlocked(ToCampaignArea(barrierId));

                if (shouldBeUnlocked ||
                    !barrier.TryConstrainToUnlockedSide(
                        constrainedPosition,
                        clearance,
                        out Vector3 correctedPosition))
                {
                    continue;
                }

                constrainedPosition = correctedPosition;
                constrainedArea = barrierId;
                wasConstrained = true;
            }

            if (!wasConstrained ||
                (constrainedPosition - currentPosition).sqrMagnitude <=
                0.000001f)
            {
                return;
            }

            bool restoreController =
                guardedCharacterController != null &&
                guardedCharacterController.enabled;

            if (restoreController)
            {
                guardedCharacterController.enabled = false;
            }

            guardedPlayer.position = constrainedPosition;
            Physics.SyncTransforms();

            if (restoreController && guardedCharacterController != null)
            {
                guardedCharacterController.enabled = true;
            }

            if (Time.unscaledTime >= nextBoundaryCorrectionWarningTime)
            {
                nextBoundaryCorrectionWarningTime = Time.unscaledTime + 2f;

            }
        }

        private bool TryResolveGuardedPlayer()
        {
            GameObject candidate = gameManager.instance != null
                ? gameManager.instance.player
                : null;

            if (candidate == null)
            {
                try
                {
                    candidate = GameObject.FindGameObjectWithTag("Player");
                }
                catch (UnityException)
                {
                    return false;
                }
            }

            if (candidate == null || !candidate.activeInHierarchy)
            {
                guardedPlayer = null;
                guardedCharacterController = null;
                return false;
            }

            if (guardedPlayer != candidate.transform)
            {
                guardedPlayer = candidate.transform;
                guardedCharacterController =
                    candidate.GetComponent<CharacterController>();
            }

            return true;
        }

        private float ResolvePlayerBoundaryClearance()
        {
            const float minimumClearance = 0.05f;

            if (guardedCharacterController == null)
            {
                return 0.6f;
            }

            Vector3 scale = guardedCharacterController.transform.lossyScale;
            float horizontalScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.z));

            return guardedCharacterController.radius * horizontalScale +
                   guardedCharacterController.skinWidth +
                   minimumClearance;
        }

        private void HandleProgressChanged(
            CampaignProgressSnapshot progress)
        {
            ApplyProgress(progress);
        }

        private void HandleAreaUnlocked(CampaignAreaId area)
        {
            if (!hasValidBarrierSet ||
                !hasValidMissionRootSet ||
                !TryGetBarrierArea(area, out OpenWorldAreaId barrierArea) ||
                !barrierById.TryGetValue(
                    barrierArea,
                    out OpenWorldAreaBarrier barrier))
            {
                return;
            }

            barrier.SetUnlocked(true);
        }

        private void ApplyProgress(CampaignProgressSnapshot progress)
        {
            if (!hasValidBarrierSet || !hasValidMissionRootSet)
            {
                LockAllReferencedBarriers();
                SetAllMissionRootsActive(false);
                return;
            }

            foreach (KeyValuePair<OpenWorldAreaId, OpenWorldAreaBarrier> pair
                     in barrierById)
            {
                pair.Value.SetUnlocked(
                    progress.IsAreaUnlocked(ToCampaignArea(pair.Key)));
            }

            ApplyMissionRootState(progress);
        }

        private void ApplyMissionRootState(CampaignProgressSnapshot progress)
        {
            GameObject[] roots = areaMissionRoots ?? Array.Empty<GameObject>();

            if (roots.Length != MissionAreaOrder.Length ||
                Array.Exists(roots, root => root == null))
            {
                SetAllMissionRootsActive(false);
                return;
            }

            int activeIndex = -1;

            for (int index = 0; index < MissionAreaOrder.Length; index++)
            {
                CampaignAreaId area = MissionAreaOrder[index];

                if (progress.IsAreaUnlocked(area) &&
                    !progress.IsAreaCompleted(area))
                {
                    activeIndex = index;
                    break;
                }
            }

            for (int index = 0; index < roots.Length; index++)
            {
                roots[index].SetActive(index == activeIndex);
            }
        }

        private void SetAllMissionRootsActive(bool active)
        {
            GameObject[] roots = areaMissionRoots ?? Array.Empty<GameObject>();

            foreach (GameObject root in roots)
            {
                if (root != null)
                {
                    root.SetActive(active);
                }
            }
        }

        private bool HasExactMissionRootSet()
        {
            GameObject[] roots = areaMissionRoots ?? Array.Empty<GameObject>();

            if (roots.Length != MissionAreaOrder.Length)
            {
                return false;
            }

            var uniqueRoots = new HashSet<GameObject>();

            foreach (GameObject root in roots)
            {
                if (root == null || !uniqueRoots.Add(root))
                {
                    return false;
                }
            }

            return true;
        }

        private static CampaignAreaId ToCampaignArea(OpenWorldAreaId area)
        {
            return area switch
            {
                OpenWorldAreaId.StillwaterFeedMill =>
                    CampaignAreaId.StillwaterFeedMill,
                OpenWorldAreaId.HarrowEstate =>
                    CampaignAreaId.HarrowEstate,
                OpenWorldAreaId.BloodrootHollow =>
                    CampaignAreaId.BloodrootHollow,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(area), area, null)
            };
        }

        private static bool TryGetBarrierArea(
            CampaignAreaId area,
            out OpenWorldAreaId barrierArea)
        {
            switch (area)
            {
                case CampaignAreaId.StillwaterFeedMill:
                    barrierArea = OpenWorldAreaId.StillwaterFeedMill;
                    return true;
                case CampaignAreaId.HarrowEstate:
                    barrierArea = OpenWorldAreaId.HarrowEstate;
                    return true;
                case CampaignAreaId.BloodrootHollow:
                    barrierArea = OpenWorldAreaId.BloodrootHollow;
                    return true;
                default:
                    barrierArea = default;
                    return false;
            }
        }
    }
}
