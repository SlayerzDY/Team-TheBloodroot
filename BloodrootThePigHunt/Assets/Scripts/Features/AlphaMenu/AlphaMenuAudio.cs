using UnityEngine;

namespace Bloodroot.Features.AlphaMenu
{
    /// <summary>
    /// Authored, persistent menu-feedback audio authority. The component never
    /// creates an AudioSource or UI and deliberately has no volume-settings
    /// behavior; its scene object must provide every reference.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AlphaMenuAudio : MonoBehaviour
    {
        private static AlphaMenuAudio instance;

        [Header("Authored Audio Routing")]
        [SerializeField] private AudioSource menuAudioSource;

        [Header("Authored Menu SFX")]
        [SerializeField] private AudioClip pointerHoverClip;
        [SerializeField] private AudioClip selectionChangedClip;
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip cancelClip;

        public static AlphaMenuAudio Instance => instance;

        public float ConfirmClipDuration =>
            confirmClip != null ? confirmClip.length : 0f;

        public bool HasAllFeedbackClips =>
            pointerHoverClip != null &&
            selectionChangedClip != null &&
            confirmClip != null &&
            cancelClip != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (menuAudioSource == null)
            {
                menuAudioSource = GetComponent<AudioSource>();
            }

            if (menuAudioSource == null)
            {
                Debug.LogError(
                    "AlphaMenuAudio requires its authored AudioSource.",
                    this);
                enabled = false;
                return;
            }

            menuAudioSource.playOnAwake = false;
            menuAudioSource.spatialBlend = 0f;
            menuAudioSource.ignoreListenerPause = true;
            menuAudioSource.volume = 1f;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Editor-authoring hook for the authored source and feedback clips.
        /// </summary>
        public void Configure(
            AudioSource source,
            AudioClip hover,
            AudioClip selection,
            AudioClip confirm,
            AudioClip cancel)
        {
            menuAudioSource = source;
            pointerHoverClip = hover;
            selectionChangedClip = selection;
            confirmClip = confirm;
            cancelClip = cancel;
        }

        public void PlayPointerHover()
        {
            Play(pointerHoverClip);
        }

        public void PlaySelectionChanged()
        {
            Play(selectionChangedClip);
        }

        public void PlayConfirm()
        {
            Play(confirmClip);
        }

        public void PlayCancel()
        {
            Play(cancelClip);
        }

        public bool ValidateAuthoredContract(out string error)
        {
            if (menuAudioSource == null)
            {
                error = "Menu AudioSource is not assigned.";
                return false;
            }

            if (!HasAllFeedbackClips)
            {
                error =
                    "Pointer, selection, confirm, and cancel clips must all be authored.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void Play(AudioClip clip)
        {
            if (!enabled || menuAudioSource == null || clip == null)
            {
                return;
            }

            menuAudioSource.PlayOneShot(clip);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (menuAudioSource == null)
            {
                menuAudioSource = GetComponent<AudioSource>();
            }
        }
#endif
    }
}
