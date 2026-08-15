//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.EventSystems;
//==============================================================================================
// Declare Item Toss Zone
//==============================================================================================
public class ItemTossZone : MonoBehaviour, IDropHandler {
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, On Drop
    //------------------------------------------------------------------------------------------
    public void OnDrop(PointerEventData eventData) {
        GameObject dropped = eventData.pointerDrag;
        if (dropped == null) { return; }
        inventoryItem draggedItem = dropped.GetComponent<inventoryItem>();
        if (draggedItem == null || draggedItem.itemStats == null) { return; }
        if (gameManager.instance == null || gameManager.instance.player == null) { return; }
        gameManager.instance.player.GetComponent<Inventory>().RemoveItem(
            draggedItem.itemStats.itemName, draggedItem.itemStats.quantity, true, draggedItem.index
        );
        draggedItem.consumed = true;
        Destroy(draggedItem.gameObject);
    }
    //==========================================================================================
}
//==============================================================================================
// End of Item Toss Zone CS
//==============================================================================================