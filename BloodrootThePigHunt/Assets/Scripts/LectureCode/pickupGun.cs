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
    [SerializeField] public gunStats gun;
    bool pickedUp;
    //==========================================================================================
    // Define Functions
    //==========================================================================================
    // Function, On Trigger Enter
    //------------------------------------------------------------------------------------------
    private void OnTriggerEnter(Collider other) {
        IPickupGun pick = other.GetComponent<IPickupGun>();
        if (pick == null)
        {
            pick = other.GetComponentInParent<IPickupGun>();
        }

        giveGunToPlayer(pick);
    }
    //==========================================================================================
    // Function, On Send Interact
    //------------------------------------------------------------------------------------------
    public void SendInteract(Collider other) {
        IPickupGun pick = null;

        if (gameManager.instance != null && gameManager.instance.player != null)
        {
            pick = gameManager.instance.player.GetComponent<IPickupGun>();
        }

        if (pick == null)
        {
            playerController player =
                FindAnyObjectByType<playerController>();

            if (player != null)
            {
                pick = player;
            }
        }

        giveGunToPlayer(pick);
    }
    //==========================================================================================
    // Function, Give Gun To Player
    //------------------------------------------------------------------------------------------
    void giveGunToPlayer(IPickupGun pick)
    {
        if (pickedUp || pick == null || gun == null)
        {
            return;
        }

        pickedUp = true;
        gun.ammoCurr = gun.ammoMax;
        pick.getGunStats(gun);
        removePickup();
    }
    //==========================================================================================
    // Function, Remove Pickup
    //------------------------------------------------------------------------------------------
    void removePickup()
    {
        Dissolver dissolve = GetComponent<Dissolver>();

        if (dissolve != null)
        {
            this.GetComponent<Dissolver>().StartCoroutine(dissolve.dissolve());
        }
        else
        {
            Destroy(gameObject);
        }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Pickup Gun CS
//==============================================================================================
