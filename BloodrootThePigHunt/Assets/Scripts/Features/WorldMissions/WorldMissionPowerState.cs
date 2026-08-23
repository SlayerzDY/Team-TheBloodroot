using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Applies an authored power state to pre-existing route roots. A mission
    /// objective can restore power, but this component never creates geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionPowerState : MonoBehaviour
    {
        [Header("Power")]
        [SerializeField] private bool startsPowered;
        [SerializeField] private WorldMissionObjective restorationObjective;
        [SerializeField] private bool restoreWhenObjectiveCompletes = true;
        [SerializeField] private bool resetWhenObjectiveBecomesInactive = true;

        [Header("Authored Route Roots")]
        [SerializeField] private GameObject[] poweredRouteRoots =
            Array.Empty<GameObject>();
        [SerializeField] private GameObject[] unpoweredRouteRoots =
            Array.Empty<GameObject>();

        [Header("Authored Power Hooks")]
        [SerializeField] private WorldMissionBoolUnityEvent
            powerStateChanged = new();
        [SerializeField] private UnityEvent powerRestored = new();
        [SerializeField] private UnityEvent powerLost = new();

        private bool initialized;
        private bool isPowered;
        private WorldMissionObjective boundObjective;

        public bool IsPowered => isPowered;
        public event Action<bool> PowerStateChanged;

        private void OnEnable()
        {
            if (!initialized)
            {
                isPowered = startsPowered;
                initialized = true;
            }

            BindObjective();
            ApplyRouteState();
        }

        private void OnDisable()
        {
            UnbindObjective();
        }

        public void RestorePower()
        {
            SetPowered(true);
        }

        public void LosePower()
        {
            SetPowered(false);
        }

        public void SetPowered(bool powered)
        {
            initialized = true;
            bool changed = isPowered != powered;
            isPowered = powered;
            ApplyRouteState();

            if (!changed)
                return;

            WorldMissionEventUtility.Invoke(
                powerStateChanged,
                isPowered,
                this);
            WorldMissionEventUtility.Invoke(
                PowerStateChanged,
                isPowered,
                this);

            if (isPowered)
            {
                WorldMissionEventUtility.Invoke(powerRestored, this);
            }
            else
            {
                WorldMissionEventUtility.Invoke(powerLost, this);
            }
        }

        public void ResetPowerState()
        {
            SetPowered(startsPowered);
        }

        public void Configure(
            WorldMissionObjective powerObjective,
            GameObject[] routesWhenPowered,
            GameObject[] routesWhenUnpowered,
            bool initiallyPowered)
        {
            UnbindObjective();
            restorationObjective = powerObjective;
            poweredRouteRoots = routesWhenPowered ?? Array.Empty<GameObject>();
            unpoweredRouteRoots = routesWhenUnpowered ??
                Array.Empty<GameObject>();
            startsPowered = initiallyPowered;
            initialized = true;
            isPowered = startsPowered;

            if (isActiveAndEnabled)
            {
                BindObjective();
                ApplyRouteState();
            }
        }

        private void BindObjective()
        {
            if (boundObjective == restorationObjective)
                return;

            UnbindObjective();
            boundObjective = restorationObjective;

            if (boundObjective == null)
                return;

            boundObjective.Completed += HandleRestorationCompleted;
            boundObjective.StateChanged += HandleRestorationStateChanged;

            if (restoreWhenObjectiveCompletes && boundObjective.IsComplete)
            {
                SetPowered(true);
            }
        }

        private void UnbindObjective()
        {
            if (boundObjective != null)
            {
                boundObjective.Completed -= HandleRestorationCompleted;
                boundObjective.StateChanged -= HandleRestorationStateChanged;
            }

            boundObjective = null;
        }

        private void HandleRestorationCompleted(
            WorldMissionObjective completedObjective)
        {
            if (restoreWhenObjectiveCompletes &&
                completedObjective == restorationObjective)
            {
                SetPowered(true);
            }
        }

        private void HandleRestorationStateChanged(
            WorldMissionObjective changedObjective)
        {
            if (resetWhenObjectiveBecomesInactive &&
                changedObjective == restorationObjective &&
                changedObjective.State ==
                    WorldMissionObjectiveState.Inactive)
            {
                SetPowered(startsPowered);
            }
        }

        private void ApplyRouteState()
        {
            SetRootsActive(poweredRouteRoots, isPowered);
            SetRootsActive(unpoweredRouteRoots, !isPowered);
        }

        private void SetRootsActive(
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
