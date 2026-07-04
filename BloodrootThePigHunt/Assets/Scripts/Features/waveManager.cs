using System;
using System.Collections;
using Bloodroot.Features.BloodMoon;
using UnityEngine;

public class waveManager : MonoBehaviour
{
    [SerializeField, Min(1)] private int totalWaves = 20;
    [SerializeField, Min(0f)] private float timeBetweenWaves = 5f;

    [Header("Wave Size")]
    [SerializeField, Min(0)] private int startingEnemyCount = 3;
    [SerializeField, Min(0)] private int enemiesAddedPerWave = 2;

    [Header("Blood Moon")]
    [SerializeField] private BloodMoonWaveDirector bloodMoonDirector;

    private bool encounterStarted;

    public int currentWave { get; private set; }
    public int enemiesRemaining { get; private set; }
    public bool waveActive { get; private set; }

    public BloodMoonModifier ActiveBloodMoonModifier
    {
        get;
        private set;
    }

    public event Action<int, int, BloodMoonModifier> WaveStarted;
    public event Action<int> WaveCompleted;
    public event Action AllWavesCompleted;

    private void Awake()
    {
        if (bloodMoonDirector == null)
        {
            bloodMoonDirector =
                FindAnyObjectByType<BloodMoonWaveDirector>();
        }
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

        Debug.Log("Wave encounter started.");
    }

    private void StartNextWave()
    {
        if (currentWave >= totalWaves)
            return;

        int nextWave = currentWave + 1;

        int baseEnemyCount =
            startingEnemyCount +
            ((nextWave - 1) * enemiesAddedPerWave);

        StartWave(baseEnemyCount);
    }

    public void StartWave(int baseEnemyCount)
    {
        if (waveActive || currentWave >= totalWaves)
            return;

        if (gameManager.instance == null)
        {
            Debug.LogError(
                "WaveManager cannot find GameManager.");
            return;
        }

        currentWave++;

        ActiveBloodMoonModifier =
            bloodMoonDirector != null
                ? bloodMoonDirector.BeginWave(currentWave)
                : null;

        if (ActiveBloodMoonModifier != null)
        {
            enemiesRemaining =
                ActiveBloodMoonModifier.ModifyEnemyCount(
                    baseEnemyCount);
        }
        else
        {
            enemiesRemaining =
                Mathf.Max(0, baseEnemyCount);
        }

        waveActive = true;

        // GameManager resets and activates the existing MobSpawner.
        if (enemiesRemaining > 0)
        {
            gameManager.instance.StartNextWave(
                enemiesRemaining);
        }

        WaveStarted?.Invoke(
            currentWave,
            enemiesRemaining,
            ActiveBloodMoonModifier);

        Debug.Log(
            $"Wave {currentWave} started with " +
            $"{enemiesRemaining} enemies.");

        if (enemiesRemaining == 0)
        {
            CompleteWave();
        }
    }

    // EnemyAI calls this once when an enemy dies.
    public void EnemyDefeated()
    {
        if (!waveActive)
            return;

        enemiesRemaining =
            Mathf.Max(0, enemiesRemaining - 1);

        Debug.Log(
            $"Enemy defeated. " +
            $"{enemiesRemaining} enemies remaining.");

        if (enemiesRemaining == 0)
        {
            CompleteWave();
        }
    }

    private void CompleteWave()
    {
        if (!waveActive)
            return;

        waveActive = false;

        if (bloodMoonDirector != null)
        {
            bloodMoonDirector.EndWave(currentWave);
        }

        ActiveBloodMoonModifier = null;

        WaveCompleted?.Invoke(currentWave);

        Debug.Log($"Wave {currentWave} completed.");

        if (currentWave >= totalWaves)
        {
            Debug.Log("All waves completed.");
            AllWavesCompleted?.Invoke();
        }
        else
        {
            StartCoroutine(WaitForNextWave());
        }
    }

    private IEnumerator WaitForNextWave()
    {
        yield return new WaitForSeconds(
            timeBetweenWaves);

        StartNextWave();
    }
}
