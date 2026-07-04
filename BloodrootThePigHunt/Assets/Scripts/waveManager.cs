using System;
using System.Collections;
using Bloodroot.Features.BloodMoon;
using UnityEngine;

public class waveManager : MonoBehaviour
{
    [SerializeField, Min(1)] private int totalWaves;
    [SerializeField, Min(0f)] private float timeBetweenWaves;

    [Header("Wave Size")]
    [SerializeField, Min(0)] private int startingEnemyCount;
    [SerializeField, Min(0)] private int enemiesAddedPerWave;

    [Header("Connections")]
    [SerializeField] private MobSpawner mobSpawner;
    [SerializeField] private BloodMoonWaveDirector bloodMoonDirector;

    private bool encounterStarted;

    public int currentWave { get; private set; }
    public int enemiesRemaining { get; private set; }
    public bool waveActive { get; private set; }

    public BloodMoonModifier ActiveBloodMoonModifier { get; private set; }

    public event Action<int, int, BloodMoonModifier> WaveStarted;
    public event Action<int> WaveCompleted;
    public event Action AllWavesCompleted;

    private void Awake()
    {
        if (mobSpawner == null)
            mobSpawner = FindAnyObjectByType<MobSpawner>();

        if (bloodMoonDirector == null)
            bloodMoonDirector = FindAnyObjectByType<BloodMoonWaveDirector>();
    }

    private void Start()
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
        StartNextWave();
    }

    private void StartNextWave()
    {
        int nextWave = currentWave + 1;

        int baseEnemyCount =
            startingEnemyCount +
            (nextWave - 1) * enemiesAddedPerWave;

        StartWave(baseEnemyCount);
    }

    public void StartWave(int baseEnemyCount)
    {
        if (waveActive || currentWave >= totalWaves)
            return;

        if (mobSpawner == null)
        {
            Debug.LogError("WaveManager cannot find MobSpawner.");
            return;
        }

        currentWave++;

        ActiveBloodMoonModifier = bloodMoonDirector != null
            ? bloodMoonDirector.BeginWave(currentWave)
            : null;

        enemiesRemaining = ActiveBloodMoonModifier != null
            ? ActiveBloodMoonModifier.ModifyEnemyCount(baseEnemyCount)
            : Mathf.Max(0, baseEnemyCount);

        waveActive = true;
        
        gameManager.instance.StartNextWave(enemiesRemaining);

        WaveStarted?.Invoke(
            currentWave,
            enemiesRemaining,
            ActiveBloodMoonModifier);

        Debug.Log(
            $"Wave {currentWave} started with " +
            $"{enemiesRemaining} enemies.");

        if (enemiesRemaining == 0)
            CompleteWave();
    }

    public void EnemyDefeated()
    {
        if (!waveActive)
            return;

        enemiesRemaining =
            Mathf.Max(0, enemiesRemaining - 1);

        if (enemiesRemaining == 0)
            CompleteWave();
    }

    private void CompleteWave()
    {
        waveActive = false;

        bloodMoonDirector?.EndWave(currentWave);

        ActiveBloodMoonModifier = null;

        WaveCompleted?.Invoke(currentWave);

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

        StartNextWave();

    }
}
