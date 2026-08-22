using System;
using TMPro;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Keeps an authored HUD compass aligned to the player's horizontal
    /// heading. Bloodroot's world convention is fixed: +Z is true north and
    /// +X is east. The dial moves beneath a fixed heading marker, so the north
    /// mark always represents the same world direction instead of drifting
    /// with a camera-relative or scene-relative offset.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class CampaignCompassHUD : MonoBehaviour
    {
        private const float MinimumForwardSqrMagnitude = 0.0001f;

        [Header("Heading Source")]
        [Tooltip("Optional authored override. When empty, the active Player is resolved through gameManager and then the Player tag.")]
        [SerializeField] private Transform playerHeading;
        [SerializeField, Min(0.1f)] private float reacquireIntervalSeconds = 0.5f;

        [Header("Authored HUD")]
        [Tooltip("The N/E/S/W dial. It rotates while the fixed player-heading marker stays upright.")]
        [SerializeField] private RectTransform compassDial;
        [SerializeField] private TMP_Text headingLabel;
        [Tooltip("Cardinal labels that should remain readable while their positions rotate with the dial.")]
        [SerializeField] private RectTransform[] uprightCardinalLabels =
            Array.Empty<RectTransform>();

        [Header("Presentation")]
        [SerializeField, Min(0f)] private float smoothingSeconds = 0.08f;

        private float displayedHeading;
        private float headingVelocity;
        private float nextReacquireTime;
        private bool hasHeading;

        public Transform PlayerHeading => playerHeading;
        public RectTransform CompassDial => compassDial;
        public TMP_Text HeadingLabel => headingLabel;
        public float DisplayedHeading => Mathf.Repeat(displayedHeading, 360f);
        public static Vector3 TrueNorth => Vector3.forward;

        private void OnEnable()
        {
            hasHeading = false;
            headingVelocity = 0f;
            nextReacquireTime = 0f;

            if (playerHeading == null)
            {
                TryResolvePlayerHeading(force: true);
            }

            if (playerHeading != null)
            {
                displayedHeading = CalculateTrueHeading(playerHeading.forward);
                hasHeading = true;
            }

            ApplyHeading(displayedHeading);
        }

        private void LateUpdate()
        {
            if (!TryResolvePlayerHeading(force: false))
            {
                return;
            }

            Vector3 flatForward = playerHeading.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < MinimumForwardSqrMagnitude)
            {
                return;
            }

            float targetHeading = CalculateTrueHeading(flatForward);
            if (!hasHeading || smoothingSeconds <= 0f)
            {
                displayedHeading = targetHeading;
                headingVelocity = 0f;
                hasHeading = true;
            }
            else
            {
                displayedHeading = Mathf.SmoothDampAngle(
                    displayedHeading,
                    targetHeading,
                    ref headingVelocity,
                    smoothingSeconds,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            ApplyHeading(displayedHeading);
        }

        private void OnValidate()
        {
            reacquireIntervalSeconds = Mathf.Max(0.1f, reacquireIntervalSeconds);
            smoothingSeconds = Mathf.Max(0f, smoothingSeconds);
            uprightCardinalLabels ??= Array.Empty<RectTransform>();
        }

        /// <summary>
        /// Used by deterministic prefab authoring. A null player is valid;
        /// the production HUD resolves the current scene's Player at runtime.
        /// </summary>
        public void Configure(
            Transform authoredPlayerHeading,
            RectTransform authoredCompassDial,
            TMP_Text authoredHeadingLabel,
            RectTransform[] authoredUprightCardinalLabels,
            float authoredSmoothingSeconds = 0.08f,
            float authoredReacquireIntervalSeconds = 0.5f)
        {
            playerHeading = authoredPlayerHeading;
            compassDial = authoredCompassDial;
            headingLabel = authoredHeadingLabel;
            uprightCardinalLabels = authoredUprightCardinalLabels ??
                Array.Empty<RectTransform>();
            smoothingSeconds = Mathf.Max(0f, authoredSmoothingSeconds);
            reacquireIntervalSeconds = Mathf.Max(
                0.1f,
                authoredReacquireIntervalSeconds);

            displayedHeading = 0f;
            headingVelocity = 0f;
            hasHeading = false;
            ApplyHeading(0f);
        }

        public bool ValidateConfiguration(out string failureReason)
        {
            if (compassDial == null)
            {
                failureReason = "Campaign compass requires an authored rotating dial.";
                return false;
            }

            if (headingLabel == null)
            {
                failureReason = "Campaign compass requires an authored heading label.";
                return false;
            }

            if (uprightCardinalLabels == null ||
                uprightCardinalLabels.Length != 4)
            {
                failureReason =
                    "Campaign compass requires exactly four N/E/S/W labels.";
                return false;
            }

            foreach (RectTransform cardinalLabel in uprightCardinalLabels)
            {
                if (cardinalLabel == null ||
                    !cardinalLabel.IsChildOf(compassDial))
                {
                    failureReason =
                        "Every campaign compass cardinal label must be authored under the rotating dial.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns clockwise degrees from world +Z. North is 0, east is 90,
        /// south is 180, and west is 270.
        /// </summary>
        public static float CalculateTrueHeading(Vector3 worldForward)
        {
            worldForward.y = 0f;
            if (worldForward.sqrMagnitude < MinimumForwardSqrMagnitude)
            {
                return 0f;
            }

            worldForward.Normalize();
            return Mathf.Repeat(
                Mathf.Atan2(worldForward.x, worldForward.z) * Mathf.Rad2Deg,
                360f);
        }

        public static string GetCardinalHeading(float headingDegrees)
        {
            string[] cardinals =
            {
                "N", "NE", "E", "SE", "S", "SW", "W", "NW"
            };
            int index = Mathf.RoundToInt(
                Mathf.Repeat(headingDegrees, 360f) / 45f) % cardinals.Length;
            return cardinals[index];
        }

        /// <summary>
        /// Editor-preview and regression-test hook. Production input still
        /// comes exclusively from the resolved Player transform.
        /// </summary>
        public void ApplyHeadingForPreview(float headingDegrees)
        {
            displayedHeading = Mathf.Repeat(headingDegrees, 360f);
            headingVelocity = 0f;
            hasHeading = true;
            ApplyHeading(displayedHeading);
        }

        private bool TryResolvePlayerHeading(bool force)
        {
            if (playerHeading != null)
            {
                return true;
            }

            float now = Time.unscaledTime;
            if (!force && now < nextReacquireTime)
            {
                return false;
            }

            nextReacquireTime = now + reacquireIntervalSeconds;

            gameManager manager = gameManager.instance;
            if (manager != null)
            {
                if (manager.playerTransform != null)
                {
                    playerHeading = manager.playerTransform;
                    return true;
                }

                if (manager.player != null)
                {
                    playerHeading = manager.player.transform;
                    return true;
                }
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerHeading = taggedPlayer.transform;
            }

            return playerHeading != null;
        }

        private void ApplyHeading(float headingDegrees)
        {
            float safeHeading = Mathf.Repeat(headingDegrees, 360f);

            if (compassDial != null)
            {
                // Positive UI Z rotation moves the north mark left when the
                // player turns east, which is the correct relative bearing.
                compassDial.localRotation = Quaternion.Euler(0f, 0f, safeHeading);
            }

            if (uprightCardinalLabels != null)
            {
                foreach (RectTransform cardinalLabel in uprightCardinalLabels)
                {
                    if (cardinalLabel != null)
                    {
                        cardinalLabel.localRotation =
                            Quaternion.Euler(0f, 0f, -safeHeading);
                    }
                }
            }

            if (headingLabel != null)
            {
                int roundedHeading =
                    Mathf.RoundToInt(safeHeading) % 360;
                headingLabel.text =
                    $"{GetCardinalHeading(safeHeading)}  {roundedHeading:000}\u00b0";
            }
        }
    }
}
