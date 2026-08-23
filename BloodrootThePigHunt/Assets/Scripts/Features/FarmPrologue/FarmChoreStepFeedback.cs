using UnityEngine;

namespace Bloodroot.Features.FarmPrologue
{
    /// <summary>
    /// Mirrors an authored chore's progress onto two pre-existing presentation
    /// roots. This component never creates visuals or other runtime objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmChoreStepFeedback : MonoBehaviour
    {
        private const string PersistentPresentationName =
            "Persistent Presentation";

        [Header("Existing Chore Hook")]
        [SerializeField] private FarmChoreInteractable chore;

        [Header("Authored Presentation Roots")]
        [Tooltip("Physical farm props that remain in the level before and after this chore completes.")]
        [SerializeField] private GameObject persistentRoot;
        [Tooltip("Temporary state or pickup cue shown only before completion.")]
        [SerializeField] private GameObject pendingRoot;
        [Tooltip("Feed, water, cleaned-stall, or other result shown after completion.")]
        [SerializeField] private GameObject completedRoot;

        private FarmChoreInteractable boundChore;

        public FarmChoreInteractable Chore => chore;
        public GameObject PersistentRoot => persistentRoot;
        public GameObject PendingRoot => pendingRoot;
        public GameObject CompletedRoot => completedRoot;
        public bool IsShowingCompleted { get; private set; }

        private void OnEnable()
        {
            BindAndRefresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnValidate()
        {
            ResolvePersistentRoot();

        }

        public void Configure(
            FarmChoreInteractable choreInteractable,
            GameObject authoredPendingRoot,
            GameObject authoredCompletedRoot)
        {
            persistentRoot = ResolveSiblingPersistentRoot(
                authoredPendingRoot,
                authoredCompletedRoot);
            Configure(
                choreInteractable,
                persistentRoot,
                authoredPendingRoot,
                authoredCompletedRoot);
        }

        public void Configure(
            FarmChoreInteractable choreInteractable,
            GameObject authoredPersistentRoot,
            GameObject authoredPendingRoot,
            GameObject authoredCompletedRoot)
        {
            Unbind();
            chore = choreInteractable;
            persistentRoot = authoredPersistentRoot;
            pendingRoot = authoredPendingRoot;
            completedRoot = authoredCompletedRoot;

            if (isActiveAndEnabled)
            {
                BindAndRefresh();
            }
        }

        public void RefreshPresentation()
        {
            if (chore == null)
                return;

            ApplyCompletionState(chore.IsComplete);
        }

        private void BindAndRefresh()
        {
            Unbind();
            ResolvePersistentRoot();

            if (chore == null)
                return;

            chore.ProgressChanged += HandleProgressChanged;
            boundChore = chore;
            RefreshPresentation();
        }

        private void Unbind()
        {
            if (boundChore != null)
            {
                boundChore.ProgressChanged -= HandleProgressChanged;
            }

            boundChore = null;
        }

        private void HandleProgressChanged(
            string objective,
            int completed,
            int required)
        {
            ApplyCompletionState(
                completed >= Mathf.Max(1, required));
        }

        private void ApplyCompletionState(bool completed)
        {
            ResolvePersistentRoot();

            if (pendingRoot != null && pendingRoot == completedRoot ||
                persistentRoot != null &&
                (persistentRoot == pendingRoot ||
                 persistentRoot == completedRoot))
            {
                return;
            }

            IsShowingCompleted = completed;
            SetActive(persistentRoot, true);
            SetActive(pendingRoot, !completed);
            SetActive(completedRoot, completed);
        }

        private void ResolvePersistentRoot()
        {
            if (persistentRoot == null)
            {
                persistentRoot = ResolveSiblingPersistentRoot(
                    pendingRoot,
                    completedRoot);
            }
        }

        private static GameObject ResolveSiblingPersistentRoot(
            GameObject pending,
            GameObject completed)
        {
            Transform parent = pending != null
                ? pending.transform.parent
                : completed != null
                    ? completed.transform.parent
                    : null;
            if (parent == null ||
                completed != null && completed.transform.parent != parent)
            {
                return null;
            }

            Transform persistent = parent.Find(PersistentPresentationName);
            return persistent != null ? persistent.gameObject : null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
