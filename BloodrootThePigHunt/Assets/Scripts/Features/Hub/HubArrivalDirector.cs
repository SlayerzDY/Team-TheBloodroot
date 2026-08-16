using System;
using System.Collections;
using Bloodroot.Campaign;
using Bloodroot.Features.FarmPrologue;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.Hub
{
    /// <summary>
    /// Owns the one-time, save-backed arrival beat after the Farm prologue.
    /// Presentation is supplied through authored objects and events so final
    /// narrative, cameras, and props can replace placeholders without code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubArrivalDirector : MonoBehaviour
    {
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private FarmPrologueDirector prologueDirector;
        [SerializeField] private GameObject firstArrivalPresentationRoot;
        [SerializeField, Min(0f)] private float autoCompleteSeconds = 3f;
        [SerializeField] private UnityEvent firstArrivalStarted = new();
        [SerializeField] private UnityEvent firstArrivalCompleted = new();
        [SerializeField] private HubStringUnityEvent arrivalFailed = new();

        private CampaignStateService boundState;
        private FarmPrologueDirector boundDirector;
        private Coroutine arrivalRoutine;
        private Coroutine startupRoutine;
        private bool arrivalActive;

        public bool IsFirstArrivalActive => arrivalActive;

        public event Action FirstArrivalStarted;
        public event Action FirstArrivalCompleted;
        public event Action<string> ArrivalFailed;

        private void OnEnable()
        {
            ResolveReferences();
            BindEvents();
            startupRoutine = StartCoroutine(EvaluateAfterSceneStart());
        }

        private void OnDisable()
        {
            UnbindEvents();

            if (startupRoutine != null)
            {
                StopCoroutine(startupRoutine);
                startupRoutine = null;
            }

            if (arrivalRoutine != null)
            {
                StopCoroutine(arrivalRoutine);
                arrivalRoutine = null;
            }

            arrivalActive = false;
            SetPresentationActive(false);
        }

        private void OnValidate()
        {
            autoCompleteSeconds = Mathf.Max(0f, autoCompleteSeconds);
        }

        public bool TryBeginFirstArrival()
        {
            ResolveReferences();
            BindEvents();

            if (arrivalActive)
                return true;

            if (campaignState == null ||
                !campaignState.HasCompletedPrologue ||
                campaignState.Current.HubIntroductionCompleted)
            {
                return false;
            }

            if (prologueDirector != null &&
                prologueDirector.CurrentPhase != FarmProloguePhase.Hub)
            {
                return false;
            }

            arrivalActive = true;
            SetPresentationActive(true);
            HubEventUtility.Invoke(FirstArrivalStarted, this);
            HubEventUtility.Invoke(firstArrivalStarted, this);

            if (autoCompleteSeconds > 0f)
            {
                ScheduleAutomaticCompletion();
            }

            return true;
        }

        public bool CompleteFirstArrival()
        {
            if (!arrivalActive)
                return false;

            ResolveReferences();

            if (campaignState == null)
            {
                Fail("The first hub arrival could not be saved because Campaign State is missing.");
                return false;
            }

            if (!campaignState.Current.HubIntroductionCompleted &&
                !campaignState.MarkHubIntroductionCompleted())
            {
                Fail("The first hub arrival could not be saved. The sequence remains available to retry.");
                ScheduleAutomaticCompletion();
                return false;
            }

            if (arrivalRoutine != null)
            {
                StopCoroutine(arrivalRoutine);
                arrivalRoutine = null;
            }

            arrivalActive = false;
            SetPresentationActive(false);
            HubEventUtility.Invoke(FirstArrivalCompleted, this);
            HubEventUtility.Invoke(firstArrivalCompleted, this);
            return true;
        }

        public void Configure(
            CampaignStateService state,
            FarmPrologueDirector director,
            GameObject presentationRoot,
            float automaticCompletionSeconds)
        {
            UnbindEvents();
            campaignState = state;
            prologueDirector = director;
            firstArrivalPresentationRoot = presentationRoot;
            autoCompleteSeconds = Mathf.Max(0f, automaticCompletionSeconds);

            if (isActiveAndEnabled)
            {
                ResolveReferences();
                BindEvents();
            }
        }

        private IEnumerator EvaluateAfterSceneStart()
        {
            yield return null;
            startupRoutine = null;

            if (!isActiveAndEnabled)
                yield break;

            ResolveReferences();
            BindEvents();

            if (campaignState != null &&
                campaignState.Current.HubIntroductionCompleted)
            {
                SetPresentationActive(false);
                yield break;
            }

            TryBeginFirstArrival();
        }

        private IEnumerator CompleteAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            arrivalRoutine = null;
            CompleteFirstArrival();
        }

        private void ScheduleAutomaticCompletion()
        {
            if (!isActiveAndEnabled || !arrivalActive ||
                autoCompleteSeconds <= 0f || arrivalRoutine != null)
            {
                return;
            }

            arrivalRoutine = StartCoroutine(
                CompleteAfterDelay(Mathf.Max(1f, autoCompleteSeconds)));
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
                    boundState.NewGameStarted -= HandleNewGame;
                }

                boundState = campaignState;

                if (boundState != null)
                {
                    boundState.ProgressLoaded += HandleProgress;
                    boundState.NewGameStarted += HandleNewGame;
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
                boundState.NewGameStarted -= HandleNewGame;
                boundState = null;
            }

            if (boundDirector != null)
            {
                boundDirector.HubUnlocked -= HandleHubUnlocked;
                boundDirector = null;
            }
        }

        private void HandleProgress(CampaignProgressSnapshot snapshot)
        {
            if (snapshot.HubIntroductionCompleted)
            {
                if (arrivalRoutine != null)
                {
                    StopCoroutine(arrivalRoutine);
                    arrivalRoutine = null;
                }

                arrivalActive = false;
                SetPresentationActive(false);
            }
        }

        private void HandleNewGame()
        {
            arrivalActive = false;
            SetPresentationActive(false);
        }

        private void HandleHubUnlocked()
        {
            TryBeginFirstArrival();
        }

        private void SetPresentationActive(bool active)
        {
            if (firstArrivalPresentationRoot != null &&
                firstArrivalPresentationRoot.activeSelf != active)
            {
                firstArrivalPresentationRoot.SetActive(active);
            }
        }

        private void Fail(string reason)
        {
            string safeReason = reason ?? string.Empty;
            Debug.LogError(safeReason, this);
            HubEventUtility.Invoke(ArrivalFailed, safeReason, this);
            HubEventUtility.Invoke(arrivalFailed, safeReason, this);
        }
    }
}
