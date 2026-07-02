using System.Collections.Generic;
using UnityEngine;

namespace Bloodroot.Features.Infection
{
    /// <summary>
    /// Trigger volume that exposes any BloodrootInfectionController entering it.
    /// Multiple colliders on one character are counted as one exposure.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BloodrootInfectionZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float infectionPerSecond = 20f;

        private readonly Dictionary<BloodrootInfectionController, int> overlapCounts =
            new Dictionary<BloodrootInfectionController, int>();

        public float InfectionPerSecond => infectionPerSecond;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            Collider zoneCollider = GetComponent<Collider>();
            if (!zoneCollider.isTrigger)
            {
                Debug.LogWarning("Bloodroot infection zones require Is Trigger to be enabled.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            BloodrootInfectionController controller =
                other.GetComponentInParent<BloodrootInfectionController>();

            if (controller == null)
            {
                return;
            }

            overlapCounts.TryGetValue(controller, out int overlapCount);
            overlapCounts[controller] = overlapCount + 1;

            if (overlapCount == 0)
            {
                controller.EnterZone(this, infectionPerSecond);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            BloodrootInfectionController controller =
                other.GetComponentInParent<BloodrootInfectionController>();

            if (controller == null || !overlapCounts.TryGetValue(controller, out int overlapCount))
            {
                return;
            }

            if (overlapCount > 1)
            {
                overlapCounts[controller] = overlapCount - 1;
                return;
            }

            overlapCounts.Remove(controller);
            controller.ExitZone(this);
        }

        private void OnDisable()
        {
            foreach (BloodrootInfectionController controller in overlapCounts.Keys)
            {
                if (controller != null)
                {
                    controller.ExitZone(this);
                }
            }

            overlapCounts.Clear();
        }

        private void OnValidate()
        {
            infectionPerSecond = Mathf.Max(0f, infectionPerSecond);
        }
    }
}
