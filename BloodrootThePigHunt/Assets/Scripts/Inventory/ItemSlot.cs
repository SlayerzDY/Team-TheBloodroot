//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.EventSystems;
//==============================================================================================
// Declare Item Slot
//==============================================================================================
public class InventorySlot : MonoBehaviour, IDropHandler {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [Header("Slot")]
    [Tooltip("Must match this slot's position in Inventory.inventoryItems")]
    public int index;
    public Inventory inventory;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, On Drop
    //------------------------------------------------------------------------------------------
    public void OnDrop(PointerEventData eventData) {
        GameObject dropped = eventData.pointerDrag;
        if (dropped == null) { return; }
        inventoryItem draggedItem = dropped.GetComponent<inventoryItem>();
        if (draggedItem == null) { return; }
        if (draggedItem.index == index) { return; } // dropped back on its own slot
        inventoryItem existingItem = GetComponentInChildren<inventoryItem>();
        if (existingItem == null) {
            MoveIntoEmptySlot(draggedItem);
        }
        else if (existingItem != draggedItem) {
            SwapWithOccupiedSlot(draggedItem, existingItem);
        }
    }
    //==========================================================================================
    // Function, Move Into Empty Slot
    //------------------------------------------------------------------------------------------
    private void MoveIntoEmptySlot(inventoryItem draggedItem) {
        if (inventory == null) { return; }
        int fromIndex = draggedItem.index;
        inventory.inventoryItems[index] = inventory.inventoryItems[fromIndex];
        inventory.inventoryItems[fromIndex] = null;
        draggedItem.index = index;
        draggedItem.itemStats = inventory.inventoryItems[index];
        draggedItem.parentAfterDrag = transform;
        draggedItem.RefreshIcon();
    }
    //==========================================================================================
    // Function, Swap Slots
    //------------------------------------------------------------------------------------------
    private void SwapWithOccupiedSlot(inventoryItem draggedItem, inventoryItem existingItem) {
        if (inventory == null) { return; }
        int fromIndex = draggedItem.index;
        int toIndex = existingItem.index;
        inventory.SwapSlots(
            inventory.inventoryItems[fromIndex], fromIndex,
            inventory.inventoryItems[toIndex], toIndex
        );
        draggedItem.index = toIndex;
        draggedItem.itemStats = inventory.inventoryItems[toIndex];
        existingItem.index = fromIndex;
        existingItem.itemStats = inventory.inventoryItems[fromIndex];
        Transform originSlot = draggedItem.parentAfterDrag;
        existingItem.transform.SetParent(originSlot);
        RectTransform existingRect = existingItem.GetComponent<RectTransform>();
        if (existingRect != null) { existingRect.anchoredPosition = Vector2.zero; }
        existingItem.RefreshIcon();
        draggedItem.parentAfterDrag = transform;
        draggedItem.RefreshIcon();
    }
    //==========================================================================================
}
//==============================================================================================
// End of Item Slot CS
//==============================================================================================