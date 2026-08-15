//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Collections;
using UnityEngine;
//==============================================================================================
// Declare Inventory UI Spawner
//==============================================================================================
public class InventoryUISpawner : MonoBehaviour {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [Header("References")]
    public GameObject slotPrefab;
    public GameObject itemUIPrefab;
    [HideInInspector] public InventorySlot[] slots;
    private Inventory playerInventory;
    private Coroutine bindRoutine;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, On Enable
    //------------------------------------------------------------------------------------------
    private void OnEnable() {
        if (bindRoutine != null) { StopCoroutine(bindRoutine); }
        bindRoutine = StartCoroutine(BindAndBuild());
    }
    //==========================================================================================
    // Function, On Disable
    //------------------------------------------------------------------------------------------
    private void OnDisable() {
        if (bindRoutine != null) { StopCoroutine(bindRoutine); bindRoutine = null; }
    }
    //==========================================================================================
    // Function, Bind and Build
    //------------------------------------------------------------------------------------------
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
    //==========================================================================================
    // Function, Refresh
    //------------------------------------------------------------------------------------------
    public void Refresh() {
        if (playerInventory == null || playerInventory.inventoryItems == null) { return; }
        BuildSlots();
        for (int i = 0; i < slots.Length; i++) {
            InventorySlot slot = slots[i];
            if (slot == null) { continue; }
            for (int c = slot.transform.childCount - 1; c >= 0; c--) {
                Destroy(slot.transform.GetChild(c).gameObject);
            }
            ItemStats stats = playerInventory.inventoryItems[i];
            if (stats == null || string.IsNullOrEmpty(stats.itemName)) { continue; }
            SpawnItemUI(stats, i, slot.transform);
        }
    }
    //==========================================================================================
    // Function, Build Slots
    //------------------------------------------------------------------------------------------
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
    //==========================================================================================
    // Function, Spawn Item UI
    //------------------------------------------------------------------------------------------
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
    //==========================================================================================
}
//==============================================================================================
// End of Inventory UI Spawner CS
//==============================================================================================