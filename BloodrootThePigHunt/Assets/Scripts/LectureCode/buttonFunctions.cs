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

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    //==========================================================================================
    // Function, Quit
    //==========================================================================================
    public void quit() {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();     
    #endif
    }
    //==========================================================================================
    // Function, Open Level
    //==========================================================================================
    public void OpenLevel(string levelName) {
        playerController controller = gameManager.instance.player.GetComponent<playerController>();
        if (controller == null) { return; }
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
