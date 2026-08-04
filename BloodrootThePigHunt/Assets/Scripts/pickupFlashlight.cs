//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
//==============================================================================================
// Declare Pickup Flashlight
//==============================================================================================
public class pickupFlashlight : MonoBehaviour, IInteract {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] flashlightStats flashlight;
    //==========================================================================================
    // Define Functions
    //==========================================================================================
    // Function, On Trigger Enter
    //------------------------------------------------------------------------------------------
    private void OnTriggerEnter(Collider other) {
        if (flashlight == null) {
            return;
        }

        IPickupFlashlight pick = other.GetComponent<IPickupFlashlight>();
        if (pick == null) {
            return;
        }

        flashlight.batteryCurr = flashlight.batteryMax;
        pick.getFlashlightStats(flashlight);
        if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); } else { Destroy(gameObject); }
    }
    //==========================================================================================
    // Function, On Send Interact
    //------------------------------------------------------------------------------------------
    public void SendInteract(Collider other) {
        if (flashlight == null)
        {
            return;
        }

        if (gameManager.instance == null || gameManager.instance.player == null)
        {
            return;
        }

        IPickupFlashlight pick = gameManager.instance.player.GetComponent<IPickupFlashlight>();

        if (pick == null)
        {
            return;
        }

        flashlight.batteryCurr = flashlight.batteryMax;
        pick.getFlashlightStats(flashlight);
        if (gameObject.GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); } else { Destroy(gameObject); }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Pickup Flashlight CS
//==============================================================================================
