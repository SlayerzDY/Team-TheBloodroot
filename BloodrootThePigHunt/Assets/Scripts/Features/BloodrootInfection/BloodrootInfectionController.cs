using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.Infection
{
    public enum InfectionStage
    {
        Clear,
        Distorted,
        Critical
    }

    [Serializable]
    public sealed class InfectionValueEvent : UnityEvent<float>
    {
    }

    [Serializable]
    public sealed class InfectionStageEvent : UnityEvent<InfectionStage>
    {
    }

    [Serializable]
    public sealed class InfectionDamageEvent : UnityEvent<int>
    {
    }


    /// Owns a character's infection meter. Infection zones register themselves as
    /// exposure sources, so overlapping trigger colliders cannot corrupt the meter.

    [DisallowMultipleComponent]
    public sealed class BloodrootInfectionController : MonoBehaviour
    {
        [Header("Exposure")]
        [SerializeField, Min(1f)] private float maxInfection = 100f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 12f;
        [SerializeField, Min(0f)] private float recoveryDelay = 1.5f;

        [Header("Stages")]
        [SerializeField, Range(0.01f, 0.98f)] private float distortedThreshold = 0.35f;
        [SerializeField, Range(0.02f, 0.99f)] private float criticalThreshold = 0.7f;

        [Header("Critical Damage")]
        [SerializeField, Range(0f, 1f)] private float damageThreshold = 0.85f;
        [SerializeField, Min(0)] private int damagePerTick = 5;
        [SerializeField, Min(0.05f)] private float damageInterval = 1f;
        [Tooltip("Optional. Assign a component implementing IDamage. If empty, this object is searched automatically.")]
        [SerializeField] private MonoBehaviour damageReceiver;

        [Header("Inspector Events")]
        [SerializeField] private InfectionValueEvent infectionChanged = new InfectionValueEvent();
        [SerializeField] private InfectionStageEvent stageChanged = new InfectionStageEvent();
        [SerializeField] private InfectionDamageEvent damageTicked = new InfectionDamageEvent();

        private readonly Dictionary<BloodrootInfectionZone, float> activeZones =
            new Dictionary<BloodrootInfectionZone, float>();

        private IDamage resolvedDamageReceiver;
        private float currentInfection;
        private float timeOutsideZones;
        private float damageTimer;
        private InfectionStage currentStage;

        public event Action<float> InfectionChanged;
        public event Action<InfectionStage> StageChanged;
        public event Action<int> DamageTicked;

        public float CurrentInfection => currentInfection;
        public float MaxInfection => maxInfection;
        public float NormalizedInfection => maxInfection <= 0f ? 0f : currentInfection / maxInfection;
        public InfectionStage CurrentStage => currentStage;
        public bool IsInsideInfectionZone => activeZones.Count > 0;

        private void Awake()
        {
            ResolveDamageReceiver();
            currentStage = EvaluateStage(NormalizedInfection);
        }

        private void Start()
        {
            NotifyInfectionChanged();
            NotifyStageChanged();
        }

        private void Update()
        {
            float exposureRate = GetStrongestExposureRate();
            if (exposureRate > 0f)
            {
                timeOutsideZones = 0f;
                AddInfection(exposureRate * Time.deltaTime);
            }
            else
            {
                timeOutsideZones += Time.deltaTime;
                if (timeOutsideZones >= recoveryDelay)
                {
                    AddInfection(-recoveryPerSecond * Time.deltaTime);
                }
            }

            UpdateCriticalDamage(exposureRate > 0f);
        }

        /// Adds a raw amount to the meter. Negative values cleanse infection.
        public void AddInfection(float amount)
        {
            SetInfection(currentInfection + amount);
        }

        public void ClearInfection()
        {
            SetInfection(0f);
        }

        internal void EnterZone(BloodrootInfectionZone zone, float infectionPerSecond)
        {
            if (zone == null)
            {
                return;
            }

            activeZones[zone] = Mathf.Max(0f, infectionPerSecond);
        }

        internal void ExitZone(BloodrootInfectionZone zone)
        {
            if (zone != null)
            {
                activeZones.Remove(zone);
            }
        }

        private void SetInfection(float value)
        {
            float clampedValue = Mathf.Clamp(value, 0f, maxInfection);
            if (Mathf.Approximately(clampedValue, currentInfection))
            {
                return;
            }

            currentInfection = clampedValue;
            NotifyInfectionChanged();

            InfectionStage nextStage = EvaluateStage(NormalizedInfection);
            if (nextStage == currentStage)
            {
                return;
            }

            currentStage = nextStage;
            NotifyStageChanged();
        }

        private float GetStrongestExposureRate()
        {
            float strongestRate = 0f;
            foreach (KeyValuePair<BloodrootInfectionZone, float> activeZone in activeZones)
            {
                if (activeZone.Key != null)
                {
                    strongestRate = Mathf.Max(strongestRate, activeZone.Value);
                }
            }

            return strongestRate;
        }

        private InfectionStage EvaluateStage(float normalizedValue)
        {
            if (normalizedValue >= criticalThreshold)
            {
                return InfectionStage.Critical;
            }

            if (normalizedValue >= distortedThreshold)
            {
                return InfectionStage.Distorted;
            }

            return InfectionStage.Clear;
        }

        private void UpdateCriticalDamage(bool isBeingExposed)
        {
            bool shouldDamage = isBeingExposed &&
                                damagePerTick > 0 &&
                                NormalizedInfection >= damageThreshold;

            if (!shouldDamage)
            {
                damageTimer = 0f;
                return;
            }

            damageTimer += Time.deltaTime;
            if (damageTimer < damageInterval)
            {
                return;
            }

            damageTimer -= damageInterval;
            resolvedDamageReceiver?.TakeDamage(damagePerTick);
            DamageTicked?.Invoke(damagePerTick);
            damageTicked.Invoke(damagePerTick);
        }

        private void ResolveDamageReceiver()
        {
            if (damageReceiver != null)
            {
                resolvedDamageReceiver = damageReceiver as IDamage;
                if (resolvedDamageReceiver == null)
                {
                    Debug.LogWarning(
                        $"{damageReceiver.GetType().Name} does not implement IDamage. " +
                        "Infection damage events will still fire.",
                        this);
                }

                return;
            }

            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is IDamage damageTarget)
                {
                    resolvedDamageReceiver = damageTarget;
                    damageReceiver = component;
                    return;
                }
            }
        }

        private void NotifyInfectionChanged()
        {
            float normalizedValue = NormalizedInfection;
            InfectionChanged?.Invoke(normalizedValue);
            infectionChanged.Invoke(normalizedValue);
        }

        private void NotifyStageChanged()
        {
            StageChanged?.Invoke(currentStage);
            stageChanged.Invoke(currentStage);
        }

        private void OnValidate()
        {
            maxInfection = Mathf.Max(1f, maxInfection);
            criticalThreshold = Mathf.Max(distortedThreshold + 0.01f, criticalThreshold);
            damageInterval = Mathf.Max(0.05f, damageInterval);
        }
    }
}
