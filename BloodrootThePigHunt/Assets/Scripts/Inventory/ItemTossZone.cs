using UnityEngine;
using UnityEngine.EventSystems;

// Attach to the background GameObject that should act as a "drop to toss" target.
// Dragging an inventoryItem onto it removes it from the player's inventory and
// spawns it into the world, instead of moving it to another slot.
public class ItemTossZone : MonoBehaviour, IDropHandler {

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
}
