using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Bloodroot.Features.BloodMoon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class waveManager : MonoBehaviour
{
    [SerializeField, Min(1)] private int totalWaves = 20;
    [SerializeField, Min(0f)] private float timeBetweenWaves = 5f;

    [Header("Wave Size")]
    [SerializeField, Min(0)] private int startingEnemyCount = 3;
    [SerializeField, Min(0)] private int enemiesAddedPerWave = 2;

    [Header("Blood Moon")]
    [SerializeField] private BloodMoonWaveDirector bloodMoonDirector;

    [Header("Wave UI")]
    [SerializeField] private TMP_Text waveNumberText;
    [SerializeField] private TMP_Text enemyTypeText;
    [SerializeField] private bool createMissingWaveUI = true;

    [Header("Hog Hunt Intro")]
    [SerializeField] private bool useHogHuntIntro = true;
    [SerializeField, Min(1)] private int minRegularPigKills = 3;
    [SerializeField, Min(1)] private int maxRegularPigKills = 6;
    [SerializeField, Min(0f)] private float curseWarningSeconds = 3f;
    [SerializeField] private bool showHuntMessage = true;
    [SerializeField, TextArea] private string huntMessage =
        "Hunt the wild hogs.";
    [SerializeField, TextArea] private string curseWarningMessage =
        "Something heard them...\nSomething is coming to avenge them.";

    private bool encounterStarted;
    private bool curseWarningStarted;
    private int regularPigsKilled;
    private int regularPigsNeededToTriggerCurse;
    private int enemiesExpectedThisWave;
    private Coroutine countdownRoutine;
    private Coroutine curseWarningRoutine;

    private readonly Dictionary<string, int> enemiesByType =
        new Dictionary<string, int>();

    private readonly StringBuilder enemyTypeBuilder =
        new StringBuilder();

    public int currentWave { get; private set; }
    public int enemiesRemaining { get; private set; }
    public bool waveActive { get; private set; }
    public bool EncounterStarted => encounterStarted;
    public bool ShouldSpawnRegularPigs =>
        useHogHuntIntro && !encounterStarted && !curseWarningStarted;
    public bool HogHuntFinished =>
        !useHogHuntIntro || curseWarningStarted || encounterStarted;

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

    private void OnValidate()
    {
        minRegularPigKills =
            Mathf.Max(1, minRegularPigKills);

        maxRegularPigKills =
            Mathf.Max(minRegularPigKills, maxRegularPigKills);
    }

    private void Start()
    {
        currentWave = 0;
        enemiesRemaining = 0;
        waveActive = false;

        SetupHogHunt();
        SetupWaveUI();
        RefreshWaveUI();
    }

    public void BeginEncounter()
    {
        if (encounterStarted)
            return;

        if (curseWarningRoutine != null)
        {
            StopCoroutine(curseWarningRoutine);
            curseWarningRoutine = null;
        }

        curseWarningStarted = true;
        encounterStarted = true;
        StartNextWave();

        Debug.Log("Wave encounter started.");
    }

    public void RegularPigKilled(GameObject pig)
    {
        if (!ShouldSpawnRegularPigs)
            return;

        regularPigsKilled++;
        RefreshWaveUI();

        if (regularPigsKilled >= regularPigsNeededToTriggerCurse)
        {
            StartCurseWarning();
        }
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

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        if (gameManager.instance == null)
        {
            Debug.LogError(
                "WaveManager cannot find GameManager.");
            return;
        }

        currentWave++;
        enemiesByType.Clear();

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

        enemiesExpectedThisWave = enemiesRemaining;
        waveActive = true;

        // GameManager resets and activates the existing MobSpawner.
        if (enemiesRemaining > 0)
        {
            gameManager.instance.StartNextWave(
                enemiesRemaining);
        }

        RefreshWaveUI();

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

    public void EnemySpawned(GameObject spawnedEnemy)
    {
        if (!waveActive)
            return;

        string enemyType =
            GetEnemyTypeName(spawnedEnemy);

        if (!enemiesByType.ContainsKey(enemyType))
        {
            enemiesByType.Add(enemyType, 0);
        }

        enemiesByType[enemyType]++;
        RefreshWaveUI();
    }

    // EnemyAI calls this once when an enemy dies.
    public void EnemyDefeated()
    {
        EnemyDefeated(null);
    }

    public void EnemyDefeated(GameObject defeatedEnemy)
    {
        if (!waveActive)
            return;

        if (defeatedEnemy != null)
        {
            string enemyType =
                GetEnemyTypeName(defeatedEnemy);

            if (enemiesByType.ContainsKey(enemyType))
            {
                enemiesByType[enemyType] =
                    Mathf.Max(0, enemiesByType[enemyType] - 1);
            }
        }

        enemiesRemaining =
            Mathf.Max(0, enemiesRemaining - 1);

        RefreshWaveUI();

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
        RefreshWaveUI();

        Debug.Log($"Wave {currentWave} completed.");

        if (currentWave >= totalWaves)
        {
            Debug.Log("All waves completed.");
            AllWavesCompleted?.Invoke();
            ShowAllWavesCleared();

            if (gameManager.instance != null)
            {
                gameManager.instance.youWin();
            }
        }
        else
        {
            countdownRoutine =
                StartCoroutine(WaitForNextWave());
        }
    }

    private IEnumerator WaitForNextWave()
    {
        float timeLeft = timeBetweenWaves;

        while (timeLeft > 0f)
        {
            ShowNextWaveCountdown(timeLeft);
            yield return null;
            timeLeft -= Time.deltaTime;
        }

        countdownRoutine = null;

        StartNextWave();
    }

    private void SetupHogHunt()
    {
        if (!useHogHuntIntro)
            return;

        regularPigsKilled = 0;
        curseWarningStarted = false;

        regularPigsNeededToTriggerCurse =
            UnityEngine.Random.Range(
                minRegularPigKills,
                maxRegularPigKills + 1);
    }

    private void StartCurseWarning()
    {
        if (curseWarningStarted)
            return;

        curseWarningStarted = true;
        RefreshWaveUI();

        curseWarningRoutine =
            StartCoroutine(BeginEncounterAfterCurseWarning());
    }

    private IEnumerator BeginEncounterAfterCurseWarning()
    {
        yield return new WaitForSeconds(curseWarningSeconds);

        curseWarningRoutine = null;
        BeginEncounter();
    }

    private void SetupWaveUI()
    {
        if (!createMissingWaveUI)
            return;

        if (waveNumberText != null && enemyTypeText != null)
            return;

        Canvas canvas = CreateWaveCanvas();

        if (waveNumberText == null)
        {
            waveNumberText =
                CreateUIText(
                    "Wave Number Text",
                    canvas.transform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -35f),
                    new Vector2(560f, 130f),
                    30f,
                    TextAlignmentOptions.TopLeft);
        }

        if (enemyTypeText == null)
        {
            enemyTypeText =
                CreateUIText(
                    "Enemy Type Count Text",
                    canvas.transform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -185f),
                    new Vector2(560f, 260f),
                    22f,
                    TextAlignmentOptions.TopLeft);
        }
    }

    private Canvas CreateWaveCanvas()
    {
        GameObject canvasObject =
            new GameObject("Wave HUD Canvas");

        Canvas canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler =
            canvasObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    private TMP_Text CreateUIText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject =
            new GameObject(objectName);

        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        RectTransform rect =
            text.GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;

        return text;
    }

    private void RefreshWaveUI()
    {
        if (waveNumberText != null)
        {
            if (useHogHuntIntro &&
                !encounterStarted &&
                currentWave == 0)
            {
                if (!showHuntMessage)
                {
                    waveNumberText.text =
                        string.Empty;
                }
                else if (curseWarningStarted)
                {
                    waveNumberText.text =
                        curseWarningMessage;
                }
                else
                {
                    waveNumberText.text =
                        huntMessage;
                }
            }
            else if (!encounterStarted && currentWave == 0)
            {
                waveNumberText.text =
                    string.Empty;
            }
            else if (waveActive)
            {
                waveNumberText.text =
                    $"Wave {currentWave}";
            }
            else if (currentWave > 0)
            {
                waveNumberText.text =
                    $"Wave {currentWave} Cleared";
            }
            else
            {
                waveNumberText.text =
                    "Wave 0";
            }
        }

        if (enemyTypeText == null)
            return;

        enemyTypeBuilder.Clear();

        if (useHogHuntIntro &&
            !encounterStarted &&
            currentWave == 0)
        {
            if (!showHuntMessage)
            {
                enemyTypeText.text =
                    string.Empty;
                return;
            }

            if (curseWarningStarted)
            {
                enemyTypeBuilder.Append(
                    "The woods are moving...");
            }
            else
            {
                enemyTypeBuilder.Append(
                    "Regular Hogs Killed: ");
                enemyTypeBuilder.Append(
                    regularPigsKilled.ToString());
            }

            enemyTypeText.text =
                enemyTypeBuilder.ToString();
            return;
        }

        if (!encounterStarted && currentWave == 0)
        {
            enemyTypeText.text =
                string.Empty;
            return;
        }

        if (waveActive)
        {
            enemyTypeBuilder.AppendLine(
                $"Enemies Remaining: {enemiesRemaining}/{enemiesExpectedThisWave}");

            if (enemiesByType.Count == 0)
            {
                enemyTypeBuilder.Append(
                    "Enemies spawning...");
            }
            else
            {
                foreach (KeyValuePair<string, int> enemyCount in enemiesByType)
                {
                    enemyTypeBuilder.Append(enemyCount.Key);
                    enemyTypeBuilder.Append(": ");
                    enemyTypeBuilder.AppendLine(enemyCount.Value.ToString());
                }
            }
        }
        else if (currentWave > 0)
        {
            enemyTypeBuilder.Append(
                "Wave cleared.");
        }

        enemyTypeText.text =
            enemyTypeBuilder.ToString();
    }

    private void ShowNextWaveCountdown(float timeLeft)
    {
        if (waveNumberText == null)
            return;

        int seconds =
            Mathf.CeilToInt(timeLeft);

        waveNumberText.text =
            $"Wave {currentWave} Cleared\nNext Wave In: {seconds}";
    }

    private void ShowAllWavesCleared()
    {
        if (waveNumberText != null)
        {
            waveNumberText.text =
                "All Waves Cleared";
        }

        if (enemyTypeText != null)
        {
            enemyTypeText.text =
                string.Empty;
        }
    }

    private string GetEnemyTypeName(GameObject enemy)
    {
        if (enemy == null)
            return "Unknown Enemy";

        string enemyName =
            enemy.name.Replace("(Clone)", string.Empty).Trim();

        if (string.IsNullOrEmpty(enemyName))
            return "Unknown Enemy";

        return enemyName;
    }
}
