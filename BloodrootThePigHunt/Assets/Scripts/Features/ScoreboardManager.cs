using TMPro;
using UnityEngine;

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


    }

    void HideScoreboard()
    {
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(false);
        }
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
