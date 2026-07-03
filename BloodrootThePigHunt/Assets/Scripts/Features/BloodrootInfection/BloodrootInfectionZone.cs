using System.Collections.Generic;
using UnityEngine;

namespace Bloodroot.Features.Infection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BloodrootInfectionZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float infectionPerSecond = 20f;

        private readonly HashSet<BloodrootInfectionController> playersInside =
            new HashSet<BloodrootInfectionController>();

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            BloodrootInfectionController infection =
                other.GetComponentInParent<BloodrootInfectionController>();

            if (infection != null && playersInside.Add(infection))
            {
                infection.EnterZone(this, infectionPerSecond);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            BloodrootInfectionController infection =
                other.GetComponentInParent<BloodrootInfectionController>();

            if (infection != null && playersInside.Remove(infection))
            {
                infection.ExitZone(this);
            }
        }

        private void OnDisable()
        {
            foreach (BloodrootInfectionController infection in playersInside)
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
