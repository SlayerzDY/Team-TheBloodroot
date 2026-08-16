using UnityEngine;
using UnityEngine.UI;

namespace Bloodroot.Features.AlphaMenu
{
    public enum AlphaMenuActionFeedbackKind
    {
        Confirm = 0,
        Cancel = 1
    }

    /// <summary>
    /// Plays confirm or cancel feedback for an existing authored Button. The
    /// component never creates or changes UI objects.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AlphaMenuButtonActionFeedback : MonoBehaviour
    {
        [SerializeField] private AlphaMenuAudio menuAudio;
        [SerializeField] private AlphaMenuActionFeedbackKind feedbackKind =
            AlphaMenuActionFeedbackKind.Confirm;

        public AlphaMenuActionFeedbackKind FeedbackKind => feedbackKind;

        public void Configure(
            AlphaMenuActionFeedbackKind kind,
            AlphaMenuAudio audio = null)
        {
            feedbackKind = kind;
            menuAudio = audio;
        }

        /// <summary>
        /// Authored as the first persistent Button listener so feedback starts
        /// before Restart, Respawn, Quit, or another action can unload the UI.
        /// </summary>
        public void PlayActionFeedback()
        {
            ResolveAudio();
            if (menuAudio == null)
                return;

            if (feedbackKind == AlphaMenuActionFeedbackKind.Cancel)
                menuAudio.PlayCancel();
            else
                menuAudio.PlayConfirm();
        }

        private void ResolveAudio()
        {
            if (AlphaMenuAudio.Instance != null)
                menuAudio = AlphaMenuAudio.Instance;
        }
    }
}
