//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System;
using System.Collections;
using Bloodroot.Campaign;
using Bloodroot.Features.BloodMoon;
using TMPro;
using UnityEngine.UI;
//==============================================================================================
// Declare Game Manager
//==============================================================================================
public class gameManager : MonoBehaviour
{
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    // Game Manager Instance, Creates Singleton
    public static gameManager instance;
    // Serialize Fields
    [SerializeField] GameObject menuActive;
    [SerializeField] GameObject menuPause;
    [SerializeField] GameObject menuOptions;
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject menuMain;
    [SerializeField] GameObject menuRadar;
    [SerializeField] TMP_Text gameGoalCountText;
    public GameObject menuInteractable;
    // Public Variables
    public GameObject checkpointPopup;
    public TextMeshProUGUI AmmoCount;
    public TextMeshProUGUI FlashlightCount;
    public Image playerHPBAR;
    public GameObject playerDamageScreen;
    public bool isPaused = false;
    public GameObject player;
    public playerController playerController;
    public GameObject playerSpawnPos;
    public bool isDefenseActive = false;
    public int totalItemsFed = 0;
    [Range(2,5)] public int ItemsNeededPerDefense = 5;
    public bool StartBaseDefenseOnStart = true;
    // Private Variables
    private float timer = 0;
    private float timeScaleOrig;
    private int gameGoalCount;
    private bool waveManagerControlsWin;
    // Refrences
    public TreeSpawner RootSpanw;
    public TreeRootInteraction RootInteraction;

    /// <summary>
    /// Lifecycle hooks for campaign-owned encounters. Listeners must not own
    /// the lose menu or player health; they use these notifications to pause
    /// or resume their own state machines.
    /// </summary>
    public event Action PlayerLost;
    public event Action PlayerRespawned;
    //==========================================================================================
    // Function, Awake, Pre Start 
    //==========================================================================================
    void Awake()
    {
        // Create world static singleton instance of the game manager
        instance = this;
        updatePlayer();
    }
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {
        timeScaleOrig = GetPlayableTimeScale(Time.timeScale);
        ScoreboardManager.GetOrCreate();

        //start game with the dense with x amount of enemies
        RootInteraction = FindAnyObjectByType<TreeRootInteraction>();
        RootSpanw = FindAnyObjectByType<TreeSpawner>();
        if(RootSpanw != null && StartBaseDefenseOnStart) { RootSpanw.StartBaseDefense(10); }

        if (playerController != null)
        {
            playerController.updatePlayerAmmo();
        }
    }
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update()
    {
        if (Input.GetButtonDown("Cancel"))
        {
            if (menuActive == null)
            {
                statePause();
                menuActive = menuPause;
                menuActive.SetActive(true);
            }
            else if (menuActive == menuPause) { stateUnpause(); }
        }
        timer += Time.deltaTime;
    }
    //==========================================================================================
    // Function, StatePause
    //==========================================================================================
    public void statePause()
    {
        isPaused = true;
        Time.timeScale = 0;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    //==========================================================================================
    // Function, StateUnpause
    //==========================================================================================
    public void stateUnpause()
    {
        isPaused = false;
        timeScaleOrig = GetPlayableTimeScale(timeScaleOrig);
        Time.timeScale = timeScaleOrig;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (menuActive != null)
        {
            menuActive.SetActive(false);
        }

        menuActive = null;
    }
    //==========================================================================================
    // Function, OptionsMenu
    //==========================================================================================
    public void OptionsMenu()
    {
        statePause();
        if (menuActive == menuPause)
        {
            menuActive = menuOptions;
            menuActive.SetActive(true);
        }
    }
    //==========================================================================================
    // Function, Update Game Goal
    //==========================================================================================
    public void updateGameGoal(int amount)
    {
        gameGoalCount += amount;

        if (waveManagerControlsWin)
            return;

        if (gameGoalCountText != null)
        {
            gameGoalCountText.text = gameGoalCount.ToString("F0");
        }

        if (gameGoalCount >= 10)
        {
            // You win the game
            youWin();
        }
    }
    //==========================================================================================
    // Function, Set Wave Manager Controls Win
    //==========================================================================================
    public void SetWaveManagerControlsWin(bool controlsWin)
    {
        waveManagerControlsWin = controlsWin;
    }
    //==========================================================================================
    // Function, Lose
    //==========================================================================================
    public void youLose()
    {
        CampaignEventUtility.Invoke(PlayerLost, this);
        ScoreboardManager.GetOrCreate().ShowFinalScore(false);
        statePause();
        menuActive = menuLose;

        if (menuActive != null)
        {
            menuActive.SetActive(true);
        }
    }

    /// <summary>
    /// Called by the authored respawn button after the existing player
    /// controller has moved and restored the player.
    /// </summary>
    public void NotifyPlayerRespawned()
    {
        CampaignEventUtility.Invoke(PlayerRespawned, this);
    }
    //==========================================================================================
    // Function, Win
    //==========================================================================================
    public void youWin()
    {
        ScoreboardManager.GetOrCreate().ShowFinalScore(true);
        statePause();
        menuActive = menuWin;
        menuActive.SetActive(true);
    }
    //==========================================================================================
    // Function, StartNextWave
    //==========================================================================================

    public bool StartNextWave(int enemyNum)
    {

        MobSpawner spawner = FindAnyObjectByType<MobSpawner>();

        if (spawner == null)
        {
            Debug.LogError(
                "GameManager cannot start the next wave because no MobSpawner was found.");

            return false;
        }

        spawner.StartWave(enemyNum);
        return true;

    }
    //==========================================================================================
    // Function, Radar
    //==========================================================================================
    public void ActivateRadar(bool on = true) {
        menuRadar.SetActive(on);
    }
    //==========================================================================================
    // Function, Update Player
    //==========================================================================================
    public void updatePlayer()
    {
        timeScaleOrig = GetPlayableTimeScale(Time.timeScale);
        player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            playerController = null;
            playerSpawnPos = GameObject.FindWithTag("PlayerSpawnPos");
            Debug.LogError("GameManager could not find an active Player object.");
            return;
        }

        playerController = player.GetComponent<playerController>();
        playerSpawnPos = GameObject.FindWithTag("PlayerSpawnPos");
    }

    private static float GetPlayableTimeScale(float candidate)
    {
        return candidate > 0f ? candidate : 1f;
    }
    //==========================================================================================
    // Function, Add Tree Item
    //==========================================================================================

    public void AddTreeItem()
    {

        totalItemsFed++;
        CheckTreeMileStone();

    }
    //==========================================================================================
    // Function, Check Mile Stone
    //==========================================================================================

    private void CheckTreeMileStone()
    {

        //unlock levels here 
        // ex could be depending on cody's code if(totalItemsFed == X){ Unlock(x) }
        

        //Base Defense stuff here
        if(totalItemsFed % ItemsNeededPerDefense == 0)
        {

            int enemiesToSpawn = 10 + (totalItemsFed * 2);
            RootInteraction.HideTreeUI();
            RootSpanw.StartBaseDefense(enemiesToSpawn);

        }

    }
    //==========================================================================================
    // Function, Base Cleared
    //==========================================================================================
    public void BaseCleared()
    {

        isDefenseActive = false;
        Debug.Log("You Completed the Defense the Hub is safe");

    }
    //==========================================================================================
    // Function, Check Wave End
    //==========================================================================================

    public void StartCheckWave()
    {

        StartCoroutine(CheckForRemainingEnemies());

    }
    //==========================================================================================
    // Function, Check Remaining enemies
    //==========================================================================================

    private IEnumerator CheckForRemainingEnemies()
    {

        yield return new WaitForSeconds(5f);

        while(GameObject.FindGameObjectsWithTag("Enemy").Length > 0)
        {

            yield return new WaitForSeconds(2f);

        }

        BaseCleared();
    }
}
//==============================================================================================
// End of Game Manager
//==============================================================================================
