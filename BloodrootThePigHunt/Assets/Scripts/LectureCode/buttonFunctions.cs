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
            Debug.LogError("Respawn requires the authored GameManager.");
            return;
        }

        if (manager.playerController == null)
        {
            manager.updatePlayer();
        }

        if (manager.playerController == null)
        {
            Debug.LogError("Respawn requires the authored player controller.");
            return;
        }
        gameManager.instance.Save();
        manager.playerController.spawnPlayer();
        manager.NotifyPlayerRespawned();
        manager.stateUnpause();
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
#if UNITY_EDITOR
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
        if (gameManager.instance.DefensesBeat >= 1)
        {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            controller.LoadLevel(levelName);
            gameManager.instance.checkpoint("PlayerSpawnPosBlackPines");
        }
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelStillwater(string levelName) {
        if (gameManager.instance.DefensesBeat >= 2)
        {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            gameManager.instance.checkpoint("PlayerSpawnPosStillwater");
            controller.LoadLevel(levelName);
        }
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelHarrow(string levelName) {
        if (gameManager.instance.DefensesBeat >= 3)
        {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            gameManager.instance.checkpoint("PlayerSpawnPosHarrowEstate");
            controller.LoadLevel(levelName);
        }
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelBloodRoot(string levelName) {
        if (gameManager.instance.DefensesBeat >= 4)
        {
            gameManager.instance.Save();
            playerController controller = gameManager.instance.player.GetComponent<playerController>();
            if (controller == null) { return; }
            gameManager.instance.checkpoint("PlayerSpawnPosBloodRootHollow");
            controller.LoadLevel(levelName);

        }
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevelHub(string levelName) {
        gameManager.instance.Save();
        playerController controller = gameManager.instance.player.GetComponent<playerController>();
        if (controller == null) { return; }
        gameManager.instance.checkpoint("PlayerSpawnPosHub");
        controller.LoadLevel(levelName);
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
