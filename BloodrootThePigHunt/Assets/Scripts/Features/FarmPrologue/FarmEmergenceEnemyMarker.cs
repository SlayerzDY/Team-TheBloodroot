using UnityEngine;

namespace Bloodroot.Features.FarmPrologue
{
    /// <summary>
    /// Runtime ownership token for one enemy spawned by a recurring Farm
    /// emergence. The marker reports only to the director that created it, so
    /// unrelated enemies can never hold the Farm defense open or clear it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmEmergenceEnemyMarker : MonoBehaviour
    {
        private FarmRecurringEmergenceDirector owner;
        private int generation;
        private bool initialized;

        public int Generation => generation;

        internal void Initialize(
            FarmRecurringEmergenceDirector emergenceOwner,
            int emergenceGeneration)
        {
            owner = emergenceOwner;
            generation = emergenceGeneration;
            initialized = owner != null;
        }

        private void OnDestroy()
        {
            if (!initialized || owner == null)
                return;

            owner.NotifyOwnedEnemyDestroyed(this, generation);
        }
    }
}
