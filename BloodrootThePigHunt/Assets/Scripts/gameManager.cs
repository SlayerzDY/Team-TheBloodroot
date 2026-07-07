//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
using Bloodroot.Features.BloodMoon;
//==============================================================================================
// Declare Game Manager
//==============================================================================================
public class gameManager : MonoBehaviour
{
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    public static gameManager instance;
    [SerializeField] GameObject menuActive;
    [SerializeField] GameObject menuPause;
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject menuMain;
    [SerializeField] GameObject menuInteract;
     
    public bool isPaused = false;
    public GameObject player;
    public playerController playerController;

    private float timer = 0;
    private float timeScaleOrig;
    private int gameGoalCount;
    private bool waveManagerControlsWin;
    //==========================================================================================
    // Function, Awake, Pre Start 
    //==========================================================================================
    void Awake()
    {
        // Create world static singleton instance of the game manager
        instance = this;
    }
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {
        timeScaleOrig = Time.timeScale;
        player = GameObject.FindWithTag("Player");
        playerController = player.GetComponent<playerController>();
        waveManagerControlsWin = FindAnyObjectByType<waveManager>() != null;
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
            else
            {
                if (menuActive == menuPause) { stateUnpause(); }
            }
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
        Time.timeScale = timeScaleOrig;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        menuActive.SetActive(false);
        menuActive = null;
    }
    //==========================================================================================
    // Function, Update Game Goal
    //==========================================================================================
    public void updateGameGoal(int amount)
    {
        gameGoalCount += amount;

        if (waveManagerControlsWin)
            return;

        if (gameGoalCount >= 10)
        {
            // You win the game
            youWin();
        }
    }
    //==========================================================================================
    // Function, Lose
    //==========================================================================================
    public void youLose()
    {
        statePause();
        menuActive = menuLose;
        menuActive.SetActive(true);
    }
    //==========================================================================================
    // Function, Win
    //==========================================================================================
    public void youWin()
    {
        statePause();
        menuActive = menuWin;
        menuActive.SetActive(true);
    }
    //==========================================================================================
    // Function, Interact Display
    //==========================================================================================
    public void InteractDisplay(bool isOn)
    {
        if (isOn)
        {
            menuActive = menuInteract;
            menuActive.SetActive(true);
        }
        else
        {
            if (menuActive != null)
            {
                if (menuActive == menuInteract)
                {
                    menuActive.SetActive(false);
                    menuActive = null;
                }
            }
        }
    }
    //

    //==========================================================================================
    // Function, StartNextWave
    //==========================================================================================

    public void StartNextWave(int enemyNum)
    {

        MobSpawner spawner = FindAnyObjectByType<MobSpawner>();

        if(spawner != null)
        {

            spawner.maxEnemies = enemyNum;

            spawner.currentEnemies = 0;

            spawner.isWaveActive = true;

        }

    }


}
//==============================================================================================
// End of Game Manager
//==============================================================================================
