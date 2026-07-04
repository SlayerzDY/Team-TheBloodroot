using UnityEngine;
using System.Collections.Generic;

namespace Bloodroot.Features.Infection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]

    public sealed class BloodrootInfectionZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float infectionPerSecond = 20f;

        private readonly HashSet<BloodRootInfectionController> playersInside =
            new HashSet<BloodRootInfectionController>();

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            BloodRootInfectionController infection =
                other.GetComponentInParent<BloodRootInfectionController>();

            if (infection != null && playersInside.Add(infection))
            {
                infection.EnterZone(this, infectionPerSecond);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            BloodRootInfectionController infection =
                other.GetComponentInParent<BloodRootInfectionController>();
            if (infection != null && playersInside.Remove(infection))
            {
                infection.ExitZone(this);
            }
        }

        private void OnDisable()
        {
            foreach (BloodRootInfectionController infection in playersInside)
            {
                if (infection != null)
                {
                    infection.ExitZone(this);
                }
            }

            playersInside.Clear();
        }

    }
}