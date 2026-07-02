using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.BloodMoon
{
    /// <summary>
    /// Chooses a special modifier on scheduled waves and shares it with the rest of
    /// the game. The existing wave manager still owns timing, spawning, and cleanup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BloodMoonWaveDirector : MonoBehaviour
    {
        [Header("Schedule")]
        [SerializeField, Min(1)] private int firstBloodMoonWave = 3;
        [SerializeField, Min(1)] private int wavesBetweenBloodMoons = 3;

        [Header("Selection")]
        [SerializeField] private bool randomizeModifiers = true;
        [SerializeField] private bool avoidImmediateRepeats = true;
        [SerializeField] private int selectionSeed = 9173;
        [SerializeField] private List<BloodMoonModifier> modifiers = new List<BloodMoonModifier>();

        [Header("Optional Starting Targets")]
        [Tooltip("Each component must implement IBloodMoonModifierTarget.")]
        [SerializeField] private List<MonoBehaviour> startingTargets = new List<MonoBehaviour>();

        [Header("Inspector Events")]
        [SerializeField] private UnityEvent onBloodMoonStarted = new UnityEvent();
        [SerializeField] private UnityEvent onNormalWaveStarted = new UnityEvent();
        [SerializeField] private UnityEvent onModifierCleared = new UnityEvent();

        private readonly HashSet<IBloodMoonModifierTarget> targets =
            new HashSet<IBloodMoonModifierTarget>();

        private BloodMoonModifier activeModifier;
        private int activeWaveNumber;
        private int lastModifierIndex = -1;

        public event Action<int, BloodMoonModifier> BloodMoonStarted;
        public event Action<int> NormalWaveStarted;
        public event Action<int> ModifierCleared;

        public BloodMoonModifier ActiveModifier => activeModifier;
        public int ActiveWaveNumber => activeWaveNumber;
        public bool HasActiveModifier => activeModifier != null;

        private void Reset()
        {
            modifiers = CreatePrototypeModifiers();
        }

        private void Awake()
        {
            foreach (MonoBehaviour component in startingTargets)
            {
                if (component is IBloodMoonModifierTarget target)
                {
                    targets.Add(target);
                }
                else if (component != null)
                {
                    Debug.LogWarning(
                        $"{component.GetType().Name} does not implement IBloodMoonModifierTarget.",
                        component);
                }
            }
        }

        private void OnDisable()
        {
            ClearCurrentModifier();
            activeWaveNumber = 0;
        }

        public bool IsBloodMoonWave(int waveNumber)
        {
            if (waveNumber < firstBloodMoonWave)
            {
                return false;
            }

            return (waveNumber - firstBloodMoonWave) % wavesBetweenBloodMoons == 0;
        }

        /// <summary>
        /// Call once at the beginning of a wave, before enemy counts or stats are calculated.
        /// Returns null for a normal wave.
        /// </summary>
        public BloodMoonModifier BeginWave(int waveNumber)
        {
            if (waveNumber < 1)
            {
                Debug.LogWarning("Wave numbers must start at 1.", this);
                return null;
            }

            if (activeWaveNumber == waveNumber)
            {
                return activeModifier;
            }

            ClearCurrentModifier();
            activeWaveNumber = waveNumber;

            if (!IsBloodMoonWave(waveNumber) || modifiers.Count == 0)
            {
                NormalWaveStarted?.Invoke(waveNumber);
                onNormalWaveStarted.Invoke();
                return null;
            }

            int modifierIndex = ChooseModifierIndex(waveNumber);
            activeModifier = modifiers[modifierIndex];
            lastModifierIndex = modifierIndex;

            foreach (IBloodMoonModifierTarget target in targets)
            {
                target.ApplyBloodMoonModifier(activeModifier);
            }

            BloodMoonStarted?.Invoke(waveNumber, activeModifier);
            onBloodMoonStarted.Invoke();
            return activeModifier;
        }

        /// <summary>Call after the wave is completely cleaned up.</summary>
        public void EndWave(int waveNumber)
        {
            if (waveNumber != activeWaveNumber)
            {
                return;
            }

            ClearCurrentModifier();
            activeWaveNumber = 0;
        }

        public void RegisterTarget(IBloodMoonModifierTarget target)
        {
            if (target == null || !targets.Add(target))
            {
                return;
            }

            if (activeModifier != null)
            {
                target.ApplyBloodMoonModifier(activeModifier);
            }
        }

        public void UnregisterTarget(IBloodMoonModifierTarget target)
        {
            if (target == null || !targets.Remove(target))
            {
                return;
            }

            if (activeModifier != null)
            {
                target.ClearBloodMoonModifier();
            }
        }

        private int ChooseModifierIndex(int waveNumber)
        {
            int index;
            if (randomizeModifiers)
            {
                int waveSeed = unchecked(selectionSeed * 397 ^ waveNumber * 7919);
                System.Random random = new System.Random(waveSeed);
                index = random.Next(modifiers.Count);
            }
            else
            {
                int bloodMoonIndex = (waveNumber - firstBloodMoonWave) / wavesBetweenBloodMoons;
                index = bloodMoonIndex % modifiers.Count;
            }

            if (avoidImmediateRepeats && modifiers.Count > 1 && index == lastModifierIndex)
            {
                index = (index + 1) % modifiers.Count;
            }

            return index;
        }

        private void ClearCurrentModifier()
        {
            if (activeModifier == null)
            {
                return;
            }

            foreach (IBloodMoonModifierTarget target in targets)
            {
                target.ClearBloodMoonModifier();
            }

            int clearedWave = activeWaveNumber;
            activeModifier = null;
            ModifierCleared?.Invoke(clearedWave);
            onModifierCleared.Invoke();
        }

        private void OnValidate()
        {
            firstBloodMoonWave = Mathf.Max(1, firstBloodMoonWave);
            wavesBetweenBloodMoons = Mathf.Max(1, wavesBetweenBloodMoons);

            foreach (BloodMoonModifier modifier in modifiers)
            {
                modifier?.Validate();
            }
        }

        private static List<BloodMoonModifier> CreatePrototypeModifiers()
        {
            return new List<BloodMoonModifier>
            {
                new BloodMoonModifier(
                    "stampede",
                    "Stampede",
                    "More pigs arrive, and they move faster, but each one has less health.",
                    1.4f,
                    0.75f,
                    1f,
                    1.3f,
                    1.15f),
                new BloodMoonModifier(
                    "thick-hide",
                    "Thick Hide",
                    "A smaller herd arrives with much more health and heavier attacks.",
                    0.7f,
                    1.8f,
                    1.3f,
                    0.9f,
                    1.5f),
                new BloodMoonModifier(
                    "blood-frenzy",
                    "Blood Frenzy",
                    "The herd hits harder and closes distance quickly. Pig parts are worth more.",
                    1f,
                    1f,
                    1.5f,
                    1.15f,
                    1.75f)
            };
        }
    }
}
