using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Farm-only interaction that burns current Heartroot cargo through the
    /// owned transaction bridge. Safety's protected win presenter is invoked
    /// exactly once and only after campaign plus Safety persistence commits.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FarmHeartrootBurnRitual : MonoBehaviour,
        global::IInteract
    {
        [SerializeField] private CampaignStateService stateService;
        [SerializeField] private CampaignHeartrootFinaleBridge finaleBridge;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool presentDurableWinOnStart = true;

        [Header("Authored Events")]
        [SerializeField] private UnityEvent burnAccepted = new();
        [SerializeField] private UnityEvent burnRejected = new();
        [SerializeField] private UnityEvent winPresented = new();

        private bool hasPresentedWin;
        private CampaignStateService subscribedState;
        private string lastFailureReason = string.Empty;

        /// <summary>
        /// Test/observer seam raised immediately before Safety's protected
        /// gameManager.youWin presentation. A listener can stand in for the
        /// manager in focused tests; production may observe it as well.
        /// </summary>
        public event Action CampaignWinRequested;

        public bool HasPresentedWin => hasPresentedWin;
        public string LastFailureReason => lastFailureReason;
        public bool CanBurn
        {
            get
            {
                CampaignStateService state = ResolveStateService();
                CampaignProgressSnapshot progress = state != null
                    ? state.Current
                    : default;
                return state != null && progress.HollowCompleted &&
                       progress.HeartrootCarried &&
                       !progress.HeartrootBurned &&
                       !progress.CampaignCompleted;
            }
        }

        private void Awake()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            BindStateService();
        }

        private void OnDisable()
        {
            UnbindStateService();
        }

        private void Start()
        {
            RefreshInteractionAvailability();
            if (presentDurableWinOnStart)
                PresentDurableWinIfNeeded();
        }

        public void SendInteract(Collider target)
        {
            TryBurn(target);
        }

        public bool TryBurn(Collider target)
        {
            if (!IsAuthoredInteractionCollider(target))
            {
                return Reject(
                    "The Heartroot burn requires the authored raycast interaction collider.");
            }

            GameObject requester = ResolveAuthoritativePlayer();
            if (!IsPlayerObject(requester))
                return Reject("Only the authoritative Player can burn the Heartroot.");

            if (!CanBurn)
            {
                return Reject(
                    "The exposed Heartroot is not current Farm cargo.");
            }

            if (!CanPresentWin())
            {
                return Reject(
                    "The protected win presenter is unavailable; the Heartroot was not burned.");
            }

            string contractError = string.Empty;
            if (finaleBridge == null ||
                !finaleBridge.ValidateRuntimeContract(
                    out contractError))
            {
                return Reject(string.IsNullOrWhiteSpace(contractError)
                    ? "The Farm Heartroot transaction bridge is unavailable."
                    : contractError);
            }

            if (!finaleBridge.TryBurnHeartroot(requester))
            {
                return Reject(string.IsNullOrWhiteSpace(
                        finaleBridge.LastFailureReason)
                    ? "The Heartroot burn transaction was rejected."
                    : finaleBridge.LastFailureReason);
            }

            CampaignStateService state = ResolveStateService();
            CampaignProgressSnapshot progress = state != null
                ? state.Current
                : default;
            if (state == null || !progress.HeartrootCarried ||
                !progress.HeartrootBurned ||
                !progress.CampaignCompleted)
            {
                return Reject(
                    "The Heartroot burn did not produce a durable campaign completion fact.");
            }

            lastFailureReason = string.Empty;
            CampaignEventUtility.Invoke(burnAccepted, this);
            return PresentDurableWinIfNeeded();
        }

        public bool PresentDurableWinIfNeeded()
        {
            if (hasPresentedWin)
                return true;

            CampaignStateService state = ResolveStateService();
            CampaignProgressSnapshot progress = state != null
                ? state.Current
                : default;
            if (state == null || !progress.HeartrootCarried ||
                !progress.HeartrootBurned ||
                !progress.CampaignCompleted || !CanPresentWin())
            {
                return false;
            }

            // Latch before any callback so re-entrant listeners cannot invoke
            // the protected presenter more than once.
            hasPresentedWin = true;
            CampaignEventUtility.Invoke(CampaignWinRequested, this);
            if (global::gameManager.instance != null)
                global::gameManager.instance.youWin();

            CampaignEventUtility.Invoke(winPresented, this);
            return true;
        }

        public void Configure(
            CampaignStateService campaignState,
            CampaignHeartrootFinaleBridge bridge,
            Collider authoredInteractionCollider,
            string authoredPlayerTag = "Player")
        {
            UnbindStateService();
            stateService = campaignState;
            finaleBridge = bridge;
            interactionCollider = authoredInteractionCollider != null
                ? authoredInteractionCollider
                : GetComponent<Collider>();
            playerTag = authoredPlayerTag?.Trim() ?? string.Empty;
            hasPresentedWin = false;
            lastFailureReason = string.Empty;
            BindStateService();
            RefreshInteractionAvailability();
        }

        public bool ValidateRuntimeContract(out string error)
        {
            error = string.Empty;
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactionCollider == null ||
                interactionCollider.gameObject != gameObject ||
                !interactionCollider.enabled ||
                interactionCollider.isTrigger ||
                interactableLayer < 0 || gameObject.layer != interactableLayer ||
                !gameObject.CompareTag("Interact"))
            {
                error =
                    "Farm Heartroot burn requires its exact enabled solid Interact/Interactable raycast collider.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerTag))
            {
                error = "Farm Heartroot burn requires a Player tag.";
                return false;
            }

            if (ResolveStateService() == null || finaleBridge == null ||
                !finaleBridge.ValidateRuntimeContract(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Farm Heartroot burn requires valid persistence and presentation references."
                    : error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private CampaignStateService ResolveStateService()
        {
            CampaignStateService persistent =
                CampaignStateService.Instance;
            if (persistent != null && persistent != stateService)
                stateService = persistent;

            return stateService;
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

        private void HandleProgressChanged(CampaignProgressSnapshot _)
        {
            RefreshInteractionAvailability();
            PresentDurableWinIfNeeded();
        }

        private void RefreshInteractionAvailability()
        {
            if (Application.isPlaying && interactionCollider != null)
                interactionCollider.enabled = CanBurn;
        }

        private bool CanPresentWin()
        {
            return global::gameManager.instance != null ||
                   CampaignWinRequested != null;
        }

        private bool IsAuthoredInteractionCollider(Collider candidate)
        {
            return candidate != null && candidate == interactionCollider &&
                   candidate.enabled && !candidate.isTrigger;
        }

        private static GameObject ResolveAuthoritativePlayer()
        {
            return global::gameManager.instance != null
                ? global::gameManager.instance.player
                : null;
        }

        private bool IsPlayerObject(GameObject candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(playerTag))
                return false;

            if (global::gameManager.instance == null ||
                global::gameManager.instance.player != candidate)
            {
                return false;
            }

            Transform current = candidate.transform;
            while (current != null)
            {
                try
                {
                    if (current.CompareTag(playerTag))
                        return true;
                }
                catch (UnityException exception)
                {

                    return false;
                }

                current = current.parent;
            }

            return false;
        }

        private bool Reject(string reason)
        {
            lastFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "The Farm Heartroot burn was rejected."
                : reason.Trim();

            CampaignEventUtility.Invoke(burnRejected, this);
            return false;
        }

        private void OnValidate()
        {
            playerTag = playerTag?.Trim() ?? string.Empty;
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }
    }
}
