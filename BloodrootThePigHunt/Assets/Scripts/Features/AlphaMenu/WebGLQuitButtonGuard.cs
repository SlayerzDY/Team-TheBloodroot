using UnityEngine;
using UnityEngine.UI;

namespace Bloodroot.Features.AlphaMenu
{
    /// <summary>
    /// Hides an authored application-quit control in WebGL, where closing the
    /// hosting browser tab is owned by the browser rather than the game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WebGLQuitButtonGuard : MonoBehaviour
    {
        [SerializeField] private Selectable quitControl;

        public Selectable QuitControl => quitControl;

        private void Awake()
        {
            ApplyPlatformVisibility();
        }

        private void OnEnable()
        {
            ApplyPlatformVisibility();
        }

        public void Configure(Selectable authoredQuitControl)
        {
            quitControl = authoredQuitControl;
        }

        public bool ValidateConfiguration(out string error)
        {
            if (quitControl == null || quitControl.gameObject != gameObject)
            {
                error = "WebGL quit guard must reference the Selectable on its own GameObject.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void ApplyPlatformVisibility()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (quitControl != null)
                quitControl.gameObject.SetActive(false);
#endif
        }
    }
}
