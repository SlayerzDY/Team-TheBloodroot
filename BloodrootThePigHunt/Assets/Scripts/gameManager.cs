//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
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
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject menuMain;
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
    // Private Variables
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
        ScoreboardManager.GetOrCreate();
        updatePlayer();
        setupPlayerHUD();

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

       gameGoalCountText.text = gameGoalCount.ToString("F0");

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
        ScoreboardManager.GetOrCreate().ShowFinalScore(false);
        statePause();
        menuActive = menuLose;
        menuActive.SetActive(true);
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

        if(spawner == null)
        {
            Debug.LogError(
                "GameManager cannot start the next wave because no MobSpawner was found.");

            return false;
        }

        spawner.StartWave(enemyNum);
        return true;

    }
    //==========================================================================================
    // Function, Update Player
    //==========================================================================================
    public void updatePlayer()
    {
        timeScaleOrig = Time.timeScale;
        player = GameObject.FindWithTag("Player");
        playerController = player.GetComponent<playerController>();
        playerSpawnPos = GameObject.FindWithTag("PlayerSpawnPos");
    }
    //==========================================================================================
    // Function, Setup Player HUD
    //==========================================================================================
    void setupPlayerHUD()
    {
        if (AmmoCount != null && FlashlightCount != null)
            return;

        GameObject canvasObject = new GameObject("Player HUD Canvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        if (AmmoCount == null)
        {
            AmmoCount = createPlayerHUDText(
                "Ammo Count",
                canvas.transform,
                new Vector2(25f, 90f),
                "0 / 0");
        }

        if (FlashlightCount == null)
        {
            FlashlightCount = createPlayerHUDText(
                "Flashlight Count",
                canvas.transform,
                new Vector2(25f, 55f),
                "Flashlight: --");
        }
    }
    //==========================================================================================
    // Function, Create Player HUD Text
    //==========================================================================================
    TextMeshProUGUI createPlayerHUDText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        string startText)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(360f, 35f);

        text.text = startText;
        text.fontSize = 24f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;

        return text;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Game Manager
//==============================================================================================
