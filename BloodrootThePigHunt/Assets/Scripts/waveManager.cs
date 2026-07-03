using UnityEngine;
using System;
using System.Collections;

public class waveManager : MonoBehaviour
{

    [SerializeField] int totalWaves;
    [SerializeField] float timeBetweenWaves;

    private bool encounterStarted;

    public int currentWave { get; private set; }
    public int enemiesRemaining { get; private set; }
    public bool waveActive { get; private set; }

    public event Action<int> NextWaveRequested;
    public event Action AllWavesCompleted;

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

    public void StartWave(int enemyCount)
    {
        if (waveActive || currentWave >= totalWaves)
            return;

        currentWave++;
        enemiesRemaining = enemyCount;
        waveActive = true;

        Debug.Log($"Wave {currentWave} started.");
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
    }

}