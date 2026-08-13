using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bloodroot.Features.AlphaMenu
{
    /// <summary>
    /// Drives the authored full-screen menu curtain. The component only
    /// controls serialized UI and never creates a runtime canvas or image.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlphaMenuTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup curtainGroup;
        [SerializeField] private Image curtainImage;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

        private Coroutine activeRoutine;

        public CanvasGroup CurtainGroup => curtainGroup;

        public Image CurtainImage => curtainImage;

        public float FadeDuration => fadeDuration;

        public bool IsTransitioning => activeRoutine != null;

        private void Awake()
        {
            SetCurtainImmediate(0f, false);
        }

        public void Configure(
            CanvasGroup authoredCurtain,
            Image authoredImage,
            float authoredFadeDuration)
        {
            curtainGroup = authoredCurtain;
            curtainImage = authoredImage;
            fadeDuration = Mathf.Max(0f, authoredFadeDuration);
            SetCurtainImmediate(0f, false);
        }

        public bool ValidateAuthoredContract(out string error)
        {
            if (curtainGroup == null || curtainImage == null)
            {
                error = "The authored transition curtain is incomplete.";
                return false;
            }

            if (curtainGroup.gameObject != gameObject ||
                curtainImage.gameObject != gameObject)
            {
                error =
                    "The transition CanvasGroup, Image, and authority must " +
                    "share one authored root.";
                return false;
            }

            if (transform.parent != null)
            {
                error =
                    "The transition authority must be a scene root so it can " +
                    "cover an asynchronous scene activation.";
                return false;
            }

            if (fadeDuration < 0f)
            {
                error = "Transition fade duration cannot be negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TrySwapScreen(
            Action coveredAction,
            Action completedAction,
            Action<string> failedAction)
        {
            if (!CanStart(out string error))
            {
                failedAction?.Invoke(error);
                return false;
            }

            activeRoutine = StartCoroutine(SwapScreenRoutine(
                coveredAction,
                completedAction,
                failedAction));
            return true;
        }

        public bool TryLoadScene(
            string sceneName,
            Action<string> failedAction)
        {
            if (!CanStart(out string error))
            {
                failedAction?.Invoke(error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                failedAction?.Invoke("A destination scene is required.");
                return false;
            }

            activeRoutine = StartCoroutine(
                LoadSceneRoutine(sceneName, failedAction));
            return true;
        }

        public bool TryRunTerminalAction(
            float minimumDelay,
            Action terminalAction,
            Action<string> failedAction)
        {
            if (!CanStart(out string error))
            {
                failedAction?.Invoke(error);
                return false;
            }

            activeRoutine = StartCoroutine(TerminalActionRoutine(
                Mathf.Max(0f, minimumDelay),
                terminalAction,
                failedAction));
            return true;
        }

        private IEnumerator SwapScreenRoutine(
            Action coveredAction,
            Action completedAction,
            Action<string> failedAction)
        {
            SetCurtainBlocking(true);
            yield return FadeCurtain(1f);

            string failure = InvokeSafely(coveredAction);
            if (!string.IsNullOrEmpty(failure))
            {
                yield return RecoverFromFailure(failure, failedAction);
                yield break;
            }

            // Give the newly revealed authored hierarchy one covered frame to
            // finish its enable/focus callbacks before the curtain clears.
            yield return null;
            yield return FadeCurtain(0f);
            FinishClear();

            failure = InvokeSafely(completedAction);
            if (!string.IsNullOrEmpty(failure))
            {
                failedAction?.Invoke(failure);
            }
        }

        private IEnumerator LoadSceneRoutine(
            string sceneName,
            Action<string> failedAction)
        {
            SetCurtainBlocking(true);
            yield return FadeCurtain(1f);

            AsyncOperation operation = null;
            string loadFailure = string.Empty;
            try
            {
                operation = SceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                loadFailure =
                    $"Could not load scene '{sceneName}': " +
                    exception.Message;
            }

            if (!string.IsNullOrEmpty(loadFailure))
            {
                yield return RecoverFromFailure(loadFailure, failedAction);
                yield break;
            }

            if (operation == null)
            {
                yield return RecoverFromFailure(
                    $"Could not begin loading scene '{sceneName}'.",
                    failedAction);
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                yield return null;
            }

            // The curtain is authored as a scene root. Persist only after the
            // destination is ready, then reveal and remove it there.
            DontDestroyOnLoad(gameObject);
            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return FadeCurtain(0f);
            activeRoutine = null;
            Destroy(gameObject);
        }

        private IEnumerator TerminalActionRoutine(
            float minimumDelay,
            Action terminalAction,
            Action<string> failedAction)
        {
            float startedAt = Time.realtimeSinceStartup;
            SetCurtainBlocking(true);
            yield return FadeCurtain(1f);

            float remainingDelay = minimumDelay -
                                   (Time.realtimeSinceStartup - startedAt);
            if (remainingDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingDelay);
            }

            string failure = InvokeSafely(terminalAction);
            if (string.IsNullOrEmpty(failure))
            {
                // A standalone quit or Play Mode stop normally ends this
                // coroutine. Clearing here also makes test callbacks safe.
                yield return FadeCurtain(0f);
                FinishClear();
                yield break;
            }

            yield return RecoverFromFailure(failure, failedAction);
        }

        private IEnumerator RecoverFromFailure(
            string failure,
            Action<string> failedAction)
        {
            yield return FadeCurtain(0f);
            FinishClear();
            failedAction?.Invoke(failure);
        }

        private IEnumerator FadeCurtain(float targetAlpha)
        {
            float startAlpha = curtainGroup.alpha;
            float duration = Mathf.Max(0f, fadeDuration);
            if (duration <= 0f)
            {
                curtainGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                curtainGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            curtainGroup.alpha = targetAlpha;
        }

        private bool CanStart(out string error)
        {
            if (!enabled || !gameObject.activeInHierarchy)
            {
                error = "The authored menu transition is not active.";
                return false;
            }

            if (!ValidateAuthoredContract(out error))
            {
                return false;
            }

            if (activeRoutine != null)
            {
                error = "A menu transition is already in progress.";
                return false;
            }

            return true;
        }

        private void FinishClear()
        {
            SetCurtainImmediate(0f, false);
            activeRoutine = null;
        }

        private void SetCurtainBlocking(bool blocking)
        {
            curtainGroup.interactable = blocking;
            curtainGroup.blocksRaycasts = blocking;
        }

        private void SetCurtainImmediate(float alpha, bool blocking)
        {
            if (curtainGroup == null)
            {
                return;
            }

            curtainGroup.alpha = Mathf.Clamp01(alpha);
            SetCurtainBlocking(blocking);
        }

        private static string InvokeSafely(Action action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            try
            {
                action.Invoke();
                return string.Empty;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fadeDuration = Mathf.Max(0f, fadeDuration);
        }
#endif
    }
}
