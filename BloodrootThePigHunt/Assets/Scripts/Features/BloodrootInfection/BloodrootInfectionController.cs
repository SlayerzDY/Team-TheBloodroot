using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bloodroot.Features.Infection
{
    [DisallowMultipleComponent]
    public sealed class BloodrootInfectionController : MonoBehaviour
    {
        [Header("Infection")]
        [SerializeField, Min(1f)] private float maxInfection = 100f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 15f;
        [SerializeField, Min(0f)] private float recoveryDelay = 1f;

        [Header("Damage")]
        [SerializeField, Range(0f, 1f)] private float damageThreshold = 0.8f;
        [SerializeField, Min(0)] private int damagePerTick = 5;
        [SerializeField, Min(0.1f)] private float damageInterval = 1f;
        [Tooltip("Optional component implementing IDamage.")]
        [SerializeField] private MonoBehaviour damageReceiver;

        private readonly Dictionary<BloodrootInfectionZone, float> activeZones =
            new Dictionary<BloodrootInfectionZone, float>();

        private IDamage damageTarget;
        private float currentInfection;
        private float timeOutsideZone;
        private float damageTimer;

        public event Action<float> InfectionChanged;

        public float CurrentInfection => currentInfection;
        public float MaxInfection => maxInfection;
        public float NormalizedInfection => currentInfection / maxInfection;
        public bool IsInsideInfectionZone => activeZones.Count > 0;

        private void Awake()
        {
            FindDamageTarget();
        }

        private void Update()
        {
            float infectionRate = GetStrongestZoneRate();

            if (infectionRate > 0f)
            {
                timeOutsideZone = 0f;
                AddInfection(infectionRate * Time.deltaTime);
            }
            else
            {
                timeOutsideZone += Time.deltaTime;
                if (timeOutsideZone >= recoveryDelay)
                {
                    AddInfection(-recoveryPerSecond * Time.deltaTime);
                }
            }

            UpdateDamage(infectionRate > 0f);
        }

        public void AddInfection(float amount)
        {
            float nextValue = Mathf.Clamp(currentInfection + amount, 0f, maxInfection);
            if (Mathf.Approximately(nextValue, currentInfection))
            {
                return;
            }

            currentInfection = nextValue;
            InfectionChanged?.Invoke(NormalizedInfection);
        }

        public void ClearInfection()
        {
            AddInfection(-currentInfection);
        }

        internal void EnterZone(BloodrootInfectionZone zone, float infectionPerSecond)
        {
            activeZones[zone] = Mathf.Max(0f, infectionPerSecond);
        }

        internal void ExitZone(BloodrootInfectionZone zone)
        {
            activeZones.Remove(zone);
        }

        private float GetStrongestZoneRate()
        {
            float strongestRate = 0f;
            foreach (KeyValuePair<BloodrootInfectionZone, float> zone in activeZones)
            {
                if (zone.Key != null)
                {
                    strongestRate = Mathf.Max(strongestRate, zone.Value);
                }
            }

            return strongestRate;
        }

        private void UpdateDamage(bool isInsideZone)
        {
            if (!isInsideZone || NormalizedInfection < damageThreshold || damagePerTick <= 0)
            {
                damageTimer = 0f;
                return;
            }

            damageTimer += Time.deltaTime;
            if (damageTimer < damageInterval)
            {
                return;
            }

            damageTimer = 0f;
            damageTarget?.TakeDamage(damagePerTick);
        }

        private void FindDamageTarget()
        {
            if (damageReceiver is IDamage assignedTarget)
            {
                damageTarget = assignedTarget;
                return;
            }

            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is IDamage foundTarget)
                {
                    damageReceiver = component;
                    damageTarget = foundTarget;
                    return;
                }
            }
        }

        private void OnValidate()
        {
            maxInfection = Mathf.Max(1f, maxInfection);
            damageInterval = Mathf.Max(0.1f, damageInterval);
        }
    }
}
