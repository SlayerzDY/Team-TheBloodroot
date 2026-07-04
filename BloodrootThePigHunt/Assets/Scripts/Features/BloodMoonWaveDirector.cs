using UnityEngine;
using System;
using System.Collections.Generic;



using Bloodroot.Features.BloodMoon;

public sealed class BloodMoonWaveDirector : MonoBehaviour
{

    [SerializeField, Min(1)] int firstBloodMoonWave;
    [SerializeField, Min(1)] int wavesBetweenBloodMoons;
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
        return waveNumber >= firstBloodMoonWave && 
            (waveNumber - firstBloodMoonWave) % wavesBetweenBloodMoons == 0;
    }

    public BloodMoonModifier BeginWave(int waveNumber)
    {
        ClearModifier();
        activeWaveNumber = waveNumber;

        if (!IsBloodMoonWave(waveNumber) || modifiers.Count == 0)
        {
            NormalWaveStarted?.Invoke(waveNumber);
            return null;
        }

        int bloodMoonNumber = (waveNumber - firstBloodMoonWave) / wavesBetweenBloodMoons;
        activeModifier = modifiers[bloodMoonNumber % modifiers.Count];
        BloodMoonStarted?.Invoke(waveNumber, activeModifier);
        return activeModifier;
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
    }

}
