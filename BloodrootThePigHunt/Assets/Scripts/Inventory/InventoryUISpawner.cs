//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Collections;
using UnityEngine;
//==============================================================================================
// Declare Inventory UI Spawner
//==============================================================================================
public class InventoryUISpawner : MonoBehaviour {

    [Header("References")]
    [Tooltip("Prefab with an InventorySlot component (and your slot background/frame)")]
    public GameObject slotPrefab;

    [Tooltip("Prefab with an Image + inventoryItem component on it")]
    public GameObject itemUIPrefab;

    [HideInInspector] public InventorySlot[] slots;

    private Inventory playerInventory;
    private Coroutine bindRoutine;

    private void OnEnable() {
        if (bindRoutine != null) { StopCoroutine(bindRoutine); }
        bindRoutine = StartCoroutine(BindAndBuild());
    }

    private void OnDisable() {
        if (bindRoutine != null) { StopCoroutine(bindRoutine); bindRoutine = null; }
    }

    private IEnumerator BindAndBuild() {
        while (gameManager.instance == null || gameManager.instance.player == null) {
            yield return null;
        }
        playerInventory = gameManager.instance.player.GetComponent<Inventory>();
        while (playerInventory == null || playerInventory.inventoryItems == null) {
            yield return null;
        }
        Refresh();
        bindRoutine = null;
    }

    // Call this any time the inventory changes (pickup, drop, use, swap from a
    // non-drag source) to rebuild the UI from Inventory.inventoryItems. Drag/drop
    // between slots updates itself and doesn't need a call here.
    public void Refresh() {
        if (playerInventory == null || playerInventory.inventoryItems == null) { return; }
        BuildSlots();
        for (int i = 0; i < slots.Length; i++) {
            InventorySlot slot = slots[i];
            if (slot == null) { continue; }
            // Clear whatever item's currently in the slot before rebuilding it
            for (int c = slot.transform.childCount - 1; c >= 0; c--) {
                Destroy(slot.transform.GetChild(c).gameObject);
            }
            ItemStats stats = playerInventory.inventoryItems[i];
            if (stats == null || string.IsNullOrEmpty(stats.itemName)) { continue; }
            SpawnItemUI(stats, i, slot.transform);
        }
    }

    // Spawns one InventorySlot per Inventory array index. If the inventory's size
    // has changed since we last built (or this is the first run), tear down and
    // rebuild from scratch so slot count always matches inventoryItems.Length.
    private void BuildSlots() {
        int size = playerInventory.inventoryItems.Length;
        if (slots != null && slots.Length == size) { return; }
        if (slotPrefab == null) {
            Debug.LogWarning("InventoryUISpawner: slotPrefab is not assigned - cannot build slots.");
            return;
        }
        foreach (Transform child in transform) {
            Destroy(child.gameObject);
        }
        slots = new InventorySlot[size];
        for (int i = 0; i < size; i++) {
            GameObject slotGO = Instantiate(slotPrefab, transform);
            InventorySlot slot = slotGO.GetComponent<InventorySlot>();
            if (slot == null) {
                Debug.LogWarning("InventoryUISpawner: slotPrefab has no InventorySlot component.");
                continue;
            }
            slot.index = i;
            slot.inventory = playerInventory;
            slots[i] = slot;
        }
    }

    private void SpawnItemUI(ItemStats stats, int index, Transform parentSlot) {
        if (itemUIPrefab == null) { return; }
        GameObject itemGO = Instantiate(itemUIPrefab, parentSlot);
        RectTransform rect = itemGO.GetComponent<RectTransform>();
        if (rect != null) { rect.anchoredPosition = Vector2.zero; }
        inventoryItem uiItem = itemGO.GetComponent<inventoryItem>();
        if (uiItem == null) { return; }
        uiItem.itemStats = stats;
        uiItem.index = index;
        uiItem.inventory = playerInventory;
        uiItem.RefreshIcon();
    }
}
//==============================================================================================
// End of Inventory UI Spawner CS
//==============================================================================================