using System;
using TMPro;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Rotates an authored compass dial from the player's true world heading.
    /// Bloodroot's fixed convention is +Z north and +X east. The active
    /// objective diamond uses the same convention and reports live bearing and
    /// distance, so written directions and the HUD agree.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class CampaignCompassHUD : MonoBehaviour
    {
        private const float MinimumForwardSqrMagnitude = 0.0001f;

        [Header("Heading Source")]
        [SerializeField] private Transform playerHeading;
        [SerializeField, Min(0.1f)] private float reacquireIntervalSeconds =
            0.5f;

        [Header("Objective Guidance")]
        [SerializeField] private CampaignObjectiveGuidance guidance;
        [SerializeField] private bool autoResolveGuidance = true;

        [Header("Authored HUD")]
        [SerializeField] private RectTransform compassDial;
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private RectTransform[] uprightCardinalLabels =
            Array.Empty<RectTransform>();
        [SerializeField] private RectTransform objectiveIndicator;
        [SerializeField] private TMP_Text objectiveLabel;

        [Header("Presentation")]
        [SerializeField, Min(0f)] private float smoothingSeconds = 0.08f;
        [SerializeField, Min(1f)] private float objectiveIndicatorRadius = 46f;

        private float displayedHeading;
        private float headingVelocity;
        private float nextReacquireTime;
        private float nextGuidanceLookupTime;
        private bool hasHeading;
        private CampaignObjectiveGuidance boundGuidance;

        public Transform PlayerHeading => playerHeading;
        public RectTransform CompassDial => compassDial;
        public TMP_Text HeadingLabel => headingLabel;
        public CampaignObjectiveGuidance Guidance => guidance;
        public float DisplayedHeading => Mathf.Repeat(displayedHeading, 360f);
        public float ObjectiveIndicatorRadius => objectiveIndicatorRadius;
        public static Vector3 TrueNorth => Vector3.forward;

        private void OnEnable()
        {
            TryResolveGuidance(force: true);
            BindGuidance();
            hasHeading = false;
            headingVelocity = 0f;
            nextReacquireTime = 0f;

            if (playerHeading == null)
                TryResolvePlayerHeading(force: true);

            if (playerHeading != null)
            {
                displayedHeading = CalculateTrueHeading(
                    playerHeading.forward);
                hasHeading = true;
            }

            ApplyHeading(displayedHeading);
            RefreshObjectivePresentation();
        }

        private void OnDisable()
        {
            UnbindGuidance();
        }

        private void LateUpdate()
        {
            TryResolveGuidance(force: false);
            if (!TryResolvePlayerHeading(force: false))
                return;

            Vector3 flatForward = playerHeading.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < MinimumForwardSqrMagnitude)
                return;

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
            RefreshObjectivePresentation();
        }

        private void OnValidate()
        {
            reacquireIntervalSeconds = Mathf.Max(0.1f,
                reacquireIntervalSeconds);
            smoothingSeconds = Mathf.Max(0f, smoothingSeconds);
            objectiveIndicatorRadius = Mathf.Max(1f,
                objectiveIndicatorRadius);
            uprightCardinalLabels ??= Array.Empty<RectTransform>();
        }

        public void Configure(
            Transform authoredPlayerHeading,
            RectTransform authoredCompassDial,
            TMP_Text authoredHeadingLabel,
            RectTransform[] authoredUprightCardinalLabels,
            float authoredSmoothingSeconds = 0.08f,
            float authoredReacquireIntervalSeconds = 0.5f)
        {
            Configure(
                null,
                authoredPlayerHeading,
                authoredCompassDial,
                authoredHeadingLabel,
                authoredUprightCardinalLabels,
                null,
                null,
                authoredSmoothingSeconds,
                authoredReacquireIntervalSeconds,
                46f);
        }

        public void Configure(
            CampaignObjectiveGuidance authoredGuidance,
            Transform authoredPlayerHeading,
            RectTransform authoredCompassDial,
            TMP_Text authoredHeadingLabel,
            RectTransform[] authoredUprightCardinalLabels,
            RectTransform authoredObjectiveIndicator,
            TMP_Text authoredObjectiveLabel,
            float authoredSmoothingSeconds = 0.08f,
            float authoredReacquireIntervalSeconds = 0.5f,
            float authoredObjectiveIndicatorRadius = 46f)
        {
            UnbindGuidance();
            guidance = authoredGuidance;
            autoResolveGuidance = authoredGuidance == null;
            playerHeading = authoredPlayerHeading;
            compassDial = authoredCompassDial;
            headingLabel = authoredHeadingLabel;
            uprightCardinalLabels = authoredUprightCardinalLabels ??
                Array.Empty<RectTransform>();
            objectiveIndicator = authoredObjectiveIndicator;
            objectiveLabel = authoredObjectiveLabel;
            smoothingSeconds = Mathf.Max(0f, authoredSmoothingSeconds);
            reacquireIntervalSeconds = Mathf.Max(
                0.1f,
                authoredReacquireIntervalSeconds);
            objectiveIndicatorRadius = Mathf.Max(
                1f,
                authoredObjectiveIndicatorRadius);

            displayedHeading = 0f;
            headingVelocity = 0f;
            hasHeading = false;
            BindGuidance();
            ApplyHeading(0f);
            RefreshObjectivePresentation();
        }

        public bool ValidateConfiguration(out string failureReason)
        {
            if (guidance == null && !autoResolveGuidance)
            {
                failureReason =
                    "Campaign compass requires objective guidance.";
                return false;
            }

            if (compassDial == null || headingLabel == null)
            {
                failureReason =
                    "Campaign compass requires an authored dial and heading label.";
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
                        "Every compass cardinal label must be authored under the rotating dial.";
                    return false;
                }
            }

            if (objectiveIndicator == null ||
                !objectiveIndicator.IsChildOf(compassDial) ||
                objectiveLabel == null)
            {
                failureReason =
                    "Campaign compass requires an objective diamond on the dial and a target label.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public static float CalculateTrueHeading(Vector3 worldForward)
        {
            worldForward.y = 0f;
            if (worldForward.sqrMagnitude < MinimumForwardSqrMagnitude)
                return 0f;

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
                Mathf.Repeat(headingDegrees, 360f) / 45f) %
                cardinals.Length;
            return cardinals[index];
        }

        public void ApplyHeadingForPreview(float headingDegrees)
        {
            displayedHeading = Mathf.Repeat(headingDegrees, 360f);
            headingVelocity = 0f;
            hasHeading = true;
            ApplyHeading(displayedHeading);
            RefreshObjectivePresentation();
        }

        private bool TryResolvePlayerHeading(bool force)
        {
            if (playerHeading != null)
                return true;

            float now = Time.unscaledTime;
            if (!force && now < nextReacquireTime)
                return false;

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

            try
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(
                    "Player");
                if (taggedPlayer != null)
                    playerHeading = taggedPlayer.transform;
            }
            catch (UnityException)
            {
                playerHeading = null;
            }

            return playerHeading != null;
        }

        private void ApplyHeading(float headingDegrees)
        {
            float safeHeading = Mathf.Repeat(headingDegrees, 360f);
            if (compassDial != null)
            {
                compassDial.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    safeHeading);
            }

            if (uprightCardinalLabels != null)
            {
                foreach (RectTransform cardinalLabel in uprightCardinalLabels)
                {
                    if (cardinalLabel != null)
                    {
                        cardinalLabel.localRotation = Quaternion.Euler(
                            0f,
                            0f,
                            -safeHeading);
                    }
                }
            }

            if (objectiveIndicator != null)
            {
                objectiveIndicator.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    45f - safeHeading);
            }

            if (headingLabel != null)
            {
                int roundedHeading = Mathf.RoundToInt(safeHeading) % 360;
                headingLabel.text =
                    $"{GetCardinalHeading(safeHeading)}  {roundedHeading:000}\u00b0";
            }
        }

        private void RefreshObjectivePresentation()
        {
            TryResolveGuidance(force: false);
            Transform target = guidance != null
                ? guidance.CurrentTarget
                : null;
            bool hasObjective = target != null && playerHeading != null;
            if (objectiveIndicator != null &&
                objectiveIndicator.gameObject.activeSelf != hasObjective)
            {
                objectiveIndicator.gameObject.SetActive(hasObjective);
            }

            if (!hasObjective)
            {
                if (objectiveLabel != null)
                    objectiveLabel.text = "NO ACTIVE WAYPOINT";
                return;
            }

            Vector3 offset = target.position - playerHeading.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            float bearing = CalculateTrueHeading(offset);
            float radians = bearing * Mathf.Deg2Rad;
            objectiveIndicator.anchoredPosition = new Vector2(
                Mathf.Sin(radians) * objectiveIndicatorRadius,
                Mathf.Cos(radians) * objectiveIndicatorRadius);

            if (objectiveLabel != null)
            {
                string targetName = string.IsNullOrWhiteSpace(
                    guidance.CurrentTargetLabel)
                    ? "OBJECTIVE"
                    : guidance.CurrentTargetLabel.ToUpperInvariant();
                objectiveLabel.text =
                    $"{targetName}  |  " +
                    $"{GetCardinalHeading(bearing)}  |  {distance:0} m";
            }
        }

        private void HandleTargetChanged(CampaignObjectiveGuidance _)
        {
            RefreshObjectivePresentation();
        }

        private void BindGuidance()
        {
            if (boundGuidance == guidance)
                return;

            UnbindGuidance();
            boundGuidance = guidance;
            if (isActiveAndEnabled && boundGuidance != null)
                boundGuidance.TargetChanged += HandleTargetChanged;
        }

        private void UnbindGuidance()
        {
            if (boundGuidance != null)
                boundGuidance.TargetChanged -= HandleTargetChanged;

            boundGuidance = null;
        }

        private void TryResolveGuidance(bool force)
        {
            if (!Application.isPlaying || guidance != null ||
                !autoResolveGuidance)
                return;

            float now = Time.unscaledTime;
            if (!force && now < nextGuidanceLookupTime)
                return;

            nextGuidanceLookupTime = now + reacquireIntervalSeconds;
            guidance = FindAnyObjectByType<CampaignObjectiveGuidance>();
            BindGuidance();
        }
    }
}
