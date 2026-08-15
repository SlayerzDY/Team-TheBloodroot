using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IDropHandler
{

    [Header("Slot")]
    [Tooltip("Must match this slot's position in Inventory.inventoryItems")]
    public int index;
    public Inventory inventory;

    public void OnDrop(PointerEventData eventData)
    {
        GameObject dropped = eventData.pointerDrag;
        if (dropped == null) { return; }

        inventoryItem draggedItem = dropped.GetComponent<inventoryItem>();
        if (draggedItem == null) { return; }
        if (draggedItem.index == index) { return; } // dropped back on its own slot

        inventoryItem existingItem = GetComponentInChildren<inventoryItem>();

        if (existingItem == null)
        {
            MoveIntoEmptySlot(draggedItem);
        }
        else if (existingItem != draggedItem)
        {
            SwapWithOccupiedSlot(draggedItem, existingItem);
        }
    }

    // Target slot is empty - SwapSlots() can't do this (it requires both sides to
    // already hold an item), so move the data directly.
    private void MoveIntoEmptySlot(inventoryItem draggedItem)
    {
        if (inventory == null) { return; }
        int fromIndex = draggedItem.index;

        inventory.inventoryItems[index] = inventory.inventoryItems[fromIndex];
        inventory.inventoryItems[fromIndex] = null;

        draggedItem.index = index;
        draggedItem.itemStats = inventory.inventoryItems[index];
        draggedItem.parentAfterDrag = transform;
        draggedItem.RefreshIcon();
    }

    // Target slot is occupied by a different item - swap both the UI and the
    // backing ItemStats using the Inventory's existing SwapSlots().
    private void SwapWithOccupiedSlot(inventoryItem draggedItem, inventoryItem existingItem)
    {
        if (inventory == null) { return; }
        int fromIndex = draggedItem.index;
        int toIndex = existingItem.index;

        inventory.SwapSlots(
            inventory.inventoryItems[fromIndex], fromIndex,
            inventory.inventoryItems[toIndex], toIndex
        );

        // SwapSlots() writes fresh ItemStats copies into the array, so pull those
        // back into the UI rather than trusting the references we had before.
        draggedItem.index = toIndex;
        draggedItem.itemStats = inventory.inventoryItems[toIndex];

        existingItem.index = fromIndex;
        existingItem.itemStats = inventory.inventoryItems[fromIndex];

        // Send the item that was already here back to the dragged item's old slot.
        Transform originSlot = draggedItem.parentAfterDrag;
        existingItem.transform.SetParent(originSlot);
        RectTransform existingRect = existingItem.GetComponent<RectTransform>();
        if (existingRect != null) { existingRect.anchoredPosition = Vector2.zero; }
        existingItem.RefreshIcon();

        draggedItem.parentAfterDrag = transform;
        draggedItem.RefreshIcon();
    }
}
