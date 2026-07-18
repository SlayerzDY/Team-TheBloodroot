//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
//==============================================================================================
// Declare Pickup Gun
//==============================================================================================
public class pickupGun : MonoBehaviour, IInteract {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] gunStats gun;
    private bool hasInteracted = false;
    //==========================================================================================
    // Define Functions
    //==========================================================================================
    // Function, On Trigger Enter
    //------------------------------------------------------------------------------------------
    private void OnTriggerEnter(Collider other) {
        if (!hasInteracted) {
            IPickupGun pick = other.GetComponent<IPickupGun>();
            if (pick != null) {
                gun.ammoCurr = gun.ammoMax;
                hasInteracted = true;
                pick.getGunStats(gun);
            }
            Dissolver dissolve = other.GetComponent<Dissolver>();
            if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); } else { Destroy(gameObject); }
        }
    }
    //==========================================================================================
    // Function, On Send Interact
    //------------------------------------------------------------------------------------------
    public void SendInteract(Collider other) {
        if (!hasInteracted) {
            IPickupGun pick = gameManager.instance.player.GetComponent<IPickupGun>();
            if (pick != null) {
                gun.ammoCurr = gun.ammoMax;
                hasInteracted = true;
                pick.getGunStats(gun);
            }
            Dissolver dissolve = other.GetComponent<Dissolver>();
            if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); } else { Destroy(gameObject); }
        }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Pickup Gun CS
//==============================================================================================