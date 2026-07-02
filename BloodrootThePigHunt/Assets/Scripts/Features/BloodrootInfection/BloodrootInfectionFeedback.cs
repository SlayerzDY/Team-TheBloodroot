using UnityEngine;

namespace Bloodroot.Features.Infection
{
    /// <summary>
    /// Optional presentation layer for the infection meter. Every reference except
    /// the controller is optional, allowing audio and visuals to be added gradually.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BloodrootInfectionController))]
    public sealed class BloodrootInfectionFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BloodrootInfectionController infectionController;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform weaponRoot;
        [SerializeField] private CanvasGroup distortionOverlay;
        [SerializeField] private AudioSource heartbeatSource;
        [SerializeField] private AudioSource hallucinationSource;

        [Header("Visual Distortion")]
        [SerializeField, Range(0f, 1f)] private float maximumOverlayAlpha = 0.55f;
        [SerializeField, Min(0f)] private float maximumFovPulse = 3f;
        [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;
        [SerializeField, Min(0f)] private float maximumWeaponSway = 5f;
        [SerializeField, Min(0f)] private float feedbackResponse = 5f;

        [Header("Audio Hallucinations")]
        [SerializeField, Range(0f, 1f)] private float fakeCallThreshold = 0.5f;
        [SerializeField] private AudioClip[] fakePigCalls;
        [SerializeField] private Vector2 fakeCallDelay = new Vector2(4f, 9f);
        [SerializeField] private Vector2 fakeCallDistance = new Vector2(5f, 14f);

        private float targetSeverity;
        private float displayedSeverity;
        private float baseCameraFov;
        private float baseHeartbeatVolume;
        private float baseHeartbeatPitch;
        private float nextFakeCallTime;
        private Quaternion baseWeaponRotation;

        private void Reset()
        {
            infectionController = GetComponent<BloodrootInfectionController>();
            targetCamera = GetComponentInChildren<Camera>();
        }

        private void Awake()
        {
            if (infectionController == null)
            {
                infectionController = GetComponent<BloodrootInfectionController>();
            }

            if (targetCamera != null)
            {
                baseCameraFov = targetCamera.fieldOfView;
            }

            if (weaponRoot != null)
            {
                baseWeaponRotation = weaponRoot.localRotation;
            }

            if (heartbeatSource != null)
            {
                baseHeartbeatVolume = heartbeatSource.volume;
                baseHeartbeatPitch = heartbeatSource.pitch;
            }

            if (distortionOverlay != null)
            {
                distortionOverlay.interactable = false;
                distortionOverlay.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            infectionController.InfectionChanged += HandleInfectionChanged;
            HandleInfectionChanged(infectionController.NormalizedInfection);
            ScheduleNextFakeCall();
        }

        private void OnDisable()
        {
            if (infectionController != null)
            {
                infectionController.InfectionChanged -= HandleInfectionChanged;
            }

            RestorePresentationDefaults();
        }

        private void Update()
        {
            displayedSeverity = Mathf.MoveTowards(
                displayedSeverity,
                targetSeverity,
                feedbackResponse * Time.deltaTime);

            float distortionStrength = displayedSeverity * displayedSeverity;
            float pulse = Mathf.Sin(Time.time * pulseSpeed);

            if (distortionOverlay != null)
            {
                distortionOverlay.alpha = distortionStrength * maximumOverlayAlpha;
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseCameraFov + pulse * maximumFovPulse * distortionStrength;
            }

            if (weaponRoot != null)
            {
                float yaw = Mathf.Sin(Time.time * pulseSpeed * 0.73f) * maximumWeaponSway * distortionStrength;
                float roll = pulse * maximumWeaponSway * distortionStrength;
                weaponRoot.localRotation = baseWeaponRotation * Quaternion.Euler(0f, yaw, roll);
            }

            UpdateHeartbeat();
            UpdateFakeCalls();
        }

        private void HandleInfectionChanged(float normalizedInfection)
        {
            targetSeverity = Mathf.Clamp01(normalizedInfection);
        }

        private void UpdateHeartbeat()
        {
            if (heartbeatSource == null || heartbeatSource.clip == null)
            {
                return;
            }

            heartbeatSource.volume = baseHeartbeatVolume * displayedSeverity;
            heartbeatSource.pitch = Mathf.Lerp(baseHeartbeatPitch * 0.85f, baseHeartbeatPitch * 1.45f, displayedSeverity);

            if (displayedSeverity > 0.02f && !heartbeatSource.isPlaying)
            {
                heartbeatSource.loop = true;
                heartbeatSource.Play();
            }
            else if (displayedSeverity <= 0.02f && heartbeatSource.isPlaying)
            {
                heartbeatSource.Stop();
            }
        }

        private void UpdateFakeCalls()
        {
            if (displayedSeverity < fakeCallThreshold ||
                hallucinationSource == null ||
                fakePigCalls == null ||
                fakePigCalls.Length == 0 ||
                Time.time < nextFakeCallTime)
            {
                return;
            }

            AudioClip clip = fakePigCalls[Random.Range(0, fakePigCalls.Length)];
            if (clip != null)
            {
                Vector2 direction2D = Random.insideUnitCircle.normalized;
                float distance = Random.Range(fakeCallDistance.x, fakeCallDistance.y);
                Vector3 direction = transform.right * direction2D.x + transform.forward * direction2D.y;
                if (hallucinationSource.transform != transform)
                {
                    hallucinationSource.transform.position = transform.position + direction * distance;
                }

                float strength = Mathf.InverseLerp(fakeCallThreshold, 1f, displayedSeverity);
                hallucinationSource.PlayOneShot(clip, Mathf.Lerp(0.35f, 1f, strength));
            }

            ScheduleNextFakeCall();
        }

        private void ScheduleNextFakeCall()
        {
            float minimumDelay = Mathf.Max(0.1f, Mathf.Min(fakeCallDelay.x, fakeCallDelay.y));
            float maximumDelay = Mathf.Max(minimumDelay, Mathf.Max(fakeCallDelay.x, fakeCallDelay.y));
            nextFakeCallTime = Time.time + Random.Range(minimumDelay, maximumDelay);
        }

        private void RestorePresentationDefaults()
        {
            if (distortionOverlay != null)
            {
                distortionOverlay.alpha = 0f;
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseCameraFov;
            }

            if (weaponRoot != null)
            {
                weaponRoot.localRotation = baseWeaponRotation;
            }

            if (heartbeatSource != null)
            {
                heartbeatSource.Stop();
                heartbeatSource.volume = baseHeartbeatVolume;
                heartbeatSource.pitch = baseHeartbeatPitch;
            }
        }

        private void OnValidate()
        {
            feedbackResponse = Mathf.Max(0f, feedbackResponse);
            fakeCallDelay.x = Mathf.Max(0.1f, fakeCallDelay.x);
            fakeCallDelay.y = Mathf.Max(fakeCallDelay.x, fakeCallDelay.y);
            fakeCallDistance.x = Mathf.Max(0f, fakeCallDistance.x);
            fakeCallDistance.y = Mathf.Max(fakeCallDistance.x, fakeCallDistance.y);
        }
    }
}
