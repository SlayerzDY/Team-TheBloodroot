//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Rotate
//==============================================================================================
public class lockedExtraction: MonoBehaviour, IInteract {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [Header("Extraction Settings")]
    [SerializeField] Item key;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, Send Interact
    //==========================================================================================
    public void SendInteract(Collider target) {
        if (key == null || key.item == null || string.IsNullOrEmpty(key.item.itemName)) { return; }
        if (gameManager.instance == null || gameManager.instance.player == null) { return; }
        if (!playerHasItem()) {
            gameManager.instance.ToastMenu(true, $"You need a {key.item.itemName} to open");
            return; 
        } 
        gameManager.instance.ExtractionMenu(true);
    }
    //==========================================================================================
    // Function, Send Interact
    //==========================================================================================
    private bool playerHasItem() {
        Inventory playerInv = gameManager.instance.player.GetComponent<Inventory>();
        if (playerInv == null) { return false; }
        ItemStats[] items = playerInv.inventoryItems;
        if (playerInv == null || items.Length == 0) { return false; }
        for (int i = 0; i < items.Length; i++) {
            if (items[i] == null) { continue; } 
            if (items[i].itemName == null) { continue; }
            if (items[i].itemName == key.item.itemName) { return true; }
        }
        return false;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Rotate CS
//==============================================================================================