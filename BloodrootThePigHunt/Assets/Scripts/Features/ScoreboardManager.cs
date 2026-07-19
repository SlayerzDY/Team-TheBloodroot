using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardManager : MonoBehaviour
{
    public static ScoreboardManager instance;

    [Header("Optional UI")]
    [SerializeField] GameObject scoreboardPanel;
    [SerializeField] TMP_Text scoreboardText;

    int regularHogsKilled;
    int enemyPigsKilled;
    int shotsFired;
    int shotsHit;
    int damageTaken;
    int wavesSurvived;
    int bloodMoonWavesSurvived;

    float runStartTime;
    bool finalScoreShown;

    public static ScoreboardManager GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        ScoreboardManager found =
            FindAnyObjectByType<ScoreboardManager>();

        if (found != null)
        {
            instance = found;
            return instance;
        }

        GameObject scoreboardObject =
            new GameObject("ScoreboardManager");

        instance =
            scoreboardObject.AddComponent<ScoreboardManager>();

        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        runStartTime = Time.time;
        HideScoreboard();
    }

    public void AddRegularHogKilled()
    {
        regularHogsKilled++;
    }

    public void AddEnemyPigKilled()
    {
        enemyPigsKilled++;
    }

    public void AddShotFired()
    {
        shotsFired++;
    }

    public void AddShotHit()
    {
        shotsHit++;
    }

    public void AddDamageTaken(int amount)
    {
        damageTaken += Mathf.Max(0, amount);
    }

    public void AddWaveSurvived(bool wasBloodMoon)
    {
        wavesSurvived++;

        if (wasBloodMoon)
        {
            bloodMoonWavesSurvived++;
        }
    }

    public void ShowFinalScore(bool won)
    {
        if (finalScoreShown)
        {
            return;
        }

        finalScoreShown = true;
        BuildScoreboardUIIfNeeded();

        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(true);
        }

        string finalText =
            GetFinalScoreText(won);

        if (scoreboardText != null)
        {
            scoreboardText.text = finalText;
        }

        Debug.Log(finalText);
    }

    void HideScoreboard()
    {
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(false);
        }
    }

    void BuildScoreboardUIIfNeeded()
    {
        if (scoreboardPanel != null && scoreboardText != null)
        {
            return;
        }

        GameObject canvasObject =
            new GameObject(
                "Scoreboard Canvas",
                typeof(RectTransform));

        Canvas canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 200;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject =
            new GameObject(
                "Hunting License Scoreboard",
                typeof(RectTransform));

        panelObject.transform.SetParent(
            canvas.transform,
            false);

        Image panelImage =
            panelObject.AddComponent<Image>();

        panelImage.color =
            new Color(0f, 0f, 0f, 0.82f);

        RectTransform panelRect =
            panelObject.GetComponent<RectTransform>();

        panelRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        panelRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        panelRect.pivot =
            new Vector2(0.5f, 0.5f);

        panelRect.anchoredPosition =
            Vector2.zero;

        panelRect.sizeDelta =
            new Vector2(680f, 520f);

        GameObject textObject =
            new GameObject(
                "Hunting License Text",
                typeof(RectTransform));

        textObject.transform.SetParent(
            panelObject.transform,
            false);

        scoreboardText =
            textObject.AddComponent<TextMeshProUGUI>();

        scoreboardText.fontSize = 28f;
        scoreboardText.alignment = TextAlignmentOptions.TopLeft;
        scoreboardText.color = Color.white;
        scoreboardText.enableWordWrapping = true;

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin =
            new Vector2(0f, 0f);

        textRect.anchorMax =
            new Vector2(1f, 1f);

        textRect.offsetMin =
            new Vector2(35f, 30f);

        textRect.offsetMax =
            new Vector2(-35f, -30f);

        scoreboardPanel =
            panelObject;
    }

    string GetFinalScoreText(bool won)
    {
        int accuracy =
            GetAccuracyPercent();

        int finalScore =
            GetFinalScore(accuracy, won);

        return
            $"HUNTING LICENSE REPORT\n\n" +
            $"Result: {(won ? "Survived" : "Lost in the woods")}\n" +
            $"Final Rank: {GetRank(finalScore)}\n" +
            $"Final Score: {finalScore}\n\n" +
            $"Regular Hogs Hunted: {regularHogsKilled}\n" +
            $"Corrupted Pigs Killed: {enemyPigsKilled}\n" +
            $"Waves Survived: {wavesSurvived}\n" +
            $"Blood Moon Waves Survived: {bloodMoonWavesSurvived}\n" +
            $"Shots Fired: {shotsFired}\n" +
            $"Shots Hit: {shotsHit}\n" +
            $"Accuracy: {accuracy}%\n" +
            $"Damage Taken: {damageTaken}\n" +
            $"Time Survived: {GetRunTimeText()}";
    }

    int GetAccuracyPercent()
    {
        if (shotsFired <= 0)
        {
            return 0;
        }

        return Mathf.RoundToInt(
            (float)shotsHit / shotsFired * 100f);
    }

    int GetFinalScore(int accuracy, bool won)
    {
        int score = 0;

        score += regularHogsKilled * 10;
        score += enemyPigsKilled * 25;
        score += wavesSurvived * 75;
        score += bloodMoonWavesSurvived * 100;
        score += accuracy;

        if (won)
        {
            score += 250;
        }

        score -= damageTaken;

        return Mathf.Max(0, score);
    }

    string GetRank(int score)
    {
        if (score >= 1200)
        {
            return "Moonlit Legend";
        }

        if (score >= 800)
        {
            return "Bloodroot Butcher";
        }

        if (score >= 400)
        {
            return "Backwoods Survivor";
        }

        return "Rookie Hunter";
    }

    string GetRunTimeText()
    {
        int totalSeconds =
            Mathf.RoundToInt(Time.time - runStartTime);

        int minutes =
            totalSeconds / 60;

        int seconds =
            totalSeconds % 60;

        return $"{minutes:00}:{seconds:00}";
    }
}
