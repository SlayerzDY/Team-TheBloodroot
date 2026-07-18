//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Checkpoint
//==============================================================================================
public class checkpoint : MonoBehaviour
{
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && gameManager.instance.playerSpawnPos.transform.position != transform.position)
        {
            if (other.CompareTag("Player")) { gameManager.instance.playerSpawnPos.transform.position = transform.position; }
            StartCoroutine(displayPopup());
        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    IEnumerator displayPopup()
    {
        gameManager.instance.checkpointPopup.SetActive(true);
        yield return new WaitForSeconds(1);
        gameManager.instance.checkpointPopup.SetActive(false);
    }
    //==========================================================================================
}
//==============================================================================================
// End of Checkpoint CS
//==============================================================================================