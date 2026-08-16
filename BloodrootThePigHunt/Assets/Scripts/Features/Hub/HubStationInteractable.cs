using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.Hub
{
    /// <summary>
    /// Small adapter between the project's existing IInteract raycast and an
    /// authored hub station/presenter. It never constructs UI or station art.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HubStationInteractable : MonoBehaviour, IInteract
    {
        [SerializeField] private HubStationProgression progression;
        [SerializeField] private HubStationId station;
        [SerializeField] private UnityEvent stationOpened = new();
        [SerializeField] private HubStringUnityEvent interactionRejected = new();

        public HubStationId Station => station;

        /// <summary>
        /// Editor-authoring access to the serialized action hook. Runtime
        /// systems should subscribe to <see cref="StationOpened"/> instead.
        /// </summary>
        public UnityEvent StationOpenedEvent => stationOpened;

        public event Action<HubStationId> StationOpened;
        public event Action<string> InteractionRejected;

        public void SendInteract(Collider target)
        {
            if (progression == null ||
                !progression.IsStationUnlocked(station))
            {
                Reject("This station is not available yet.");
                return;
            }

            HubEventUtility.Invoke(StationOpened, station, this);
            HubEventUtility.Invoke(stationOpened, this);
        }

        public void Configure(
            HubStationProgression owner,
            HubStationId stationId)
        {
            progression = owner;
            station = stationId;
        }

        private void Reject(string reason)
        {
            string safeReason = reason ?? string.Empty;
            HubEventUtility.Invoke(InteractionRejected, safeReason, this);
            HubEventUtility.Invoke(
                interactionRejected,
                safeReason,
                this);
        }
    }
}
