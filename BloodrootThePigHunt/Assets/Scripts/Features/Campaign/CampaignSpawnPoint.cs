using System;
using System.Collections;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Named destination for scene travel. It can place the tagged player
    /// automatically or be called directly by an existing player bootstrap.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class CampaignSpawnPoint : MonoBehaviour
    {
        [SerializeField]
        private string destinationId = string.Empty;

        [SerializeField]
        private Transform arrivalTransform;

        [SerializeField]
        private bool automaticallyPlacePlayer = true;

        [SerializeField]
        private string playerTag = "Player";

        [SerializeField, Min(0f)]
        private float playerLookupTimeout = 5f;

        public string DestinationId => destinationId;

        public event Action<Transform> PlayerPlaced;

        private IEnumerator Start()
        {
            // Let every scene-authored player bootstrap finish its own Start
            // before applying the durable campaign arrival. In particular,
            // this prevents a player's default spawn from overwriting the
            // named campaign destination later in the same startup frame.
            yield return null;

            if (!automaticallyPlacePlayer ||
                !HasMatchingPendingSpawn())
            {
                yield break;
            }

            float deadline = Time.unscaledTime + playerLookupTimeout;

            do
            {
                GameObject player = FindTaggedPlayer();

                if (player != null)
                {
                    TryPlacePlayer(player.transform);
                    yield break;
                }

                yield return null;
            }
            while (Time.unscaledTime <= deadline);

            Debug.LogWarning(
                $"Campaign spawn '{destinationId}' could not find a player " +
                $"tagged '{playerTag}' within {playerLookupTimeout:0.##} seconds.",
                this);
        }

        public bool HasMatchingPendingSpawn()
        {
            CampaignStateService stateService = CampaignStateService.Instance;

            return stateService != null &&
                   stateService.HasPendingSpawn(
                       gameObject.scene.name,
                       destinationId);
        }

        public bool TryPlacePlayer(Transform player)
        {
            CampaignStateService stateService = CampaignStateService.Instance;

            if (player == null || stateService == null ||
                !stateService.HasPendingSpawn(
                    gameObject.scene.name,
                    destinationId))
            {
                return false;
            }

            Transform destination =
                arrivalTransform != null ? arrivalTransform : transform;

            CharacterController characterController =
                player.GetComponent<CharacterController>();
            bool controllerWasEnabled =
                characterController != null && characterController.enabled;

            try
            {
                if (controllerWasEnabled)
                {
                    characterController.enabled = false;
                }

                player.SetPositionAndRotation(
                    destination.position,
                    destination.rotation);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Campaign spawn '{destinationId}' could not place the " +
                    $"player: {exception.Message}",
                    this);
                return false;
            }
            finally
            {
                if (controllerWasEnabled && characterController != null)
                {
                    characterController.enabled = true;
                }
            }

            // Clear the durable handoff only after the player was placed. If
            // saving this acknowledgement fails, the pending destination is
            // retained so a later scene entry can safely retry it.
            if (!stateService.CompletePendingSpawn(
                    gameObject.scene.name,
                    destinationId))
            {
                return false;
            }

            CampaignEventUtility.Invoke(PlayerPlaced, player, this);
            return true;
        }

        public void Configure(
            string id,
            Transform destination,
            bool placePlayerAutomatically)
        {
            destinationId = id?.Trim() ?? string.Empty;
            arrivalTransform = destination;
            automaticallyPlacePlayer = placePlayerAutomatically;
        }

        private GameObject FindTaggedPlayer()
        {
            if (string.IsNullOrWhiteSpace(playerTag))
            {
                return null;
            }

            try
            {
                return GameObject.FindGameObjectWithTag(playerTag);
            }
            catch (UnityException exception)
            {
                Debug.LogError(
                    $"Campaign spawn player tag '{playerTag}' is invalid: " +
                    exception.Message,
                    this);
                return null;
            }
        }

        private void OnValidate()
        {
            destinationId = destinationId?.Trim() ?? string.Empty;
            playerTag = playerTag?.Trim() ?? string.Empty;
            playerLookupTimeout = Mathf.Max(0f, playerLookupTimeout);
        }

        private void OnDrawGizmosSelected()
        {
            Transform destination =
                arrivalTransform != null ? arrivalTransform : transform;
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.18f, 0.75f, 0.95f, 0.9f);
            Gizmos.DrawWireSphere(destination.position, 0.5f);
            Gizmos.DrawRay(
                destination.position,
                destination.forward * 1.5f);
            Gizmos.color = previousColor;
        }
    }
}
