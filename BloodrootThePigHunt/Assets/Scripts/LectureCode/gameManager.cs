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
    public static gameManager instance;

    [Header("Menu's")]
    [SerializeField] GameObject menuUtility;
    [SerializeField] GameObject menuActive;
    [SerializeField] GameObject menuPause;
    [SerializeField] GameObject menuOptions;
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject menuMain;
    [SerializeField] GameObject menuRadar;
    [SerializeField] GameObject menuInventory;

    [Header("Text")]
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text enemyCountText;
    [SerializeField] TMP_Text congratulations;

    [Header("Other Stuff That Needs Sorted")]
    public GameObject menuInteractable;
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

    [Header("Tree Root Variables")]
    public TreeSpawner RootSpanw;
    public TreeRootInteraction RootInteraction;
    public bool isDefenseActive = false;
    [Range(2, 5)] public int ItemsNeededPerDefense = 5;
    public bool StartBaseDefenseOnStart = true;
    [Range(1f, 30f)] public float preperationTime = 15.0f;

    /// <summary>
    /// Lifecycle hooks for campaign-owned encounters.
    /// </summary>
    public event Action PlayerLost;
    public event Action PlayerRespawned;

    //==========================================================================================
    // Function, Awake, Pre Start 
    //==========================================================================================
    void Awake()
    {
        instance = this;
        updatePlayer();
    }

    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {
        timeScaleOrig = GetPlayableTimeScale(Time.timeScale);

        if (timeText != null) timeText.gameObject.SetActive(false);
        if (enemyCountText != null) enemyCountText.gameObject.SetActive(false);
        if (congratulations != null) congratulations.gameObject.SetActive(false);

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
            else if (menuActive != null && menuActive != menuPause)
            {
                menuActive.SetActive(false);
                menuActive = MenuTracker.Instance.PreviousMenu();
                if (menuActive != null) menuActive.SetActive(true);
            }
            else if (menuActive == menuPause)
            {
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
                openInventory(true);
            }
            else if (menuActive == menuInventory)
            {
                openInventory(false);
            }
        }

        timer += Time.deltaTime;
    }

    //==========================================================================================
    // Function, StatePause & StateUnpause
    //==========================================================================================
    public void statePause()
    {
        isPaused = true;
        Time.timeScale = 0;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

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
    // Function, Win & Lose
    //==========================================================================================
    public void youLose()
    {
        CampaignEventUtility.Invoke(PlayerLost, this);
        statePause();
        menuActive = menuLose;

        if (menuActive != null)
        {
            menuActive.SetActive(true);
        }
    }

    public void NotifyPlayerRespawned()
    {
        CampaignEventUtility.Invoke(PlayerRespawned, this);
    }

    public void youWin()
    {
        statePause();
        menuActive = menuWin;
        if (menuActive != null) menuActive.SetActive(true);
    }

    //==========================================================================================
    // Function, UI Controls (Inventory, Radar, Stamina)
    //==========================================================================================
    public void openInventory(bool isOn = true)
    {
        if (isOn)
        {
            statePause();
            menuActive = menuInventory;
            if (menuActive != null) menuActive.SetActive(true);
        }
        else
        {
            if (menuInventory != null) menuInventory.SetActive(false);
            stateUnpause();
        }
    }

    public void showInventory(Inventory inventory)
    {
        if (inventory == null) return;
        openInventory(true);
    }

    public void ActivateRadar(bool on = true)
    {
        if (menuRadar != null) menuRadar.SetActive(on);
    }

    public void showStamina(bool isOn)
    {
        if (playerStam != null) playerStam.SetActive(isOn);
    }

    //==========================================================================================
    // Function, Spawner & Player Setup
    //==========================================================================================
    public bool StartNextWave(int enemyNum)
    {
        MobSpawner spawner = FindAnyObjectByType<MobSpawner>();

        if (spawner == null)
        {
            Debug.LogError("GameManager cannot start the next wave because no MobSpawner was found.");
            return false;
        }

        spawner.StartWave(enemyNum);
        return true;
    }

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
    // Function, Tree MileStone & Defense
    //==========================================================================================
    public void AddTreeItem()
    {
        totalItemsFed++;
        CheckTreeMileStone();
    }

    private void CheckTreeMileStone()
    {
        bool isFirstWave = (totalItemsFed == 1);
        bool isFuture = (!isFirstWave && totalItemsFed % ItemsNeededPerDefense == 0);

        if (isFirstWave || isFuture)
        {
            int enemiesToSpawn = 1 + (totalItemsFed * 2);
            if (RootInteraction != null) RootInteraction.HideTreeUI();
            StartCoroutine(StartDefenseWithCountDown(enemiesToSpawn));
        }
    }

    private IEnumerator StartDefenseWithCountDown(int enemyCount)
    {
        if (timeText != null) timeText.gameObject.SetActive(true);

        while (preperationTime > 0)
        {
            if (timeText != null)
            {
                timeText.text = $"Wave Starts in: {preperationTime:F0}s";
            }
            yield return new WaitForSeconds(1.0f);
            preperationTime -= 1.0f;
        }

        if (timeText != null) timeText.gameObject.SetActive(false);
        if (RootSpanw != null) RootSpanw.StartBaseDefense(enemyCount);

        StartCheckWave();
    }

    public IEnumerator BaseCleared()
    {
        isDefenseActive = false;
        if (enemyCountText != null) enemyCountText.gameObject.SetActive(false);
        if (congratulations != null)
        {
            congratulations.gameObject.SetActive(true);
            congratulations.text = "Wave Defense has been cleared";
        }

        yield return new WaitForSeconds(5.0f);

        if (congratulations != null) congratulations.gameObject.SetActive(false);
        Debug.Log("You Completed the Defense the Hub is safe");
    }

    public void StartCheckWave()
    {
        StartCoroutine(CheckForRemainingEnemies());
    }

    private IEnumerator CheckForRemainingEnemies()
    {
        yield return new WaitForSeconds(0.2f);
        if (enemyCountText != null) enemyCountText.gameObject.SetActive(true);

        int enemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;
        while (enemyCount > 0)
        {
            if (enemyCountText != null) enemyCountText.text = $"Enemies That Remain: {enemyCount}";
            yield return new WaitForSeconds(2f);
            enemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;
        }

        StartCoroutine(BaseCleared());
    }
}