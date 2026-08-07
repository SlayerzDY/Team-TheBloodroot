//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Linq;
using UnityEngine;
using static UnityEditor.Progress;
//==============================================================================================
// Declare Inventory
//==============================================================================================
public class Inventory : MonoBehaviour {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    // Public
    // Item Inventory Array
    [Range(1, 100)] [SerializeField] int inventorySize;
    [SerializeField] ItemStats[] inventoryItems;
    [Range(1, 100)][SerializeField] float distance = 2.0f;
    // Private
    private ItemStats currItem;
    private ItemStats tempItem;
    private int currItemAmount;
    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Function, Add Item
    //------------------------------------------------------------------------------------------
    public void Start() {
        // Initialize Inventory
        inventoryItems = new ItemStats[inventorySize];
    }
    //==========================================================================================
    // Function, Add Item
    //------------------------------------------------------------------------------------------
    public void AddItem(GameObject objectSpawn) {
        // Safety Check, If not Item Return
        if (objectSpawn.GetComponent<Item>() == null) { return; }
        if (currItemAmount >= inventorySize) {
            SpawnItem(objectSpawn);
            return;
        }
        FindNextAvailableIndex(objectSpawn);
    }
    //==========================================================================================
    // Function, Remove Item
    //------------------------------------------------------------------------------------------
    public void RemoveItem(ItemStats item) {

    }
    //==========================================================================================
    // Function, Spawn Item
    //------------------------------------------------------------------------------------------
    private void SpawnItem(GameObject objectSpawn) {
        Vector3 frontPosition = gameManager.instance.player.transform.position + (gameManager.instance.player.transform.forward * distance);
        Quaternion localRotation = gameManager.instance.player.transform.localRotation;
        //objectSpawn.GetComponent<Item>().item = item;
        objectSpawn.GetComponent<Item>().canInteract = true;
        Instantiate(objectSpawn, frontPosition, localRotation);
    }
    //==========================================================================================
    // Function, Find Next Available Index
    //------------------------------------------------------------------------------------------
    private void FindNextAvailableIndex(GameObject objectSpawn) {
        ItemStats itemStats = objectSpawn.GetComponent<Item>().item;
        int remaining = itemStats.quantity;
        // Top off the Existing Item Stack
        for (int i = 0; i < inventoryItems.Length && remaining > 0; i++) {
            if (IsSlotEmpty(i)) continue;
            if (inventoryItems[i].itemName != itemStats.itemName) continue;
            int space = itemStats.stackSize - inventoryItems[i].quantity;
            if (space <= 0) continue;
            int add = Mathf.Min(space, remaining);
            inventoryItems[i].quantity += add;
            remaining -= add;
        }
        // Assign to Empty Slots
        for (int i = 0; i < inventoryItems.Length && remaining > 0; i++) {
            if (!IsSlotEmpty(i)) continue;
            int add = Mathf.Min(remaining, itemStats.stackSize);
            inventoryItems[i] = CopyItem(itemStats, add);
            remaining -= add;
            currItemAmount++;
        }
    }
    //==========================================================================================
    // Function, Is Slot Empty
    //------------------------------------------------------------------------------------------
    private bool IsSlotEmpty(int index) {
        return inventoryItems[index] == null || string.IsNullOrEmpty(inventoryItems[index].itemName);
    }
    //==========================================================================================
    // Function, Copy Item
    //------------------------------------------------------------------------------------------
    private ItemStats CopyItem(ItemStats source, int qty) {
        return new ItemStats {
            itemName = source.itemName,
            itemDescription = source.itemDescription,
            icon = source.icon,
            weight = source.weight,
            quantity = qty,
            stackSize = source.stackSize,
            itemMesh = source.itemMesh,
            pickupSound = source.pickupSound,
            itemIncreases = source.itemIncreases
        };
    }
    //==========================================================================================
    // Function, Find Item
    //------------------------------------------------------------------------------------------
    public ItemStats FindItem(ItemStats item) {
        foreach (ItemStats searchItem in inventoryItems) {
            if (searchItem == item) {
                return searchItem;
            }
        }
        return null;
    }
    //==========================================================================================
    // Function, Check Item
    //------------------------------------------------------------------------------------------
    public bool CheckItem(ItemStats item) {
        foreach (ItemStats searchItem in inventoryItems) {
            if (searchItem == item) {
                // Do something
                return true;
            }
        }
        return false;
    }
    //==========================================================================================
    // Function, Adjust Stack Size
    //------------------------------------------------------------------------------------------
    private void AdjustStackSize() {

    }
    //==========================================================================================
}
//==============================================================================================
// End of Declare Inventory
//==============================================================================================