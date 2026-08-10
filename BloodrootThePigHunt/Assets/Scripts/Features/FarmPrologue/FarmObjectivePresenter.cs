using System.Collections;
using TMPro;
using UnityEngine;

namespace Bloodroot.Features.FarmPrologue
{
    /// <summary>
    /// Binds Farm prologue state to UI objects authored in the scene/prefab.
    /// This component never creates UI at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmObjectivePresenter : MonoBehaviour
    {
        [SerializeField] private FarmPrologueDirector director;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text objectiveDetail;
        [SerializeField] private string labelText = "Objective";

        [Header("Temporary Authored Status")]
        [SerializeField] private string statusLabelText = "Status";
        [SerializeField, Min(0f)] private float rejectedStatusSeconds = 2.5f;

        private string currentObjective = string.Empty;
        private string temporaryStatus = string.Empty;
        private int currentAmount;
        private int requiredAmount;
        private Coroutine temporaryStatusRoutine;

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
            StopTemporaryStatus();
            Refresh();
        }

        private void OnValidate()
        {
            rejectedStatusSeconds = Mathf.Max(0f, rejectedStatusSeconds);
        }

        public void Configure(
            FarmPrologueDirector prologueDirector,
            TMP_Text authoredObjectiveLabel,
            TMP_Text authoredObjectiveDetail)
        {
            Unbind();
            StopTemporaryStatus();
            director = prologueDirector;
            objectiveLabel = authoredObjectiveLabel;
            objectiveDetail = authoredObjectiveDetail;

            if (isActiveAndEnabled)
            {
                Bind();
            }

            Refresh();
        }

        private void Bind()
        {
            if (director == null)
                return;

            director.ObjectiveTextChanged -= HandleObjectiveTextChanged;
            director.ObjectiveProgressChanged -= HandleObjectiveProgressChanged;
            director.InteractionRejected -= HandleInteractionRejected;
            director.ObjectiveTextChanged += HandleObjectiveTextChanged;
            director.ObjectiveProgressChanged += HandleObjectiveProgressChanged;
            director.InteractionRejected += HandleInteractionRejected;
            currentObjective = director.CurrentObjectiveText;
            currentAmount = director.CurrentObjectiveAmount;
            requiredAmount = director.CurrentObjectiveRequired;
        }

        private void Unbind()
        {
            if (director == null)
                return;

            director.ObjectiveTextChanged -= HandleObjectiveTextChanged;
            director.ObjectiveProgressChanged -= HandleObjectiveProgressChanged;
            director.InteractionRejected -= HandleInteractionRejected;
        }

        private void HandleObjectiveTextChanged(string objective)
        {
            currentObjective = objective ?? string.Empty;
            Refresh();
        }

        private void HandleInteractionRejected(string reason)
        {
            StopTemporaryStatus();
            temporaryStatus = string.IsNullOrWhiteSpace(reason)
                ? "That cannot be done right now."
                : reason.Trim();
            Refresh();

            if (rejectedStatusSeconds <= 0f)
            {
                temporaryStatus = string.Empty;
                Refresh();
                return;
            }

            temporaryStatusRoutine =
                StartCoroutine(ClearTemporaryStatusAfterDelay());
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

        private void Refresh()
        {
            bool showingStatus =
                !string.IsNullOrWhiteSpace(temporaryStatus);
            string displayText = showingStatus
                ? temporaryStatus
                : currentObjective;
            bool hasObjective = !string.IsNullOrWhiteSpace(displayText);

            if (objectiveLabel != null)
            {
                objectiveLabel.text = showingStatus
                    ? statusLabelText
                    : labelText;
                objectiveLabel.gameObject.SetActive(hasObjective);
            }

            if (objectiveDetail == null)
                return;

            if (!showingStatus && hasObjective && requiredAmount > 1)
            {
                objectiveDetail.text =
                    $"{displayText} ({currentAmount}/{requiredAmount})";
            }
            else
            {
                objectiveDetail.text = displayText;
            }

            objectiveDetail.gameObject.SetActive(hasObjective);
        }

        private IEnumerator ClearTemporaryStatusAfterDelay()
        {
            yield return new WaitForSecondsRealtime(rejectedStatusSeconds);
            temporaryStatusRoutine = null;
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

            temporaryStatus = string.Empty;
        }
    }
}
