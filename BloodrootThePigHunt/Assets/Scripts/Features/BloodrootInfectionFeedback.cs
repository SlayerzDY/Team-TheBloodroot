using UnityEngine;
using UnityEngine.UI;

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

        [Header("Overlay")]
        [SerializeField] private Color overlayColor =
            new Color(0.85f, 0f, 0f, 1f);
        [SerializeField] private int overlaySortingOrder = -20;

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

            if (distortionOverlay == null)
            {
                distortionOverlay = CreateDistortionOverlay();
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

        private CanvasGroup CreateDistortionOverlay()
        {
            GameObject canvasObject =
                new GameObject("Bloodroot Infection Overlay Canvas");

            canvasObject.transform.SetParent(transform, false);

            Canvas canvas =
                canvasObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = overlaySortingOrder;

            CanvasGroup canvasGroup =
                canvasObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            GameObject imageObject =
                new GameObject("Bloodroot Infection Red Overlay");

            imageObject.transform.SetParent(canvasObject.transform, false);

            Image image =
                imageObject.AddComponent<Image>();

            image.color = overlayColor;
            image.raycastTarget = false;

            RectTransform rect =
                image.GetComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return canvasGroup;
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
