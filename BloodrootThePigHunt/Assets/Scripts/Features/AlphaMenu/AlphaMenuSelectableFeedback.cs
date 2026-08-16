using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bloodroot.Features.AlphaMenu
{
    /// <summary>
    /// Adds pointer-hover and keyboard-selection feedback to an authored
    /// Selectable. Button actions remain responsible for confirm/cancel SFX.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class AlphaMenuSelectableFeedback : MonoBehaviour,
        IPointerEnterHandler,
        ISelectHandler
    {
        [SerializeField] private AlphaMenuAudio menuAudio;

        private Selectable selectable;
        private bool pointerIsSelecting;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();

            if (AlphaMenuAudio.Instance != null)
            {
                menuAudio = AlphaMenuAudio.Instance;
            }
        }

        public void Configure(AlphaMenuAudio audio)
        {
            menuAudio = audio;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanRespond())
            {
                return;
            }

            menuAudio.PlayPointerHover();

            EventSystem currentEventSystem = EventSystem.current;
            if (currentEventSystem == null ||
                currentEventSystem.currentSelectedGameObject == gameObject)
            {
                return;
            }

            // Pointer focus and keyboard focus intentionally stay in sync.
            // Suppress the selection clip for this pointer-driven change so a
            // hover never produces two sounds.
            pointerIsSelecting = true;
            currentEventSystem.SetSelectedGameObject(gameObject, eventData);
            pointerIsSelecting = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!pointerIsSelecting && CanRespond())
            {
                menuAudio.PlaySelectionChanged();
            }
        }

        private bool CanRespond()
        {
            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }

            if (AlphaMenuAudio.Instance != null)
            {
                menuAudio = AlphaMenuAudio.Instance;
            }

            return menuAudio != null &&
                   selectable != null &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }
    }
}
