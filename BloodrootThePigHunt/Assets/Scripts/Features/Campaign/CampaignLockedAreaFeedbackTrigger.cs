using System.Collections.Generic;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Trigger-side feedback for an invisible locked-area barrier. It only
    /// reports the prerequisite to an authored presenter; it does not own,
    /// enable, or disable the barrier collider itself.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CampaignLockedAreaFeedbackTrigger : MonoBehaviour
    {
        [SerializeField]
        private CampaignAreaId lockedArea;

        [SerializeField]
        private CampaignLockedAreaFeedbackPresenter presenter;

        [SerializeField]
        private string playerTag = "Player";

        [SerializeField, TextArea]
        private string lockedMessage = string.Empty;

        private readonly HashSet<Collider> playerColliders = new();
        private CampaignStateService subscribedState;

        public CampaignAreaId LockedArea => lockedArea;

        public CampaignLockedAreaFeedbackPresenter Presenter => presenter;

        public string LockedMessage => GetMessage();

        private void Awake()
        {
        }

        private void OnEnable()
        {
            TrySubscribe();
            ClearIfUnlocked();
        }

        private void Start()
        {
            // CampaignStateService is persistent and normally initializes
            // before scene components. Start covers scene-authored ordering.
            TrySubscribe();
            ClearIfUnlocked();
        }

        private void OnDisable()
        {
            playerColliders.Clear();
            presenter?.ClearIfShowing(lockedArea);
            Unsubscribe();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            bool wasOutside = playerColliders.Count == 0;
            playerColliders.Add(other);

            if (wasOutside)
            {
                ShowFeedbackIfLocked();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            playerColliders.Remove(other);
        }

        /// <summary>
        /// Configuration API for editor tooling. This does not alter the
        /// authored collider or create presentation objects.
        /// </summary>
        public void Configure(
            CampaignAreaId area,
            CampaignLockedAreaFeedbackPresenter feedbackPresenter,
            string authoredPlayerTag = "Player",
            string messageOverride = "")
        {
            lockedArea = area;
            presenter = feedbackPresenter;
            playerTag = string.IsNullOrWhiteSpace(authoredPlayerTag)
                ? "Player"
                : authoredPlayerTag.Trim();
            lockedMessage = messageOverride?.Trim() ?? string.Empty;
        }

        private void TrySubscribe()
        {
            CampaignStateService state = CampaignStateService.Instance;

            if (state == subscribedState)
            {
                return;
            }

            Unsubscribe();
            subscribedState = state;

            if (subscribedState != null)
            {
                subscribedState.AreaUnlocked += HandleAreaUnlocked;
                subscribedState.ProgressChanged += HandleProgressChanged;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedState != null)
            {
                subscribedState.AreaUnlocked -= HandleAreaUnlocked;
                subscribedState.ProgressChanged -= HandleProgressChanged;
            }

            subscribedState = null;
        }

        private void HandleAreaUnlocked(CampaignAreaId area)
        {
            if (area == lockedArea)
            {
                presenter?.ClearIfShowing(lockedArea);
            }
        }

        private void HandleProgressChanged(CampaignProgressSnapshot progress)
        {
            if (progress.IsAreaUnlocked(lockedArea))
            {
                presenter?.ClearIfShowing(lockedArea);
            }
        }

        private void ClearIfUnlocked()
        {
            if (subscribedState != null &&
                subscribedState.IsAreaUnlocked(lockedArea))
            {
                presenter?.ClearIfShowing(lockedArea);
            }
        }

        private void ShowFeedbackIfLocked()
        {
            if (presenter == null ||
                (subscribedState != null &&
                 subscribedState.IsAreaUnlocked(lockedArea)))
            {
                return;
            }

            presenter.ShowLockedArea(lockedArea, GetMessage());
        }

        private bool IsPlayerCollider(Collider candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(playerTag))
            {
                return false;
            }

            for (Transform current = candidate.transform;
                 current != null;
                 current = current.parent)
            {
                if (current.CompareTag(playerTag))
                {
                    return true;
                }
            }

            if (candidate.attachedRigidbody == null)
            {
                return false;
            }

            Transform rigidbodyRoot = candidate.attachedRigidbody.transform;

            return rigidbodyRoot != null &&
                rigidbodyRoot.CompareTag(playerTag);
        }

        private string GetMessage()
        {
            if (!string.IsNullOrWhiteSpace(lockedMessage))
            {
                return lockedMessage.Trim();
            }

            return lockedArea switch
            {
                CampaignAreaId.StillwaterFeedMill =>
                    "Complete Black Pines to enter Stillwater Feed Mill.",
                CampaignAreaId.HarrowEstate =>
                    "Complete Stillwater Feed Mill to enter Harrow Estate.",
                CampaignAreaId.BloodrootHollow =>
                    "Complete Harrow Estate to enter Bloodroot Hollow.",
                _ => "This area is not available yet."
            };
        }
    }
}
