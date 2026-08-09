//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Linq;
using UnityEngine;
using static UnityEditor.Progress;
//==============================================================================================
// Instructions for Using the Inventory
//==============================================================================================
/* 
 * Instructions for using the Inventory Script:
 * Creating a new Item!
 * 1.) Go to Folder Prefabs/Items/ItemPickups/
 * 2.) Then right click on Item_Pickup_Master prefab
 * 3.) Select Create => Prefab Variant
 * 4.) Fill out the ItemStats for the new ItemPickup prefab variant
 * 5.) Celebrate, you have created a new ItemPickup prefab variant
 * 
 * itemName => The name of the item
 * itemDescription => The description of the item
 * icon => The icon of the item
 * weight => The weight of the item
 * int quantity => The quantity of the item
 * int stackSize => The stack size of the item
 * itemMesh => The mesh of the item(will be used to display the item in the world)
 * pickupSound => The sound that will play when the item is picked up
 * itemIncreases => The hero stats that will be increased when the item is picked up
 * itemPickup => The prefab that will be spawned when the item is picked up(This should be set to self!
 * 
 * Creating a new Hero Stats Scriptable Object!
 * 1.) Go to Folder Prefabs/Items/ScriptablePowerup/
 * 2.) Then copy Item_HeroStats_Master prefab
 * 3.) Paste your new Item_HeroStats prefab into the same folder
 * 4.) Fill out the ItemStats for the new Item_HeroStats prefab variant
 * 5.) Celebrate, you have created a new Item_HeroStats prefab variant
*/
//==============================================================================================
// Declare Inventory
//==============================================================================================
public class Inventory : MonoBehaviour {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    // Public
    // Item Inventory Array
    [SerializeField] public float inventoryWeight = 0.0f;
    [Range(0f, 300f)][SerializeField] public float weightThreshold = 150.0f;
    [Range(1, 100)] [SerializeField] int inventorySize;
    [SerializeField] public ItemStats[] inventoryItems;
    [Range(1, 100)][SerializeField] float distance = 2.0f;
    // Private
    private ItemStats currItem;
    private ItemStats tempItem;
    private int currItemAmount;
    [SerializeField] private GameObject genericPickupShell;
    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Function, Start
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
            SpawnItem(objectSpawn.GetComponent<Item>().item);
            return;
        }
        FindNextAvailableIndex(objectSpawn);
    }
    //==========================================================================================
    // Overload Function, Add Item (Index)
    //------------------------------------------------------------------------------------------
    public void AddItem(GameObject objectSpawn, int index)
    {
        // Safety Check, If not Item Return
        if (objectSpawn.GetComponent<Item>() == null) { return; }
        if (currItemAmount >= inventorySize)
        {
            SpawnItem(objectSpawn.GetComponent<Item>().item);
            return;
        }
        FindNextAvailableIndex(objectSpawn, index);
    }
    //==========================================================================================
    // Function, Remove Item    
    //------------------------------------------------------------------------------------------
    public void RemoveItem(GameObject item, bool spawnItem = true) {
        ItemStats itemStats = item.GetComponent<Item>()?.item;
        if (itemStats == null) { return; }
        if (spawnItem) { SpawnItem(item.GetComponent<Item>().item); }
        KillItem(itemStats.itemName, itemStats.quantity, spawnItem);
    }
    //==========================================================================================
    // Overload Function, Remove Item By Index
    //------------------------------------------------------------------------------------------
    public void RemoveItem(GameObject item, bool spawnItem = true, int index = 0)
    {
        ItemStats itemStats = item.GetComponent<Item>()?.item;
        if (itemStats == null) { return; }
        if (spawnItem) { SpawnItem(item.GetComponent<Item>().item); }
        KillItem(itemStats.itemName, itemStats.quantity, spawnItem, index);
    }
    //==========================================================================================
    // Overload Function, Remove Item By String Name and Amount
    //------------------------------------------------------------------------------------------
    public void RemoveItem(string name, int amount = 0, bool spawnItem = true) {
        if (name == null) { return; }
        if (amount == 0) { return; }
        KillItem(name, amount, spawnItem);
    }
    //==========================================================================================
    // Overload Function, Remove Item By String Name and Amount and Index
    //------------------------------------------------------------------------------------------
    public void RemoveItem(string name, int amount = 0, bool spawnItem = true, int index = 0) {
        if (name == null) { return; }
        if (amount == 0) { return; }
        KillItem(name, amount, spawnItem, index);
    }
    //==========================================================================================
    // Function, Kill Item (by name/amount — for crafting, consuming, etc.)
    //------------------------------------------------------------------------------------------
    public void KillItem(string itemName, int amount, bool spawnItem = true) {
        int remaining = amount;
        for (int i = 0; i < inventoryItems.Length && remaining > 0; i++) {
            if (IsSlotEmpty(i)) continue;
            if (inventoryItems[i].itemName != itemName) continue;
            if (spawnItem) { SpawnItem(inventoryItems[i]); }
            int take = Mathf.Min(remaining, inventoryItems[i].quantity);
            inventoryItems[i].quantity -= take;
            remaining -= take;
            if (inventoryItems[i].quantity <= 0) {
                inventoryItems[i] = null;
                currItemAmount--;
            }
        }
    }
    //==========================================================================================
    // Overload Function, Kill Item Index
    //------------------------------------------------------------------------------------------
    public void KillItem(string itemName, int amount, bool spawnItem, int index = 0)
    {
        int remaining = amount;
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++)
        {
            if (IsSlotEmpty(i)) continue;
            if (inventoryItems[i].itemName != itemName) continue;
            if (spawnItem) { SpawnItem(inventoryItems[i]); }
            int take = Mathf.Min(remaining, inventoryItems[i].quantity);
            inventoryItems[i].quantity -= take;
            remaining -= take;
            if (inventoryItems[i].quantity <= 0)
            {
                inventoryItems[i] = null;
                currItemAmount--;
            }
        }
    }
    //==========================================================================================
    // Function, Spawn Item
    //------------------------------------------------------------------------------------------
    private void SpawnItem(ItemStats objectSpawn) {
        Vector3 frontPosition = gameManager.instance.player.transform.position + (gameManager.instance.player.transform.forward * distance);
        Quaternion localRotation = gameManager.instance.player.transform.localRotation;
        //objectSpawn.GetComponent<Item>().item = item;
        genericPickupShell.GetComponent<Item>().item = CopyItem(objectSpawn, objectSpawn.quantity);
        GameObject newPickup = Instantiate(genericPickupShell, frontPosition, localRotation);
        newPickup.GetComponent<Item>().canInteract = true;
        newPickup.GetComponent<Item>().ApplyMeshToSelf();
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
    // Function, Find Next Available Index Start Index
    //------------------------------------------------------------------------------------------
    private void FindNextAvailableIndex(GameObject objectSpawn, int index = 0)
    {
        ItemStats itemStats = objectSpawn.GetComponent<Item>().item;
        int remaining = itemStats.quantity;
        // Top off the Existing Item Stack
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++)
        {
            if (IsSlotEmpty(i)) continue;
            if (inventoryItems[i].itemName != itemStats.itemName) continue;
            int space = itemStats.stackSize - inventoryItems[i].quantity;
            if (space <= 0) continue;
            int add = Mathf.Min(space, remaining);
            inventoryItems[i].quantity += add;
            remaining -= add;
        }
        // Assign to Empty Slots
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++)
        {
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
    public bool IsSlotEmpty(int index) {
        return inventoryItems[index] == null || string.IsNullOrEmpty(inventoryItems[index].itemName);
    }
    //==========================================================================================
    // Function, Is Slot Not Empty
    //------------------------------------------------------------------------------------------
    public bool IsSlotNotEmpty(int index)
    {
        return inventoryItems[index] != null && !string.IsNullOrEmpty(inventoryItems[index].itemName);
    }
    //==========================================================================================
    // Function, Is Valid Index
    //------------------------------------------------------------------------------------------
    public bool IsValidIndex(int index) {
        return index >= 0 && index < inventoryItems.Length;
    }
    //==========================================================================================
    // Function, Is Not Valid Index
    //------------------------------------------------------------------------------------------
    public bool IsNotValidIndex(int index) {
        return index < 0 || index >= inventoryItems.Length;
    }
    //==========================================================================================
    // Overload Function, Is Valid Item
    //------------------------------------------------------------------------------------------
    public bool IsValidIndex(GameObject target, int index = 0) {
        if (target.GetComponent<Item>() == null) { return false; }
        if (target.GetComponent<Item>().item.itemName == null) { return false; }
        if (!IsSlotEmpty(index)) { return false; }
        return true;
    }
    //==========================================================================================
    // Overload Function, Is Valid Item
    //------------------------------------------------------------------------------------------
    public bool IsNotValidIndex(GameObject target, int index = 0) {
        if (target.GetComponent<Item>() == null) { return true; }
        if (target.GetComponent<Item>().item.itemName == null) { return true; }
        if (!IsSlotEmpty(index)) { return true; }
        return false;
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
        if (IsNotValidIndex(0)) { return null; }
        for (int i = 0; i < inventoryItems.Length; i++) {
            if (IsSlotEmpty(i)) { continue; }
            if (inventoryItems[i].itemName == item.itemName) {
                return inventoryItems[i];
            }
        }
        return null;
    }
    //==========================================================================================
    // Function, Find Item Index
    //------------------------------------------------------------------------------------------
    public int FindItem(ItemStats item, int index = 0) {
        if (IsNotValidIndex(index)) { return -1; }
        for (int i = index; i < inventoryItems.Length; i++) {
            if (IsSlotEmpty(i)) { continue; }
            if (inventoryItems[i].itemName == item.itemName) {
                return i;
            }
        }
        return -1;
    }
    //==========================================================================================
    // Function, Check Item
    //------------------------------------------------------------------------------------------
    public bool CheckItem(ItemStats item) {
        if (IsNotValidIndex(0)) { return false; }
        for (int i = 0; i < inventoryItems.Length; i++) {
            if (IsSlotEmpty(i)) { continue; }
            if (inventoryItems[i].itemName == item.itemName) {
                // Do something
                return true;
            }
        }
        return false;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Declare Inventory
//==============================================================================================