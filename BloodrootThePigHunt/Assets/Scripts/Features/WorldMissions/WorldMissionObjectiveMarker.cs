using System;
using UnityEngine;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Drives authored markers, shortcut blockers, or loop roots from one
    /// objective's state. Assigning the same root to multiple state arrays is
    /// allowed; the final authored state entry wins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionObjectiveMarker : MonoBehaviour
    {
        [SerializeField] private WorldMissionObjective objective;

        [Header("Authored State Roots")]
        [SerializeField] private GameObject[] activeWhenInactive =
            Array.Empty<GameObject>();
        [SerializeField] private GameObject[] activeWhenAvailable =
            Array.Empty<GameObject>();
        [SerializeField] private GameObject[] activeWhenCompleted =
            Array.Empty<GameObject>();

        private WorldMissionObjective boundObjective;

        public WorldMissionObjective Objective => objective;

        private void OnEnable()
        {
            BindObjective();
            RefreshMarkerState();
        }

        private void OnDisable()
        {
            UnbindObjective();
        }

        public void Configure(
            WorldMissionObjective targetObjective,
            GameObject[] inactiveRoots,
            GameObject[] availableRoots,
            GameObject[] completedRoots)
        {
            UnbindObjective();
            objective = targetObjective;
            activeWhenInactive = inactiveRoots ?? Array.Empty<GameObject>();
            activeWhenAvailable = availableRoots ?? Array.Empty<GameObject>();
            activeWhenCompleted = completedRoots ?? Array.Empty<GameObject>();

            if (isActiveAndEnabled)
            {
                BindObjective();
                RefreshMarkerState();
            }
        }

        public void RefreshMarkerState()
        {
            WorldMissionObjectiveState objectiveState = objective != null
                ? objective.State
                : WorldMissionObjectiveState.Inactive;

            SetRootsForState(
                activeWhenInactive,
                objectiveState == WorldMissionObjectiveState.Inactive);
            SetRootsForState(
                activeWhenAvailable,
                objectiveState == WorldMissionObjectiveState.Available);
            SetRootsForState(
                activeWhenCompleted,
                objectiveState == WorldMissionObjectiveState.Completed);
        }

        private void BindObjective()
        {
            if (boundObjective == objective)
                return;

            UnbindObjective();
            boundObjective = objective;

            if (boundObjective != null)
            {
                boundObjective.StateChanged += HandleObjectiveStateChanged;
            }
        }

        private void UnbindObjective()
        {
            if (boundObjective != null)
            {
                boundObjective.StateChanged -= HandleObjectiveStateChanged;
            }

            boundObjective = null;
        }

        private void HandleObjectiveStateChanged(
            WorldMissionObjective changedObjective)
        {
            RefreshMarkerState();
        }

        private void SetRootsForState(
            GameObject[] roots,
            bool active)
        {
            if (roots == null)
                return;

            foreach (GameObject root in roots)
            {
                if (root == null)
                    continue;

                if (root == gameObject)
                {
                    Debug.LogError(
                        "A WorldMissionObjectiveMarker cannot toggle its own " +
                        "GameObject because it would stop receiving state " +
                        "changes.",
                        this);
                    continue;
                }

                if (root.activeSelf != active)
                {
                    root.SetActive(active);
                }
            }
        }
    }
}
