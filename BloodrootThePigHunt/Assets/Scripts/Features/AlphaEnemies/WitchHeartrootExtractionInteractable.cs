using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WitchHeartrootExtractionInteractable : MonoBehaviour, global::IInteract
    {
        [SerializeField] private WitchEncounterDirector encounterDirector;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private UnityEvent onExtractionAccepted = new UnityEvent();
        [SerializeField] private UnityEvent onExtractionRejected = new UnityEvent();

        private void Awake()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }

        public void SendInteract(Collider target)
        {
            TryExtract(target);
        }

        public bool TryExtract(Collider target)
        {
            GameObject requester = ResolveAuthoritativePlayer();
            bool accepted = IsAuthoredInteractionCollider(target) &&
                            IsPlayerObject(requester) &&
                            encounterDirector != null &&
                            encounterDirector.TryCompleteHeartrootExtraction(
                                requester);
            AlphaEnemyEventUtility.Invoke(
                accepted ? onExtractionAccepted : onExtractionRejected,
                this,
                accepted ? nameof(onExtractionAccepted) : nameof(onExtractionRejected));
            return accepted;
        }

        public void Configure(
            WitchEncounterDirector authoredEncounterDirector,
            string authoredPlayerTag = "Player")
        {
            Configure(
                authoredEncounterDirector,
                GetComponent<Collider>(),
                authoredPlayerTag);
        }

        public void Configure(
            WitchEncounterDirector authoredEncounterDirector,
            Collider authoredInteractionCollider,
            string authoredPlayerTag = "Player")
        {
            encounterDirector = authoredEncounterDirector;
            interactionCollider = authoredInteractionCollider != null
                ? authoredInteractionCollider
                : GetComponent<Collider>();
            playerTag = authoredPlayerTag?.Trim() ?? string.Empty;
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
                    "Heartroot extraction requires its exact enabled solid Interact/Interactable raycast collider.";
                return false;
            }

            if (encounterDirector == null ||
                !encounterDirector.ValidateRuntimeContract(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Heartroot extraction requires a valid witch encounter director."
                    : error;
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerTag))
            {
                error = "Heartroot extraction requires a Player tag.";
                return false;
            }

            error = string.Empty;
            return true;
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

        private void OnValidate()
        {
            playerTag = playerTag?.Trim() ?? string.Empty;
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }
    }
}
