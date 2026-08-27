using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Moves one authored emissive diamond to the active campaign waypoint.
    /// The marker spins, bobs, pulses, and grows with viewing distance while
    /// remaining a presentation-only object with no gameplay collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignObjectiveWorldMarker : MonoBehaviour
    {
        [SerializeField] private CampaignObjectiveGuidance guidance;
        [SerializeField] private GameObject markerPresentation;
        [SerializeField] private Transform spinningDiamond;
        [SerializeField] private Light markerLight;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 105f;
        [SerializeField, Min(0f)] private float hoverAmplitude = 0.28f;
        [SerializeField, Min(0f)] private float hoverCyclesPerSecond = 0.7f;
        [SerializeField, Min(0f)] private float pulseAmount = 0.12f;

        [Header("Visibility")]
        [SerializeField] private Vector3 baseWorldScale =
            new Vector3(0.54f, 0.76f, 0.54f);
        [SerializeField, Min(1f)] private float maximumDistanceScale = 4.5f;
        [SerializeField, Min(1f)] private float distanceForDoubleScale = 150f;
        [SerializeField, Min(0.1f)] private float baseLightIntensity = 7f;

        private Transform player;
        private float nextPlayerLookupTime;

        public CampaignObjectiveGuidance Guidance => guidance;
        public GameObject MarkerPresentation => markerPresentation;
        public Transform SpinningDiamond => spinningDiamond;
        public float HoverAmplitude => hoverAmplitude;
        public Vector3 BaseWorldScale => baseWorldScale;
        public float MaximumDistanceScale => maximumDistanceScale;

        private void OnEnable()
        {
            if (guidance != null)
                guidance.TargetChanged += HandleTargetChanged;

            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (guidance != null)
                guidance.TargetChanged -= HandleTargetChanged;
        }

        private void LateUpdate()
        {
            Transform target = guidance != null
                ? guidance.CurrentTarget
                : null;
            if (target == null)
            {
                SetPresentationActive(false);
                return;
            }

            SetPresentationActive(true);

            float time = Time.unscaledTime;
            float hover = Mathf.Sin(
                time * Mathf.PI * 2f * hoverCyclesPerSecond) *
                hoverAmplitude;
            markerPresentation.transform.position =
                target.position + Vector3.up * hover;

            float spin = Mathf.Repeat(
                time * spinDegreesPerSecond,
                360f);
            spinningDiamond.localRotation = Quaternion.Euler(
                12f,
                spin,
                12f);

            float pulse = 1f + Mathf.Sin(time * Mathf.PI * 2f) * pulseAmount;
            float distanceScale = ResolveDistanceScale(target.position);
            spinningDiamond.localScale =
                baseWorldScale * (pulse * distanceScale);

            if (markerLight != null)
            {
                markerLight.intensity = baseLightIntensity *
                    (1f + Mathf.Sin(time * Mathf.PI * 2f) * 0.18f);
            }
        }

        private void OnValidate()
        {
            spinDegreesPerSecond = Mathf.Max(0f, spinDegreesPerSecond);
            hoverAmplitude = Mathf.Max(0f, hoverAmplitude);
            hoverCyclesPerSecond = Mathf.Max(0f, hoverCyclesPerSecond);
            pulseAmount = Mathf.Max(0f, pulseAmount);
            maximumDistanceScale = Mathf.Max(1f, maximumDistanceScale);
            distanceForDoubleScale = Mathf.Max(1f, distanceForDoubleScale);
            baseLightIntensity = Mathf.Max(0.1f, baseLightIntensity);
        }

        public void Configure(
            CampaignObjectiveGuidance authoredGuidance,
            GameObject authoredMarkerPresentation,
            Transform authoredSpinningDiamond,
            Light authoredMarkerLight)
        {
            if (Application.isPlaying && isActiveAndEnabled && guidance != null)
                guidance.TargetChanged -= HandleTargetChanged;

            guidance = authoredGuidance;
            markerPresentation = authoredMarkerPresentation;
            spinningDiamond = authoredSpinningDiamond;
            markerLight = authoredMarkerLight;

            if (Application.isPlaying && isActiveAndEnabled && guidance != null)
                guidance.TargetChanged += HandleTargetChanged;

            RefreshVisibility();
        }

        public void ConfigurePresentation(
            Vector3 authoredBaseWorldScale,
            float authoredHoverAmplitude,
            float authoredMaximumDistanceScale)
        {
            baseWorldScale = new Vector3(
                Mathf.Max(0.01f, authoredBaseWorldScale.x),
                Mathf.Max(0.01f, authoredBaseWorldScale.y),
                Mathf.Max(0.01f, authoredBaseWorldScale.z));
            hoverAmplitude = Mathf.Max(0f, authoredHoverAmplitude);
            maximumDistanceScale = Mathf.Max(
                1f,
                authoredMaximumDistanceScale);
        }

        public bool ValidateConfiguration(out string failureReason)
        {
            if (guidance == null || markerPresentation == null ||
                spinningDiamond == null || markerLight == null)
            {
                failureReason =
                    "The world objective marker requires guidance, presentation, diamond, and light references.";
                return false;
            }

            if (!spinningDiamond.IsChildOf(markerPresentation.transform))
            {
                failureReason =
                    "The spinning objective diamond must be inside its presentation root.";
                return false;
            }

            if (spinningDiamond.GetComponentInChildren<Collider>(true) != null)
            {
                failureReason =
                    "The objective marker is presentation-only and cannot own a collider.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private void HandleTargetChanged(CampaignObjectiveGuidance _)
        {
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            SetPresentationActive(
                guidance != null && guidance.CurrentTarget != null);
        }

        private void SetPresentationActive(bool active)
        {
            if (markerPresentation != null &&
                markerPresentation.activeSelf != active)
            {
                markerPresentation.SetActive(active);
            }
        }

        private float ResolveDistanceScale(Vector3 targetPosition)
        {
            ResolvePlayer();
            if (player == null)
                return 1f;

            float distance = Vector3.Distance(player.position, targetPosition);
            return Mathf.Clamp(
                1f + distance / distanceForDoubleScale,
                1f,
                maximumDistanceScale);
        }

        private void ResolvePlayer()
        {
            if (player != null || Time.unscaledTime < nextPlayerLookupTime)
                return;

            nextPlayerLookupTime = Time.unscaledTime + 0.5f;
            gameManager manager = gameManager.instance;
            if (manager != null)
            {
                if (manager.playerTransform != null)
                {
                    player = manager.playerTransform;
                    return;
                }

                if (manager.player != null)
                {
                    player = manager.player.transform;
                    return;
                }
            }

            try
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(
                    "Player");
                if (taggedPlayer != null)
                    player = taggedPlayer.transform;
            }
            catch (UnityException)
            {
                player = null;
            }
        }
    }
}
