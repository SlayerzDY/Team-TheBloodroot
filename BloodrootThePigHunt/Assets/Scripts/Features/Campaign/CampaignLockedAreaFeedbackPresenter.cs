using System.Collections;
using TMPro;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Displays locked-area feedback through UI already authored in a scene or
    /// prefab. It never instantiates UI or adds components at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignLockedAreaFeedbackPresenter : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private CanvasGroup messageCanvasGroup;

        [SerializeField, Min(0f)]
        private float displaySeconds = 3f;

        private Coroutine hideRoutine;
        private CampaignAreaId displayedArea;
        private bool isShowing;
        private bool hasPresentationSnapshot;
        private string previousText = string.Empty;
        private bool previousTextEnabled;
        private float previousCanvasAlpha;
        private bool previousCanvasInteractable;
        private bool previousCanvasBlocksRaycasts;
        private string ownedMessage = string.Empty;

        public TMP_Text MessageText => messageText;

        public CanvasGroup MessageCanvasGroup => messageCanvasGroup;

        public float DisplaySeconds => displaySeconds;

        public bool IsShowing => isShowing;

        public CampaignAreaId DisplayedArea => displayedArea;

        private void Awake()
        {
            isShowing = false;
        }

        private void OnDisable()
        {
            StopHideRoutine();
            RestoreAuthoredPresentation();
        }

        private void OnValidate()
        {
            displaySeconds = Mathf.Max(0f, displaySeconds);
        }

        /// <summary>
        /// Shows a message supplied by a locked-area trigger. The timer uses
        /// realtime so pause menus do not leave stale feedback visible.
        /// </summary>
        public void ShowLockedArea(
            CampaignAreaId area,
            string message)
        {
            StopHideRoutine();
            CaptureAuthoredPresentation();
            displayedArea = area;
            isShowing = true;
            ownedMessage = message ?? string.Empty;

            if (messageText != null)
            {
                messageText.text = ownedMessage;
                messageText.enabled = true;
            }

            if (messageCanvasGroup != null)
            {
                messageCanvasGroup.alpha = 1f;
                messageCanvasGroup.interactable = false;
                messageCanvasGroup.blocksRaycasts = false;
            }

            if (displaySeconds <= 0f)
            {
                RestoreAuthoredPresentation();
                return;
            }

            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        /// <summary>
        /// Clears feedback only when it belongs to the supplied campaign area.
        /// This lets an unlocking barrier clear its own feedback without
        /// suppressing a message from another locked area.
        /// </summary>
        public bool ClearIfShowing(CampaignAreaId area)
        {
            if (!isShowing || displayedArea != area)
            {
                return false;
            }

            StopHideRoutine();
            RestoreAuthoredPresentation();
            return true;
        }

        /// <summary>
        /// Configuration API for editor tooling. Both references must point to
        /// existing, saved UI components; this component never creates them.
        /// </summary>
        public void Configure(
            TMP_Text authoredMessageText,
            CanvasGroup authoredMessageCanvasGroup,
            float seconds)
        {
            StopHideRoutine();
            RestoreAuthoredPresentation();
            messageText = authoredMessageText;
            messageCanvasGroup = authoredMessageCanvasGroup;
            displaySeconds = Mathf.Max(0f, seconds);
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(displaySeconds);
            hideRoutine = null;
            RestoreAuthoredPresentation();
        }

        private void CaptureAuthoredPresentation()
        {
            if (hasPresentationSnapshot)
            {
                return;
            }

            if (messageText != null)
            {
                previousText = messageText.text;
                previousTextEnabled = messageText.enabled;
            }

            if (messageCanvasGroup != null)
            {
                previousCanvasAlpha = messageCanvasGroup.alpha;
                previousCanvasInteractable =
                    messageCanvasGroup.interactable;
                previousCanvasBlocksRaycasts =
                    messageCanvasGroup.blocksRaycasts;
            }

            hasPresentationSnapshot = true;
        }

        private void RestoreAuthoredPresentation()
        {
            if (!hasPresentationSnapshot)
            {
                isShowing = false;
                ownedMessage = string.Empty;
                return;
            }

            // Restore only values still owned by this presenter. If another
            // authored objective system changed the shared text while the
            // feedback was visible, its newer value wins.
            if (messageText != null &&
                string.Equals(messageText.text, ownedMessage))
            {
                messageText.text = previousText;
                messageText.enabled = previousTextEnabled;
            }

            if (messageCanvasGroup != null &&
                Mathf.Approximately(messageCanvasGroup.alpha, 1f) &&
                !messageCanvasGroup.interactable &&
                !messageCanvasGroup.blocksRaycasts)
            {
                messageCanvasGroup.alpha = previousCanvasAlpha;
                messageCanvasGroup.interactable =
                    previousCanvasInteractable;
                messageCanvasGroup.blocksRaycasts =
                    previousCanvasBlocksRaycasts;
            }

            hasPresentationSnapshot = false;
            isShowing = false;
            ownedMessage = string.Empty;
        }

        private void StopHideRoutine()
        {
            if (hideRoutine == null)
            {
                return;
            }

            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
    }
}
