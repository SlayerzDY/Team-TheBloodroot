//==============================================================================================
// Using Unity Engine
//==============================================================================================
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
//==============================================================================================
// Declare UI Button Functions
//==============================================================================================
public class buttonFunctions : MonoBehaviour {
    //==========================================================================================
    // Function, Resume
    //==========================================================================================
    public void resume() {
        gameManager.instance.Save();
        gameManager.instance.stateUnpause();
    }
    //==========================================================================================
    // Function, Options
    //==========================================================================================
    public void options()
    {
        gameManager.instance.OptionsMenu();
    }
    //==========================================================================================
    // Function, Respawn
    //==========================================================================================
    public void respawn()
    {
        gameManager manager = gameManager.instance;

        if (manager == null)
        {

            return;
        }

        if (manager.playerController == null)
        {
            manager.updatePlayer();
        }

        if (manager.playerController == null)
        {

            return;
        }
        gameManager.instance.Save();
        manager.playerController.spawnPlayer();
        manager.NotifyPlayerRespawned();
        manager.stateUnpause();
    }

    //==========================================================================================
    // Function, Return to MainMenu
    //==========================================================================================

    public void ReturnToMainMenu()
    {
        gameManager.instance.Save();
        OpenLevel("MainMenu");
    }

    //==========================================================================================
    // Function, Return to Hub
    //==========================================================================================

    public void ReturnToHub()
    {
        gameManager manager = gameManager.instance;

        if (manager == null)
        {

            return;
        }

        if (manager.playerController == null)
        {
            manager.updatePlayer();
        }

        if (manager.playerController == null)
        {

            return;
        }
        if (gameManager.instance != null)
        {
            // Restore real gameplay time before the replacement scene starts.
            // This prevents a freshly loaded manager from capturing a paused
            // time scale as its baseline.
            gameManager.instance.stateUnpause();
        }

        gameManager.instance.Save();
        OpenLevelHub("Farm_PrologueHub");
        //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        //manager.playerController.spawnPlayer();
        //manager.NotifyPlayerRespawned();
        //manager.stateUnpause();
    }

    //==========================================================================================
    // Function, Restart
    //==========================================================================================
    public void restart() {
        if (gameManager.instance != null)
        {
            // Restore real gameplay time before the replacement scene starts.
            // This prevents a freshly loaded manager from capturing a paused
            // time scale as its baseline.
            gameManager.instance.stateUnpause();
        }
        gameManager.instance.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    //==========================================================================================
    // Function, Quit
    //==========================================================================================
    public void quit() {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#elif UNITY_EDITOR
        gameManager.instance.Save();    
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevel(string levelName) {
        gameManager.instance.Save();
        playerController controller = gameManager.instance.player.GetComponent<playerController>();
        if (controller == null) { return; }
        controller.LoadLevel(levelName);
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelBlackPines(string levelName) {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            controller.LoadLevel(levelName);
            // Accepted truck travel must not carry the extraction pause
            // into the next scene. A failed load leaves the menu unchanged.
            if (!string.IsNullOrEmpty(levelName) && gameManager.instance != null)
                gameManager.instance.stateUnpause();
            gameManager.instance.checkpoint("PlayerSpawnPos");
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelStillwater(string levelName) {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            controller.LoadLevel(levelName);
        gameManager.instance.checkpoint("PlayerSpawnPos");
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelHarrow(string levelName) {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            controller.LoadLevel(levelName);
        gameManager.instance.checkpoint("PlayerSpawnPos");
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelBloodRoot(string levelName) {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            controller.LoadLevel(levelName);
        gameManager.instance.checkpoint("PlayerSpawnPos");
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelHub(string levelName) {
        gameManager.instance.Save();
        playerController controller = gameManager.instance.player.GetComponent<playerController>();
        if (controller == null) { return; }
        controller.LoadLevel(levelName);
        gameManager.instance.checkpoint("PlayerSpawnPosHub");
    }
    //==========================================================================================
    // Function, Extraction Close
    //==========================================================================================
    public void ExtractionClose() {
        gameManager.instance.ExtractionMenu(false);
    }
    //==========================================================================================
}
//==============================================================================================
// End of Declare UI Button Functions
//==============================================================================================
