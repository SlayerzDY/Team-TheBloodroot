//==============================================================================================
// Using Unity Engine
//==============================================================================================
using Bloodroot.Campaign;
using Bloodroot.Features.BloodMoon;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using TMPro;
using UnityEngine;
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
    [Header("Menu's")]
    // Serialize Fields
    [SerializeField] GameObject menuUtility;
    [SerializeField] GameObject menuActive;
    [SerializeField] GameObject menuPause;
    [SerializeField] GameObject menuOptions;
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject menuMain;
    [SerializeField] GameObject menuRadar;
    [SerializeField] GameObject menuInventory;
    [SerializeField] GameObject menuExtraction;
    //[SerializeField] TMP_Text gameGoalCountText;
    [Header("Text")]
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text enemyCountText;
    [SerializeField] TMP_Text congratulations;
    [Header("Other Stuf That Needs Sorted")]
    public GameObject menuInteractable;
    // Public Variables
    public GameObject checkpointPopup;
    public TextMeshProUGUI weight;
    public TextMeshProUGUI AmmoCount;
    public TextMeshProUGUI FlashlightCount;
    public Image playerHPBAR;
    public GameObject playerStam;
    public Image playerStamBar;
    public GameObject playerDamageScreen;
    public bool isPaused = false;
    public GameObject player;
    public playerController playerController;
    public GameObject playerSpawnPos;
    public int totalItemsFed = 0;
    // Private Variables
    private float timer = 0;
    private float timeScaleOrig;
    //private int gameGoalCount;
    //private bool waveManagerControlsWin;
    [Header("Tree Root Variables")]
    public TreeSpawner RootSpanw;
    public TreeRootInteraction RootInteraction;
    public bool isDefenseActive = false;
    [Range(2,5)] public int ItemsNeededPerDefense = 5;
    public bool StartBaseDefenseOnStart = true;
    [Range(1f,30f)]public float preperationTime = 15.0f;
    [Header("Dependencies")]
    public Transform playerTransform;
    [Header("Live Tracked Variables")]
    public int currentScore;
    public float currentHealth;

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
        //ScoreboardManager.GetOrCreate();

        timeText.gameObject.SetActive(false);
        enemyCountText.gameObject.SetActive(false);
        congratulations.gameObject.SetActive(false);

        //start game with the dense with x amount of enemies
        RootInteraction = FindAnyObjectByType<TreeRootInteraction>();
        RootSpanw = FindAnyObjectByType<TreeSpawner>();

        if (playerController != null)
        {
            playerController.updatePlayerAmmo();
            playerController.updatePlayerWeight();
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
            else if (menuActive != null && menuActive != menuPause) {
                menuActive.SetActive(false);
                menuActive = MenuTracker.Instance.PreviousMenu();
                menuActive.SetActive(true);
            }
            else if (menuActive == menuPause) {
                menuActive.SetActive(false);
                menuActive = null;
                MenuTracker.Instance.Clear();
                stateUnpause();
            }
        }
        if (Input.GetButtonDown("Inventory"))
        {
            if (menuActive == null)
            {
                openInventory();
            }
            else if (menuActive == menuInventory) { openInventory(false); }
        }
        if (Input.GetKeyDown(KeyCode.F5)) Save();
        if (Input.GetKeyDown(KeyCode.F9)) Load();
        timer += Time.deltaTime;
    }

    //==========================================================================================
    // Function, StatePause
    //==========================================================================================
    public void statePause() {
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
        if (menuActive == menuPause)
        {
            MenuTracker.Instance.AddMenu(menuActive);
            menuActive.SetActive(false);
            menuActive = menuOptions;
            menuActive.SetActive(true);
        }
    }
    //==========================================================================================
    // Function, Update Game Goal
    //==========================================================================================
    //public void updateGameGoal(int amount)
    //{
    //    gameGoalCount += amount;

    //    if (waveManagerControlsWin)
    //        return;

    //    if (gameGoalCountText != null)
    //    {
    //        gameGoalCountText.text = gameGoalCount.ToString("F0");
    //    }

    //    if (gameGoalCount >= 10)
    //    {
    //        // You win the game
    //        youWin();
    //    }
    //}
    //==========================================================================================
    // Function, Set Wave Manager Controls Win
    //==========================================================================================
    //public void SetWaveManagerControlsWin(bool controlsWin)
    //{
    //    waveManagerControlsWin = controlsWin;
    //}
    //==========================================================================================
    // Function, Lose
    //==========================================================================================
    public void youLose()
    {
        CampaignEventUtility.Invoke(PlayerLost, this);
        //ScoreboardManager.GetOrCreate().ShowFinalScore(false);
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
        //ScoreboardManager.GetOrCreate().ShowFinalScore(true);
        statePause();
        menuActive = menuWin;
        menuActive.SetActive(true);
    }
    //==========================================================================================
    // Function, menuInventory
    //==========================================================================================
    public void openInventory(bool isOn = true)
    {
        //ScoreboardManager.GetOrCreate().ShowFinalScore(true);
        if (isOn) {
            statePause();
            menuActive = menuInventory;
            menuActive.SetActive(isOn);
        } else {
            stateUnpause();
            menuInventory.SetActive(isOn);
            menuActive = null;
        }
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
    // Function, Update Player
    //==========================================================================================
    public void checkpoint(string name) {
        playerSpawnPos = GameObject.FindWithTag(name);
    }
    //==========================================================================================
    // Function, Add Tree Item
    //==========================================================================================

    public void AddTreeItem() {
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

        bool isFirstWave = (totalItemsFed == 1);

        bool isFuture = (!isFirstWave && totalItemsFed % ItemsNeededPerDefense == 0);

        if(isFirstWave || isFuture)
        {

            int enemiesToSpawn = 1 + (totalItemsFed * 2);
            RootInteraction.HideTreeUI();
            StartCoroutine(StartDefenseWithCountDown(enemiesToSpawn));

        }

    }

    //==========================================================================================
    // Function, Defense Wave with a countdown
    //==========================================================================================
    private IEnumerator StartDefenseWithCountDown(int enemyCount)
    {
      

        if(timeText != null)
        {

            timeText.gameObject.SetActive(true);

        }
        while (preperationTime > 0)
        { 
        
            if(timeText != null)
            {

                timeText.text = $"Wave Starts in: {preperationTime:F0}s";
                yield return new WaitForSeconds(1.0f);
                preperationTime -= 1.0f;

            }
        }
        if (timeText != null)
        {

            timeText.gameObject.SetActive(false);

        }

        if(RootSpanw != null) { RootSpanw.StartBaseDefense(enemyCount); }
        StartCheckWave();

    }

    //==========================================================================================
    // Function, Base Cleared
    //==========================================================================================

    public IEnumerator BaseCleared()
    {

        isDefenseActive = false;
        if(enemyCountText != null) { enemyCountText.gameObject.SetActive(false); }
        if (congratulations != null) { congratulations.gameObject.SetActive(true); }
        if (congratulations != null) { congratulations.text = $"Wave Defense has been cleared"; }
        yield return new WaitForSeconds(5.0f);
        if (congratulations != null) { congratulations.gameObject.SetActive(false); }
        Debug.Log("You Completed the Defense the Hub is safe");

    }

    //==========================================================================================
    // Function, Check Wave End
    //==========================================================================================
    public void StartCheckWave() {
        StartCoroutine(CheckForRemainingEnemies());
    }

    //==========================================================================================
    // Function, Check Remaining enemies
    //==========================================================================================

    private IEnumerator CheckForRemainingEnemies()
    {

        yield return new WaitForSeconds(0.2f);
        if(enemyCountText != null) { enemyCountText.gameObject.SetActive(true); }

        int enemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;
        while (enemyCount > 0)
        {

            if (enemyCountText != null) { enemyCountText.text = $"Enemies That Remain: {enemyCount}"; }
            yield return new WaitForSeconds(2f);
            enemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;

        }

        StartCoroutine(BaseCleared());
    }
    //==========================================================================================
    // Function, Stamina Bar Display
    //==========================================================================================
    public void Stamina(bool isOn) {
        if (isOn) {
            playerStam.SetActive(isOn);
        } else {
            playerStam.SetActive(isOn);
        }
    }
    //==========================================================================================
    // Function, Extraction Menu
    //==========================================================================================
    public void ExtractionMenu(bool isOn) {
        if (isOn) {
            menuActive = menuExtraction;
            menuExtraction.SetActive(isOn);
            statePause();
        } else {
            menuActive = null;
            menuExtraction.SetActive(isOn);
            stateUnpause();
        }
    }
    //==========================================================================================
    // Function, Save
    //==========================================================================================
    public void Save() {
        // Get References Needed
        playerController player = gameManager.instance.player.GetComponent<playerController>();
        if (player == null) { Debug.Log("Please Assign Player Controller!"); return; }
        Inventory playerInv = gameManager.instance.player.GetComponent<Inventory>();
        if (playerInv == null) { Debug.Log("Please Assign Player Inventory!"); return; }
        // Pass live references into the constructor
        GameData dataToSave = new GameData(player, playerInv);
        // Push data to data path using [SaveSystem](https://unity3d.com)
        SaveSystem.SaveGame(dataToSave);
    }
    //==========================================================================================
    // Function, Load
    //------------------------------------------------------------------------------------------
    public void Load() {
        // Pull the saved data from Save System
        GameData loadedData = SaveSystem.LoadGame();
        if (loadedData == null) { Debug.Log("No save data to load!"); return; }
        // Get live references
        playerController player = gameManager.instance.player.GetComponent<playerController>();
        Inventory playerInv = gameManager.instance.player.GetComponent<Inventory>();
        if (player == null || playerInv == null) { Debug.Log("Missing Player or Inventory!"); return; }
        // Apply saved stats back to the player
        player.HP = loadedData._savHP;
        player.stam = loadedData._savstam;
        player.hasFlashlight = loadedData._savhasFlashlight;
        player.gunInvPos = loadedData._savgunInvPos;
        playerInv.inventoryWeight = loadedData._savinventoryWeight;
        // Apply position
        if (loadedData._savplayerPosition != null && loadedData._savplayerPosition.Length >= 3) {
            Vector3 loadedPos = new Vector3(
                loadedData._savplayerPosition[0],
                loadedData._savplayerPosition[1],
                loadedData._savplayerPosition[2]
            );
            player.transform.position = loadedPos;
        }
        // Apply inventory & guns
        if (loadedData._savInventory != null) {
            playerInv.inventoryItems = (ItemStats[])loadedData._savInventory.Clone();
        }
        if (loadedData._savgunInv != null) {
            player.gunInv = new List<gunStats>(loadedData._savgunInv);
        }
        Debug.Log("Game loaded successfully!");
    }
    //==========================================================================================
}
//==============================================================================================
// End of Game Manager
//==============================================================================================
