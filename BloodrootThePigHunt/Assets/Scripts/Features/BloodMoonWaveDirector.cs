using UnityEngine;
using System;
using System.Collections.Generic;



using Bloodroot.Features.BloodMoon;

public sealed class BloodMoonWaveDirector : MonoBehaviour
{

    [Header("Fixed Blood Moon Schedule")]
    [SerializeField] bool useFixedBloodMoonSchedule = true;
    [SerializeField, Min(1)] int firstBloodMoonWave;
    [SerializeField, Min(1)] int wavesBetweenBloodMoons;

    [Header("Random Blood Moon Chance")]
    [SerializeField, Range(0f, 100f)] float randomBloodMoonChancePercent;

    [SerializeField] private List<BloodMoonModifier> modifiers = new List<BloodMoonModifier>();

    private BloodMoonModifier activeModifier;
    private int activeWaveNumber;

    public event Action<int, BloodMoonModifier> BloodMoonStarted;
    public event Action<int> NormalWaveStarted;
    public event Action<int> ModifierCleared;

    public BloodMoonModifier ActiveModifier => activeModifier;
    public int ActiveWaveNumber => activeWaveNumber;
    public bool HasActiveModifier => activeModifier != null;

    private void Reset()
    {
        modifiers = new List<BloodMoonModifier>
            {
                new BloodMoonModifier(
                    "Stampede",
                    "More pigs move faster, but have less health.",
                    1.4f, 0.75f, 1f, 1.3f),
                new BloodMoonModifier(
                    "Thick Hide",
                    "Fewer pigs arrive with more health and damage.",
                    0.7f, 1.8f, 1.3f, 0.9f),
                new BloodMoonModifier(
                    "Blood Frenzy",
                    "Pigs hit harder and move faster.",
                    1f, 1f, 1.5f, 1.15f)
            };
    }

    public bool IsBloodMoonWave(int waveNumber)
    {
        return useFixedBloodMoonSchedule &&
            waveNumber >= firstBloodMoonWave &&
            (waveNumber - firstBloodMoonWave) % wavesBetweenBloodMoons == 0;
    }

    public BloodMoonModifier BeginWave(int waveNumber)
    {
        ClearModifier();
        activeWaveNumber = waveNumber;

        bool scheduledBloodMoon =
            IsBloodMoonWave(waveNumber);

        bool randomBloodMoon =
            !scheduledBloodMoon && RollRandomBloodMoon();

        if ((!scheduledBloodMoon && !randomBloodMoon) ||
            modifiers.Count == 0)
        {
            NormalWaveStarted?.Invoke(waveNumber);
            return null;
        }

        int modifierIndex =
            GetModifierIndex(waveNumber, scheduledBloodMoon);

        activeModifier = modifiers[modifierIndex];
        BloodMoonStarted?.Invoke(waveNumber, activeModifier);
        return activeModifier;
    }

    private bool RollRandomBloodMoon()
    {
        if (randomBloodMoonChancePercent <= 0f)
            return false;

        return UnityEngine.Random.Range(0f, 100f) < randomBloodMoonChancePercent;
    }

    private int GetModifierIndex(int waveNumber, bool scheduledBloodMoon)
    {
        if (scheduledBloodMoon)
        {
            int bloodMoonNumber =
                (waveNumber - firstBloodMoonWave) / wavesBetweenBloodMoons;

            return bloodMoonNumber % modifiers.Count;
        }

        return (waveNumber - 1) % modifiers.Count;
    }

    public void EndWave(int waveNumber)
    {
        if(waveNumber != ActiveWaveNumber)
        {
            return;
        }

        ClearModifier();
        activeWaveNumber = 0;
    }

    private void ClearModifier()
    {
        if (activeModifier == null)
        {
            return;
        }

        int clearedWave = activeWaveNumber;
        activeModifier = null;
        ModifierCleared?.Invoke(clearedWave);

    }

    private void OnValidate()
    {
        firstBloodMoonWave = Mathf.Max(1, firstBloodMoonWave);
        wavesBetweenBloodMoons = Mathf.Max(1, wavesBetweenBloodMoons);
        randomBloodMoonChancePercent = Mathf.Clamp(
            randomBloodMoonChancePercent,
            0f,
            100f);
    }

}
