using UnityEngine;

namespace Bloodroot.Features.BloodMoon
{
    /// <summary>Optional lighting, banner, and sound treatment for a Blood Moon wave.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BloodMoonWaveDirector))]
    public sealed class BloodMoonPresentation : MonoBehaviour
    {
        [SerializeField] private BloodMoonWaveDirector director;
        [SerializeField] private Light sceneLight;
        [SerializeField] private CanvasGroup announcementBanner;
        [SerializeField] private AudioSource stingerSource;
        [SerializeField] private AudioClip bloodMoonStinger;

        [Header("Look")]
        [SerializeField] private Color bloodMoonLightColor = new Color(0.65f, 0.08f, 0.06f);
        [SerializeField, Min(0f)] private float lightIntensityMultiplier = 0.7f;
        [SerializeField, Min(0f)] private float transitionSpeed = 1.5f;
        [SerializeField, Min(0f)] private float bannerHoldTime = 2.5f;

        private Color normalLightColor;
        private float normalLightIntensity;
        private float bloodMoonWeight;
        private float targetWeight;
        private float hideBannerAt;

        private void Reset()
        {
            director = GetComponent<BloodMoonWaveDirector>();
        }

        private void Awake()
        {
            if (director == null)
            {
                director = GetComponent<BloodMoonWaveDirector>();
            }

            if (sceneLight != null)
            {
                normalLightColor = sceneLight.color;
                normalLightIntensity = sceneLight.intensity;
            }

            if (announcementBanner != null)
            {
                announcementBanner.alpha = 0f;
                announcementBanner.interactable = false;
                announcementBanner.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            director.BloodMoonStarted += HandleBloodMoonStarted;
            director.NormalWaveStarted += HandleNormalWaveStarted;
            director.ModifierCleared += HandleModifierCleared;
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.BloodMoonStarted -= HandleBloodMoonStarted;
                director.NormalWaveStarted -= HandleNormalWaveStarted;
                director.ModifierCleared -= HandleModifierCleared;
            }

            RestoreNormalLook();
        }

        private void Update()
        {
            bloodMoonWeight = Mathf.MoveTowards(
                bloodMoonWeight,
                targetWeight,
                transitionSpeed * Time.deltaTime);

            if (sceneLight != null)
            {
                sceneLight.color = Color.Lerp(normalLightColor, bloodMoonLightColor, bloodMoonWeight);
                sceneLight.intensity = Mathf.Lerp(
                    normalLightIntensity,
                    normalLightIntensity * lightIntensityMultiplier,
                    bloodMoonWeight);
            }

            if (announcementBanner != null && announcementBanner.alpha > 0f && Time.time >= hideBannerAt)
            {
                announcementBanner.alpha = Mathf.MoveTowards(
                    announcementBanner.alpha,
                    0f,
                    Time.deltaTime * 2f);
            }
        }

        private void HandleBloodMoonStarted(int waveNumber, BloodMoonModifier modifier)
        {
            targetWeight = 1f;
            hideBannerAt = Time.time + bannerHoldTime;

            if (announcementBanner != null)
            {
                announcementBanner.alpha = 1f;
            }

            if (stingerSource != null && bloodMoonStinger != null)
            {
                stingerSource.PlayOneShot(bloodMoonStinger);
            }
        }

        private void HandleNormalWaveStarted(int waveNumber)
        {
            targetWeight = 0f;
        }

        private void HandleModifierCleared(int waveNumber)
        {
            targetWeight = 0f;
        }

        private void RestoreNormalLook()
        {
            targetWeight = 0f;
            bloodMoonWeight = 0f;

            if (sceneLight != null)
            {
                sceneLight.color = normalLightColor;
                sceneLight.intensity = normalLightIntensity;
            }

            if (announcementBanner != null)
            {
                announcementBanner.alpha = 0f;
            }
        }
    }
}
