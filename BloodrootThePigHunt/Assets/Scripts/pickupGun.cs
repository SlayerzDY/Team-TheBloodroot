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
    //==========================================================================================
    // Define Functions
    //==========================================================================================
    // Function, On Trigger Enter
    //------------------------------------------------------------------------------------------
    private void OnTriggerEnter(Collider other) {
        IPickupGun pick = other.GetComponent<IPickupGun>();
        if (pick != null) {
            gun.ammoCurr = gun.ammoMax;
            pick.getGunStats(gun); 
        }
        Dissolver dissolve = other.GetComponent <Dissolver>();
        if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); } else { Destroy(gameObject); }
    }
    //==========================================================================================
    // Function, On Send Interact
    //------------------------------------------------------------------------------------------
    public void SendInteract(Collider other) {
        IPickupGun pick = gameManager.instance.player.GetComponent<IPickupGun>();

        if (pick != null)
        {
            gun.ammoCurr = gun.ammoMax;
            pick.getGunStats(gun);
        }
        Dissolver dissolve = other.GetComponent<Dissolver>();
        if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); } else { Destroy(gameObject); }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Pickup Gun CS
//==============================================================================================