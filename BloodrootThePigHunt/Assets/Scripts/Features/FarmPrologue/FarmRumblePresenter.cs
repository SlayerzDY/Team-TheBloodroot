using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.FarmPrologue
{
    /// <summary>
    /// Authored presentation adapter for the independently timed ground rumble.
    /// It applies a deterministic positional camera shake, optionally plays an
    /// assigned AudioSource, and restores the exact local camera position it
    /// captured. No camera, audio, or UI object is created at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmRumblePresenter : MonoBehaviour
    {
        [Header("Prologue Hook")]
        [SerializeField] private FarmPrologueDirector director;

        [Header("Authored Camera")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.025f;
        [SerializeField, Min(0.01f)] private float shakeFrequency = 10f;
        [SerializeField, Min(0f)] private float rampInSeconds = 0.35f;
        [SerializeField] private Vector3 axisScale =
            new Vector3(1f, 0.65f, 0.35f);

        [Header("Authored Audio")]
        [SerializeField] private AudioSource rumbleAudioSource;
        [SerializeField] private bool playAssignedAudioSource = true;

        [Header("Authored Extension Hooks")]
        [SerializeField] private UnityEvent presentationStarted = new();
        [SerializeField] private UnityEvent presentationStopped = new();

        private bool isBound;
        private bool isPresenting;
        private bool startedAudio;
        private float elapsed;
        private Vector3 capturedLocalPosition;

        public bool IsPresenting => isPresenting;
        public float ShakeAmplitude => shakeAmplitude;
        public float ShakeFrequency => shakeFrequency;
        public float RampInSeconds => rampInSeconds;

        private void OnEnable()
        {
            Bind();

            if (director != null &&
                director.IsGroundRumbleActive)
            {
                StartPresentation();
            }
        }

        private void OnDisable()
        {
            Unbind();
            StopPresentation();
        }

        private void OnDestroy()
        {
            StopPresentation();
        }

        private void OnValidate()
        {
            shakeAmplitude = Mathf.Max(0f, shakeAmplitude);
            shakeFrequency = Mathf.Max(0.01f, shakeFrequency);
            rampInSeconds = Mathf.Max(0f, rampInSeconds);
        }

        private void LateUpdate()
        {
            if (!isPresenting || cameraTransform == null)
                return;

            elapsed += Time.unscaledDeltaTime;

            float angularTime =
                elapsed * shakeFrequency * Mathf.PI * 2f;
            float envelope = rampInSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / rampInSeconds);

            Vector3 deterministicOffset = new Vector3(
                Mathf.Sin(angularTime),
                Mathf.Sin(angularTime * 1.371f + 1.047f),
                Mathf.Sin(angularTime * 0.793f + 2.094f));
            deterministicOffset = Vector3.Scale(
                deterministicOffset,
                axisScale);
            deterministicOffset *= shakeAmplitude * envelope;

            cameraTransform.localPosition =
                capturedLocalPosition + deterministicOffset;
        }

        public void Configure(
            FarmPrologueDirector prologueDirector,
            Transform authoredCamera,
            AudioSource authoredAudioSource)
        {
            Unbind();
            StopPresentation();
            director = prologueDirector;
            cameraTransform = authoredCamera;
            rumbleAudioSource = authoredAudioSource;

            if (isActiveAndEnabled)
            {
                Bind();

                if (director != null &&
                    director.IsGroundRumbleActive)
                {
                    StartPresentation();
                }
            }
        }

        public void ConfigureShake(
            float amplitude,
            float frequency,
            float rampInDuration)
        {
            shakeAmplitude = Mathf.Max(0f, amplitude);
            shakeFrequency = Mathf.Max(0.01f, frequency);
            rampInSeconds = Mathf.Max(0f, rampInDuration);
        }

        public void StartPresentation()
        {
            if (isPresenting)
                return;

            isPresenting = true;
            elapsed = 0f;

            if (cameraTransform != null)
            {
                capturedLocalPosition = cameraTransform.localPosition;
            }

            startedAudio = false;

            if (playAssignedAudioSource &&
                rumbleAudioSource != null &&
                rumbleAudioSource.clip != null &&
                !rumbleAudioSource.isPlaying)
            {
                rumbleAudioSource.Play();
                startedAudio = true;
            }

            FarmPrologueEventUtility.Invoke(presentationStarted, this);
        }

        public void StopPresentation()
        {
            if (!isPresenting)
                return;

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = capturedLocalPosition;
            }

            if (startedAudio && rumbleAudioSource != null)
            {
                rumbleAudioSource.Stop();
            }

            startedAudio = false;
            isPresenting = false;
            elapsed = 0f;
            FarmPrologueEventUtility.Invoke(presentationStopped, this);
        }

        private void HandleGroundRumbleStateChanged(bool active)
        {
            if (active)
            {
                StartPresentation();
            }
            else
            {
                StopPresentation();
            }
        }

        private void Bind()
        {
            if (isBound || director == null)
                return;

            director.GroundRumbleStateChanged +=
                HandleGroundRumbleStateChanged;
            isBound = true;
        }

        private void Unbind()
        {
            if (isBound && director != null)
            {
                director.GroundRumbleStateChanged -=
                    HandleGroundRumbleStateChanged;
            }

            isBound = false;
        }
    }
}
