using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Fail-closed presentation and collision adapter for the authored Hollow
    /// thorn-veil states. CanEnterHollow is the sole opening authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignThornVeilGate : MonoBehaviour
    {
        [Header("Campaign Authority")]
        [SerializeField] private CampaignStateService stateService;

        [Header("Authored Veil States")]
        [SerializeField] private GameObject blockedRoot;
        [SerializeField] private GameObject openRoot;
        [SerializeField] private Collider[] blockingColliders =
            Array.Empty<Collider>();
        [SerializeField] private NavMeshObstacle[] blockingObstacles =
            Array.Empty<NavMeshObstacle>();

        [Header("Authored Events")]
        [SerializeField] private UnityEvent gateOpened = new UnityEvent();
        [SerializeField] private UnityEvent gateClosed = new UnityEvent();

        private CampaignStateService subscribedState;
        private bool isOpen;
        private bool hasAppliedState;

        public bool IsOpen => isOpen;
        public bool HasValidAuthoredGate => ValidateAuthoredGate();
        public GameObject BlockedRoot => blockedRoot;
        public GameObject OpenRoot => openRoot;

        private void OnEnable()
        {
            BindStateService();
            RefreshGate();
        }

        private void OnDisable()
        {
            UnbindStateService();
        }

        public void Configure(
            CampaignStateService campaignState,
            GameObject authoredBlockedRoot,
            GameObject authoredOpenRoot,
            Collider[] solidColliders,
            NavMeshObstacle[] carvingObstacles)
        {
            UnbindStateService();
            stateService = campaignState;
            blockedRoot = authoredBlockedRoot;
            openRoot = authoredOpenRoot;
            blockingColliders = solidColliders ?? Array.Empty<Collider>();
            blockingObstacles = carvingObstacles ??
                                Array.Empty<NavMeshObstacle>();
            BindStateService();
            RefreshGate();
        }

        /// <summary>
        /// Re-evaluates the durable campaign predicate. Missing or internally
        /// inconsistent state always leaves the veil closed.
        /// </summary>
        public bool RefreshGate()
        {
            CampaignStateService state = ResolveStateService();
            BindStateService();
            bool shouldOpen = state != null &&
                              ValidateAuthoredGate() &&
                              state.Current.CanEnterHollow;
            ApplyGateState(shouldOpen);
            return isOpen;
        }

        private void ApplyGateState(bool open)
        {
            if (blockedRoot != null && blockedRoot == openRoot)
            {
                open = false;

            }

            bool changed = !hasAppliedState || isOpen != open;

            if (open)
            {
                SetBlockingEnabled(false);
                SetRootActive(blockedRoot, false);
                SetRootActive(openRoot, true);
            }
            else
            {
                SetRootActive(openRoot, false);
                SetRootActive(blockedRoot, true);
                SetBlockingEnabled(true);
            }

            isOpen = open;
            hasAppliedState = true;
            if (!changed)
                return;

            if (isOpen)
            {
                gateOpened.Invoke();
            }
            else
            {
                gateClosed.Invoke();
            }
        }

        private void SetBlockingEnabled(bool enabled)
        {
            if (blockingColliders != null)
            {
                foreach (Collider blockingCollider in blockingColliders)
                {
                    if (blockingCollider != null &&
                        blockingCollider.enabled != enabled)
                    {
                        blockingCollider.enabled = enabled;
                    }
                }
            }

            if (blockingObstacles == null)
                return;

            foreach (NavMeshObstacle obstacle in blockingObstacles)
            {
                if (obstacle != null && obstacle.enabled != enabled)
                {
                    obstacle.enabled = enabled;
                }
            }
        }

        private static void SetRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        private void HandleProgressChanged(CampaignProgressSnapshot snapshot)
        {
            RefreshGate();
        }

        private void BindStateService()
        {
            CampaignStateService state = ResolveStateService();
            if (subscribedState == state)
                return;

            UnbindStateService();
            subscribedState = state;
            if (subscribedState != null)
            {
                subscribedState.ProgressChanged += HandleProgressChanged;
            }
        }

        private void UnbindStateService()
        {
            if (subscribedState != null)
            {
                subscribedState.ProgressChanged -= HandleProgressChanged;
            }

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

        private bool ValidateAuthoredGate()
        {
            if (blockedRoot == null || openRoot == null ||
                blockedRoot == openRoot)
            {
                return false;
            }

            bool hasSolidCollider = false;
            if (blockingColliders != null)
            {
                foreach (Collider blockingCollider in blockingColliders)
                {
                    if (blockingCollider != null &&
                        !blockingCollider.isTrigger)
                    {
                        hasSolidCollider = true;
                        break;
                    }
                }
            }

            if (!hasSolidCollider || blockingObstacles == null)
                return false;

            foreach (NavMeshObstacle obstacle in blockingObstacles)
            {
                if (obstacle != null)
                    return true;
            }

            return false;
        }

        private void OnValidate()
        {
            blockingColliders ??= Array.Empty<Collider>();
            blockingObstacles ??= Array.Empty<NavMeshObstacle>();
        }
    }
}
