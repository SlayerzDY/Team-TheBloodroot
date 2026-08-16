using TMPro;
using UnityEngine;

namespace Bloodroot.Features.Hub
{
    /// <summary>
    /// Writes loadout selection/results into an authored world-space text
    /// object. It never creates UI or presentation objects at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubLoadoutFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private HubLoadoutStation loadoutStation;
        [SerializeField] private TMP_Text feedbackText;

        private HubLoadoutStation boundStation;

        public void Configure(
            HubLoadoutStation station,
            TMP_Text authoredFeedbackText)
        {
            Unbind();
            loadoutStation = station;
            feedbackText = authoredFeedbackText;

            if (isActiveAndEnabled)
            {
                Bind();
                RefreshSelection();
            }
        }

        private void OnEnable()
        {
            Bind();
            RefreshSelection();
        }

        private void Start()
        {
            Bind();
            RefreshSelection();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void RefreshSelection()
        {
            if (feedbackText == null || loadoutStation == null)
                return;

            int index = loadoutStation.SelectedLoadoutIndex;
            if (index < 0 || index >= loadoutStation.Loadouts.Count ||
                loadoutStation.Loadouts[index] == null)
            {
                feedbackText.text = "LOADOUT NOT CONFIGURED";
                return;
            }

            HubLoadoutDefinition selected = loadoutStation.Loadouts[index];
            feedbackText.text =
                $"SELECTED: {selected.DisplayName.ToUpperInvariant()}\n" +
                "SIDE CONTROLS SELECT / CENTER APPLIES";
        }

        private void Bind()
        {
            if (boundStation == loadoutStation)
                return;

            Unbind();
            boundStation = loadoutStation;
            if (boundStation == null)
                return;

            boundStation.LoadoutSelected += HandleLoadoutSelected;
            boundStation.LoadoutApplied += HandleLoadoutApplied;
        }

        private void Unbind()
        {
            if (boundStation == null)
                return;

            boundStation.LoadoutSelected -= HandleLoadoutSelected;
            boundStation.LoadoutApplied -= HandleLoadoutApplied;
            boundStation = null;
        }

        private void HandleLoadoutSelected(string _)
        {
            RefreshSelection();
        }

        private void HandleLoadoutApplied(
            string _,
            bool succeeded,
            string message)
        {
            if (feedbackText == null)
                return;

            feedbackText.text = succeeded
                ? $"LOADOUT READY\n{message}"
                : $"LOADOUT REJECTED\n{message}";
        }
    }
}
