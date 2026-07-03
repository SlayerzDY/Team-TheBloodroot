using UnityEngine;

namespace Bloodroot.Features.BloodMoon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BloodMoonWaveDirector))]
    public sealed class BloodMoonPresentation : MonoBehaviour
    {
        [SerializeField] private BloodMoonWaveDirector director;
        [SerializeField] private Light sceneLight;
        [SerializeField] private CanvasGroup announcementBanner;
        [SerializeField] private AudioSource stingerSource;
        [SerializeField] private AudioClip bloodMoonStinger;
        [SerializeField] private Color bloodMoonColor = new Color(0.65f, 0.08f, 0.06f);
        [SerializeField, Min(0f)] private float bannerSeconds = 2.5f;

        private Color normalLightColor;
        private float bannerHideTime;

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
            }

            if (announcementBanner != null)
            {
                announcementBanner.alpha = 0f;
                announcementBanner.blocksRaycasts = false;
                announcementBanner.interactable = false;
            }
        }

        private void OnEnable()
        {
            director.BloodMoonStarted += ShowBloodMoon;
            director.NormalWaveStarted += ShowNormalWave;
            director.ModifierCleared += ShowNormalWave;
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.BloodMoonStarted -= ShowBloodMoon;
                director.NormalWaveStarted -= ShowNormalWave;
                director.ModifierCleared -= ShowNormalWave;
            }

            ShowNormalWave(0);
        }

        private void Update()
        {
            if (announcementBanner != null &&
                announcementBanner.alpha > 0f &&
                Time.time >= bannerHideTime)
            {
                announcementBanner.alpha = 0f;
            }
        }

        private void ShowBloodMoon(int waveNumber, BloodMoonModifier modifier)
        {
            if (sceneLight != null)
            {
                sceneLight.color = bloodMoonColor;
            }

            if (announcementBanner != null)
            {
                announcementBanner.alpha = 1f;
                bannerHideTime = Time.time + bannerSeconds;
            }

            if (stingerSource != null && bloodMoonStinger != null)
            {
                stingerSource.PlayOneShot(bloodMoonStinger);
            }
        }

        private void ShowNormalWave(int waveNumber)
        {
            if (sceneLight != null)
            {
                sceneLight.color = normalLightColor;
            }

            if (announcementBanner != null)
            {
                announcementBanner.alpha = 0f;
            }
        }
    }
}
