using UnityEngine;
using System;
using System.Collections;
using Bloodroot.Features.BloodMoon;

public class waveManager : MonoBehaviour
{

    [SerializeField] int totalWaves;
    [SerializeField] float timeBetweenWaves;

    [Header("Wave Size")]
    [SerializeField, Min(0)] int startingEnemyCount = 5;
    [SerializeField, Min(0)] int enemiesAddedPerWave = 1;

    [Header("Connections")]
    [SerializeField] BloodMoonWaveDirector bloodMoonDirector;

    private bool encounterStarted;

    public int currentWave { get; private set; }
    public int enemiesRemaining { get; private set; }
    public bool waveActive { get; private set; }
    public BloodMoonModifier ActiveBloodMoonModifier { get; private set; }

    public event Action<int> NextWaveRequested;
    public event Action<int, int, BloodMoonModifier> WaveStarted;
    public event Action AllWavesCompleted;

    void Awake()
    {
        if (bloodMoonDirector == null)
            bloodMoonDirector = FindAnyObjectByType<BloodMoonWaveDirector>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentWave = 0;
        enemiesRemaining = 0;
        waveActive = false;
    }

    public void BeginEncounter()
    {
        if (encounterStarted)
            return;

        encounterStarted = true;
        RequestWave(1);

        Debug.Log("Wave encounter triggered");
    }

    public void StartWave(int baseEnemyCount)
    {
        if (waveActive || currentWave >= totalWaves)
            return;

        currentWave++;

        ActiveBloodMoonModifier = bloodMoonDirector != null
            ? bloodMoonDirector.BeginWave(currentWave)
            : null;

        enemiesRemaining = ActiveBloodMoonModifier != null
            ? ActiveBloodMoonModifier.ModifyEnemyCount(baseEnemyCount)
            : Mathf.Max(0, baseEnemyCount);

        waveActive = true;

        WaveStarted?.Invoke(currentWave, enemiesRemaining, ActiveBloodMoonModifier);
        Debug.Log($"Wave {currentWave} started with {enemiesRemaining} enemies.");

        if (enemiesRemaining == 0)
            CompleteWave();
    }

    public void EnemyDefeated()
    {
        if (!waveActive)
            return;

        enemiesRemaining = Mathf.Max(enemiesRemaining - 1, 0);

        if (enemiesRemaining == 0)
            CompleteWave();
    }

    private void CompleteWave()
    {
        waveActive = false;
        bloodMoonDirector?.EndWave(currentWave);
        ActiveBloodMoonModifier = null;

        Debug.Log($"Wave {currentWave} complete!");

        if (currentWave >= totalWaves)
        {
            AllWavesCompleted?.Invoke();
        }
        else
        {
            StartCoroutine(WaitForNextWave());
        }
    }

    private IEnumerator WaitForNextWave()
    {
        yield return new WaitForSeconds(timeBetweenWaves);
        RequestWave(currentWave + 1);
    }

    private void RequestWave(int waveNumber)
    {
        NextWaveRequested?.Invoke(waveNumber);

        if (!waveActive)
        {
            int baseEnemyCount = startingEnemyCount +
                                 (waveNumber - 1) * enemiesAddedPerWave;
            StartWave(baseEnemyCount);
        }
    }

}
