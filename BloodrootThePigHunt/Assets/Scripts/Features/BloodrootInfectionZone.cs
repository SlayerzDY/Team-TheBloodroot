using UnityEngine;
using System.Collections.Generic;

namespace Bloodroot.Features.Infection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]

    public sealed class BloodrootInfectionZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float infectionPerSecond = 20f;

        private readonly Dictionary<BloodRootInfectionController, int> playerColliderCounts =
            new Dictionary<BloodRootInfectionController, int>();

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            BloodRootInfectionController infection =
                other.GetComponentInParent<BloodRootInfectionController>();

            if (infection == null)
            {
                return;
            }

            playerColliderCounts.TryGetValue(infection, out int colliderCount);
            playerColliderCounts[infection] = colliderCount + 1;
            if (colliderCount == 0)
            {
                infection.EnterZone(this, infectionPerSecond);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            BloodRootInfectionController infection =
                other.GetComponentInParent<BloodRootInfectionController>();
            if (infection == null || !playerColliderCounts.TryGetValue(infection, out int colliderCount))
            {
                return;
            }

            if (colliderCount > 1)
            {
                playerColliderCounts[infection] = colliderCount - 1;
            }
            else
            {
                playerColliderCounts.Remove(infection);
                infection.ExitZone(this);
            }
        }

        private void OnDisable()
        {
            foreach (BloodRootInfectionController infection in playerColliderCounts.Keys)
            {
                if (infection != null)
                {
                    infection.ExitZone(this);
                }
            }

            playerColliderCounts.Clear();
        }

    }
}
