using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bloodroot.Features.AlphaMenu
{
    /// <summary>
    /// Gives an authored menu a deterministic initial selection whenever the
    /// menu becomes active. No EventSystem or UI object is created at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlphaMenuFocusOnEnable : MonoBehaviour
    {
        [SerializeField] private Button firstSelectedButton;

        private Coroutine deferredSelection;

        public Button FirstSelectedButton => firstSelectedButton;

        private void OnEnable()
        {
            SelectFirstAvailable();
            deferredSelection = StartCoroutine(SelectNextFrame());
        }

        private void OnDisable()
        {
            if (deferredSelection != null)
            {
                StopCoroutine(deferredSelection);
                deferredSelection = null;
            }
        }

        public void Configure(Button firstButton)
        {
            firstSelectedButton = firstButton;
        }

        public void SelectFirstAvailable()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null ||
                firstSelectedButton == null ||
                !firstSelectedButton.IsActive() ||
                !firstSelectedButton.IsInteractable())
            {
                return;
            }

            eventSystem.SetSelectedGameObject(
                firstSelectedButton.gameObject);
        }

        private IEnumerator SelectNextFrame()
        {
            yield return null;
            deferredSelection = null;
            SelectFirstAvailable();
        }
    }
}
