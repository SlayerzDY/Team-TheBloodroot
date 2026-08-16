using System.Collections;
using TMPro;
using UnityEngine;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Binds a world mission to pre-existing authored TMP labels. It snapshots
    /// shared HUD state and restores only values it still owns, so a later HUD
    /// writer is never overwritten when this mission root is disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionObjectivePresenter : MonoBehaviour
    {
        [SerializeField] private WorldMissionDirector director;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text objectiveDetail;
        [SerializeField] private string labelText = "Objective";

        [Header("Temporary Authored Status")]
        [SerializeField] private string statusLabelText = "Status";
        [SerializeField, Min(0f)] private float rejectedStatusSeconds = 2.5f;
        [SerializeField] private string evidenceStatusLabelText =
            "Evidence Recovered";
        [SerializeField, Min(0f)] private float evidenceStatusSeconds = 6f;
        [SerializeField] private bool restoreSharedTextOnDisable = true;

        private string currentObjective = string.Empty;
        private int currentAmount;
        private int requiredAmount;
        private string temporaryStatusLabel = string.Empty;
        private string temporaryStatus = string.Empty;
        private Coroutine temporaryStatusRoutine;

        private bool snapshotCaptured;
        private string previousLabelText;
        private bool previousLabelActive;
        private string previousDetailText;
        private bool previousDetailActive;

        private bool ownsLabelText;
        private string ownedLabelText;
        private bool ownsLabelActive;
        private bool ownedLabelActive;
        private bool ownsDetailText;
        private string ownedDetailText;
        private bool ownsDetailActive;
        private bool ownedDetailActive;

        public bool IsShowingTemporaryStatus =>
            !string.IsNullOrWhiteSpace(temporaryStatus);

        public string TemporaryStatusLabel => temporaryStatusLabel;

        public string TemporaryStatusText => temporaryStatus;

        private void OnEnable()
        {
            CaptureSharedTextSnapshot();
            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
            StopTemporaryStatus();

            if (restoreSharedTextOnDisable)
            {
                RestoreSharedTextIfStillOwned();
            }

            ClearOwnershipSnapshot();
        }

        private void OnValidate()
        {
            rejectedStatusSeconds = Mathf.Max(0f, rejectedStatusSeconds);
            evidenceStatusSeconds = Mathf.Max(0f, evidenceStatusSeconds);
            statusLabelText = statusLabelText?.Trim() ?? string.Empty;
            evidenceStatusLabelText =
                evidenceStatusLabelText?.Trim() ?? string.Empty;
        }

        public void Configure(
            WorldMissionDirector missionDirector,
            TMP_Text authoredObjectiveLabel,
            TMP_Text authoredObjectiveDetail)
        {
            Unbind();
            StopTemporaryStatus();

            if (restoreSharedTextOnDisable)
            {
                RestoreSharedTextIfStillOwned();
            }

            ClearOwnershipSnapshot();
            director = missionDirector;
            objectiveLabel = authoredObjectiveLabel;
            objectiveDetail = authoredObjectiveDetail;

            if (isActiveAndEnabled)
            {
                CaptureSharedTextSnapshot();
                Bind();
                Refresh();
            }
        }

        private void Bind()
        {
            if (director == null)
                return;

            director.ObjectiveTextChanged -= HandleObjectiveTextChanged;
            director.ObjectiveProgressChanged -=
                HandleObjectiveProgressChanged;
            director.InteractionRejected -= HandleInteractionRejected;
            director.MissionStateChanged -= HandleMissionStateChanged;

            director.ObjectiveTextChanged += HandleObjectiveTextChanged;
            director.ObjectiveProgressChanged +=
                HandleObjectiveProgressChanged;
            director.InteractionRejected += HandleInteractionRejected;
            director.MissionStateChanged += HandleMissionStateChanged;

            currentObjective = director.CurrentObjectiveText;
            currentAmount = director.CurrentObjectiveAmount;
            requiredAmount = director.CurrentObjectiveRequired;
        }

        private void Unbind()
        {
            if (director == null)
                return;

            director.ObjectiveTextChanged -= HandleObjectiveTextChanged;
            director.ObjectiveProgressChanged -=
                HandleObjectiveProgressChanged;
            director.InteractionRejected -= HandleInteractionRejected;
            director.MissionStateChanged -= HandleMissionStateChanged;
        }

        private void HandleObjectiveTextChanged(string objective)
        {
            currentObjective = objective ?? string.Empty;
            Refresh();
        }

        private void HandleObjectiveProgressChanged(
            string objective,
            int current,
            int required)
        {
            currentObjective = objective ?? string.Empty;
            currentAmount = Mathf.Max(0, current);
            requiredAmount = Mathf.Max(0, required);
            Refresh();
        }

        private void HandleInteractionRejected(string reason)
        {
            ShowRejectedStatus(reason);
        }

        /// <summary>
        /// Shows a rejection through the mission's already-authored HUD text.
        /// Evidence adapters use this when they must reject before an
        /// objective can publish its own ActionRejected event.
        /// </summary>
        public void ShowRejectedStatus(string reason)
        {
            string message = string.IsNullOrWhiteSpace(reason)
                ? "That mission action cannot be completed right now."
                : reason.Trim();
            ShowTemporaryStatus(
                statusLabelText,
                message,
                rejectedStatusSeconds);
        }

        public void ShowEvidenceCollected(string title, string body)
        {
            string normalizedTitle = title?.Trim() ?? string.Empty;
            string normalizedBody = body?.Trim() ?? string.Empty;
            string message = normalizedTitle.Length == 0
                ? normalizedBody
                : normalizedBody.Length == 0
                    ? normalizedTitle
                    : $"{normalizedTitle}\n{normalizedBody}";
            ShowTemporaryStatus(
                evidenceStatusLabelText,
                message,
                evidenceStatusSeconds);
        }

        public void ShowTemporaryStatus(
            string label,
            string detail,
            float durationSeconds)
        {
            StopTemporaryStatus();
            temporaryStatusLabel = string.IsNullOrWhiteSpace(label)
                ? statusLabelText
                : label.Trim();
            temporaryStatus = detail?.Trim() ?? string.Empty;
            Refresh();

            float duration = Mathf.Max(0f, durationSeconds);
            if (temporaryStatus.Length == 0 || duration <= 0f)
            {
                temporaryStatusLabel = string.Empty;
                temporaryStatus = string.Empty;
                Refresh();
                return;
            }

            temporaryStatusRoutine = StartCoroutine(
                ClearTemporaryStatusAfterDelay(duration));
        }

        private void HandleMissionStateChanged(WorldMissionState missionState)
        {
            if (director != null)
            {
                currentObjective = director.CurrentObjectiveText;
                currentAmount = director.CurrentObjectiveAmount;
                requiredAmount = director.CurrentObjectiveRequired;
            }

            Refresh();
        }

        private void Refresh()
        {
            bool showingStatus =
                !string.IsNullOrWhiteSpace(temporaryStatus);
            string displayText = showingStatus
                ? temporaryStatus
                : currentObjective;
            bool hasContent = !string.IsNullOrWhiteSpace(displayText);

            if (objectiveLabel != null)
            {
                WriteLabelText(showingStatus
                    ? temporaryStatusLabel
                    : labelText);
                WriteLabelActive(hasContent);
            }

            if (objectiveDetail == null)
                return;

            string detailText;

            if (!showingStatus && hasContent && requiredAmount > 1)
            {
                detailText =
                    $"{displayText} ({currentAmount}/{requiredAmount})";
            }
            else
            {
                detailText = displayText;
            }

            WriteDetailText(detailText);
            WriteDetailActive(hasContent);
        }

        private IEnumerator ClearTemporaryStatusAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            temporaryStatusRoutine = null;
            temporaryStatusLabel = string.Empty;
            temporaryStatus = string.Empty;
            Refresh();
        }

        private void StopTemporaryStatus()
        {
            if (temporaryStatusRoutine != null)
            {
                StopCoroutine(temporaryStatusRoutine);
                temporaryStatusRoutine = null;
            }

            temporaryStatusLabel = string.Empty;
            temporaryStatus = string.Empty;
        }

        private void CaptureSharedTextSnapshot()
        {
            if (snapshotCaptured)
                return;

            if (objectiveLabel != null)
            {
                previousLabelText = objectiveLabel.text;
                previousLabelActive = objectiveLabel.gameObject.activeSelf;
            }

            if (objectiveDetail != null)
            {
                previousDetailText = objectiveDetail.text;
                previousDetailActive = objectiveDetail.gameObject.activeSelf;
            }

            snapshotCaptured = true;
        }

        private void RestoreSharedTextIfStillOwned()
        {
            if (!snapshotCaptured)
                return;

            bool labelStillOwned = objectiveLabel != null &&
                (!ownsLabelText || objectiveLabel.text == ownedLabelText) &&
                (!ownsLabelActive ||
                    objectiveLabel.gameObject.activeSelf == ownedLabelActive);
            bool detailStillOwned = objectiveDetail != null &&
                (!ownsDetailText || objectiveDetail.text == ownedDetailText) &&
                (!ownsDetailActive ||
                    objectiveDetail.gameObject.activeSelf == ownedDetailActive);

            if (objectiveLabel != null)
            {
                if (labelStillOwned && ownsLabelText)
                {
                    objectiveLabel.text = previousLabelText;
                }

                if (labelStillOwned && ownsLabelActive)
                {
                    objectiveLabel.gameObject.SetActive(previousLabelActive);
                }
            }

            if (objectiveDetail != null)
            {
                if (detailStillOwned && ownsDetailText)
                {
                    objectiveDetail.text = previousDetailText;
                }

                if (detailStillOwned && ownsDetailActive)
                {
                    objectiveDetail.gameObject.SetActive(previousDetailActive);
                }
            }
        }

        private void ClearOwnershipSnapshot()
        {
            snapshotCaptured = false;
            ownsLabelText = false;
            ownsLabelActive = false;
            ownsDetailText = false;
            ownsDetailActive = false;
            previousLabelText = string.Empty;
            previousDetailText = string.Empty;
        }

        private void WriteLabelText(string value)
        {
            ownedLabelText = value ?? string.Empty;
            objectiveLabel.text = ownedLabelText;
            ownsLabelText = true;
        }

        private void WriteLabelActive(bool active)
        {
            ownedLabelActive = active;
            objectiveLabel.gameObject.SetActive(active);
            ownsLabelActive = true;
        }

        private void WriteDetailText(string value)
        {
            ownedDetailText = value ?? string.Empty;
            objectiveDetail.text = ownedDetailText;
            ownsDetailText = true;
        }

        private void WriteDetailActive(bool active)
        {
            ownedDetailActive = active;
            objectiveDetail.gameObject.SetActive(active);
            ownsDetailActive = true;
        }
    }
}
