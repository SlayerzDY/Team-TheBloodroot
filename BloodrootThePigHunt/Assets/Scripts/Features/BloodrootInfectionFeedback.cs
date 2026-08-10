using UnityEngine;

namespace Bloodroot.Features.Infection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BloodRootInfectionController))]
    public sealed class BloodrootInfectionFeedback : MonoBehaviour
    {
        [SerializeField] private BloodRootInfectionController infectionController;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CanvasGroup distortionOverlay;
        [SerializeField] private AudioSource heartbeatSource;

        [Header("Effect Strength")]
        [SerializeField, Range(0f, 1f)] private float maximumOverlayAlpha = 0.55f;
        [SerializeField, Min(0f)] private float maximumFovPulse = 3f;
        [SerializeField, Min(0f)] private float pulseSpeed = 3f;

        private float normalFov;
        private float normalHeartbeatVolume;
        private float normalHeartbeatPitch;
        private float infectionAmount;

        private void Reset()
        {
            infectionController = GetComponent<BloodRootInfectionController>();
            targetCamera = GetComponentInChildren<Camera>();
        }

        private void Awake()
        {
            if (infectionController == null)
            {
                infectionController = GetComponent<BloodRootInfectionController>();
            }

            if (targetCamera != null)
            {
                normalFov = targetCamera.fieldOfView;
            }

            if (heartbeatSource != null)
            {
                normalHeartbeatVolume = heartbeatSource.volume;
                normalHeartbeatPitch = heartbeatSource.pitch;
            }

            if (distortionOverlay != null)
            {
                distortionOverlay.blocksRaycasts = false;
                distortionOverlay.interactable = false;
            }
        }

        private void OnEnable()
        {
            if (infectionController == null)
            {
                infectionController = GetComponent<BloodRootInfectionController>();
            }

            if (infectionController == null)
                return;

            infectionController.InfectionChanged += SetInfectionAmount;
            SetInfectionAmount(infectionController.NormalizedInfection);
        }

        private void OnDisable()
        {
            if (infectionController != null)
            {
                infectionController.InfectionChanged -= SetInfectionAmount;
            }

            RestoreDefaults();
        }

        private void Update()
        {
            float strength = infectionAmount * infectionAmount;
            float pulse = Mathf.Sin(Time.time * pulseSpeed);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = normalFov + pulse * maximumFovPulse * strength;
            }

            if (distortionOverlay != null)
            {
                distortionOverlay.alpha = maximumOverlayAlpha * strength;
            }

            if (heartbeatSource != null && heartbeatSource.clip != null)
            {
                heartbeatSource.volume = normalHeartbeatVolume * infectionAmount;
                heartbeatSource.pitch = Mathf.Lerp(
                    normalHeartbeatPitch,
                    normalHeartbeatPitch * 1.4f,
                    infectionAmount);

                if (infectionAmount > 0.05f && !heartbeatSource.isPlaying)
                {
                    heartbeatSource.loop = true;
                    heartbeatSource.Play();
                }
                else if (infectionAmount <= 0.05f && heartbeatSource.isPlaying)
                {
                    heartbeatSource.Stop();
                }
            }
        }

        private void SetInfectionAmount(float amount)
        {
            infectionAmount = Mathf.Clamp01(amount);
        }

        private void RestoreDefaults()
        {
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = normalFov;
            }

            if (distortionOverlay != null)
            {
                distortionOverlay.alpha = 0f;
            }

            if (heartbeatSource != null)
            {
                heartbeatSource.Stop();
                heartbeatSource.volume = normalHeartbeatVolume;
                heartbeatSource.pitch = normalHeartbeatPitch;
            }
        }
    }
}
