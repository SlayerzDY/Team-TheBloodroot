//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Collections.Generic;
using UnityEngine;
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
 * itemPickup => The prefab that will be spawned when the item is picked up(This should 
 * be set to self!
 * 
 * Creating a new Hero Stats Scriptable Object!
 * 1.) Go to Folder Prefabs/Items/ScriptablePowerup/
 * 2.) Then copy Item_HeroStats_Master prefab
 * 3.) Paste your new Item_HeroStats prefab into the same folder
 * 4.) Fill out the ItemStats for the new Item_HeroStats prefab variant
 * 5.) Celebrate, you have created a new Item_HeroStats prefab variant
 * 
 * ***Comments Assisted by Gemini, Asked for this as my prompt***
 * Can you give me two lists. One with private and one with public functions and a short 
 * description of what each function does for my instructions. Head each entry with " * " 
 * that way I can just paste it in
 * ==========================================================================================
 * Public Functions
 * ==========================================================================================
 * Start(): Initializes the inventory array size based on the configured inspector settings 
 * when the script starts.
 * AddItem(GameObject objectSpawn): Validates and adds an item GameObject to the next 
 * available inventory slot or stacks it, spawning it into the world if full.
 * AddItem(GameObject objectSpawn, int index): Adds an item GameObject starting search or 
 * insertion at a specific inventory index.
 * RemoveItem(GameObject item, bool spawnItem = true): Removes an item from the inventory 
 * using its world GameObject reference and optionally spawns a drop.
 * RemoveItem(GameObject item, bool spawnItem = true, int index = 0): Removes an item from 
 * a specific inventory index using its world GameObject reference and optional drop behavior.
 * RemoveItem(string name, int amount = 0, bool spawnItem = true, int index = 0): Master removal 
 * method 
 * by item name, quantity, drop toggle, and start index to clear inventory amounts cleanly.
 * KillItem(string itemName, int amount, bool spawnItem = true): Reduces or clears item quantities 
 * from the inventory by name across all matching slots.
 * KillItem(string itemName, int amount, bool spawnItem, int index = 0): Overload to reduce or 
 * clear item quantities starting from a specified index position.
 * RemoveMultipleItems(GameObject item, bool spawnItem = true): Validates and removes the total 
 * accumulated quantity of a specified item GameObject from the inventory in a single pass.
 * RemoveMultipleItems(string item, bool spawnItem = true): Validates and removes the total 
 * accumulated quantity of a specified item by its string name from the inventory in a single pass.
 * IsSlotEmpty(int index): Checks if a specific inventory slot index is null or contains an empty 
 * item name.
 * IsSlotNotEmpty(int index): Checks if a specific inventory slot index contains a valid item.
 * IsValidIndex(int index): Validates whether an index falls within the active bounds of the 
 * inventory array.
 * IsNotValidIndex(int index): Validates whether an index falls outside the active bounds of the 
 * inventory array.
 * IsValidIndex(GameObject target, int index = 0): Validates if a target GameObject has a valid 
 * item and points to an empty inventory slot at the specified index.
 * IsNotValidIndex(GameObject target, int index = 0): Validates if a target GameObject lacks a 
 * valid item or points to a non-empty slot.
 * FindItem(ItemStats item): Searches the inventory for an item matching the given ItemStats 
 * and returns its data instance.
 * FindItem(ItemStats item): Searches the inventory using an ItemStats reference, accumulates 
 * the total quantity of matching items, and returns a key-value pair of the item and its total 
 * count.
 * FindItem(GameObject objectRef): Searches the inventory using a world GameObject reference, 
 * extracts its item stats, accumulates the total matching quantity, and returns a key-value 
 * pair of the item and its total count.
 * CheckItem(ItemStats item): Returns true if the specified ItemStats exists anywhere in the 
 * inventory array.
 * ==========================================================================================
 * Private Functions
 * ==========================================================================================
 * SpawnItem(ItemStats objectSpawn): Instantiates a physical pickup world object in front of 
 * the player using the provided item statistics.
 * FindNextAvailableIndex(GameObject objectSpawn): Handles stacking items onto existing matches 
 * or placing them into the next open inventory slot.
 * FindNextAvailableIndex(GameObject objectSpawn, int index = 0): Handles stacking or placing 
 * items starting the search from a designated slot index.
 * CopyItem(ItemStats source, int qty): Clones an existing ItemStats object with a new specified 
 * quantity to safely store runtime variations in the inventory array.
 * ***Comments by Gemini Concluded***
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
    private KeyValuePair<ItemStats, int> currItemQuantity;
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
    public void AddItem(GameObject objectSpawn, int index) {
        // Safety Check, If not Item Return
        if (objectSpawn.GetComponent<Item>() == null) { return; }
        if (currItemAmount >= inventorySize) {
            SpawnItem(objectSpawn.GetComponent<Item>().item);
            return;
        }
        FindNextAvailableIndex(objectSpawn, index);
    }
    //==========================================================================================
    // Function, Add Item
    //------------------------------------------------------------------------------------------
    public void AddItem(ItemStats item) {
        // Safety Check, If not Item Return
        if (item.itemName == null) { return; }
        if (currItemAmount >= inventorySize) {
            SpawnItem(item);
            return;
        }
        // Execute Add Item Function
        FindNextAvailableIndex(item);
    }
    //==========================================================================================
    // Function, Remove Item    
    //------------------------------------------------------------------------------------------
    public void RemoveItem(GameObject item, bool spawnItem = true) {
        // Safety Check, If not Item Return
        ItemStats itemStats = item.GetComponent<Item>()?.item;
        if (itemStats == null) { return; }
        if (spawnItem) { SpawnItem(item.GetComponent<Item>().item); }
        // Execute Remove Item Function
        KillItem(itemStats.itemName, itemStats.quantity, spawnItem);
    }
    //==========================================================================================
    // Overload Function, Remove Item By Index
    //------------------------------------------------------------------------------------------
    public void RemoveItem(GameObject item, bool spawnItem = true, int index = 0) {
        // Safety Check, If not Item Return
        ItemStats itemStats = item.GetComponent<Item>()?.item;
        if (itemStats == null) { return; }
        if (spawnItem) { SpawnItem(item.GetComponent<Item>().item); }
        // Execute Remove Item Function
        KillItem(itemStats.itemName, itemStats.quantity, spawnItem, index);
    }
    //==========================================================================================
    // Overload Function, Remove Item By String Name and Amount
    //------------------------------------------------------------------------------------------
    public void RemoveItem(string name, int amount = 0, bool spawnItem = true) {
        // Safety Check, If not Item Return
        if (name == null) { return; }
        if (amount <= 0) { return; }
        // Execute Remove Item Function
        KillItem(name, amount, spawnItem);
    }
    //==========================================================================================
    // Overload Function, Remove Item By String Name and Amount and Index
    //------------------------------------------------------------------------------------------
    public void RemoveItem(string name, int amount = 0, bool spawnItem = true, int index = 0) {
        // Safety Check, If not Item Return
        if (name == null) { return; }
        if (amount <= 0) { return; }
        // Execute Remove Item Function
        KillItem(name, amount, spawnItem, index);
    }
    //==========================================================================================
    // Function, Remove Multiple Items
    //------------------------------------------------------------------------------------------
    public bool RemoveMultipleItems(GameObject item, bool spawnItem = true) {
        // Safety Check, If not Item Return
        ItemStats itemTest = item.GetComponent<Item>()?.item;
        if (itemTest == null) { return false; }
        currItemQuantity = FindItem(item);
        if (currItemQuantity.Key == null) { return false; }
        if (currItemQuantity.Value <= 0) { return false; }
        // Execute Remove Item Function
        KillItem(currItemQuantity.Key.itemName, currItemQuantity.Value, spawnItem);
        return true;
    }
    //==========================================================================================
    // Overload Function, Remove Multiple Items (by name)
    //------------------------------------------------------------------------------------------
    public bool RemoveMultipleItems(string item, bool spawnItem = true) {
        // Safety Check, If not Item Return
        ItemStats itemTest = new ItemStats();
        itemTest.itemName = item;
        if (itemTest == null) { return false; }
        currItemQuantity = FindItem(itemTest);
        if (currItemQuantity.Key == null) { return false; }
        if (currItemQuantity.Value <= 0) { return false; }
        // Execute Remove Item Function
        KillItem(currItemQuantity.Key.itemName, currItemQuantity.Value, spawnItem);
        return true;
    }
    //==========================================================================================
    // Function, Transfer To New Inventory
    //------------------------------------------------------------------------------------------
    public bool TransferToNewInventory(GameObject newInventory, GameObject item) {
        // Safety Check, If not Item Return
        Inventory newInv = newInventory.GetComponent<Inventory>();
        if (newInv.inventoryItems.Length <= 0) { return false; }
        ItemStats itemStats = item.GetComponent<Item>()?.item; 
        if (itemStats.itemName == null) { return false; }
        currItemQuantity = FindItem(item);
        if (currItemQuantity.Key == null) { return false; }
        if (currItemQuantity.Value <= 0) { return false; }
        // Remove Item from our Inventory
        KillItem(currItemQuantity.Key.itemName, currItemQuantity.Value, false);
        // Add Item to New Inventory
        newInv.AddItem(item);
        return true;
    }
    //==========================================================================================
    // Overload Function, Transfer To New Inventory (ItemStats)
    //------------------------------------------------------------------------------------------
    public bool TransferToNewInventory(GameObject newInventory, ItemStats item) {
        // Safety Check, If not Item Return
        Inventory newInv = newInventory.GetComponent<Inventory>();
        if (newInv.inventoryItems.Length <= 0) { return false; }
        if (item.itemName == null) { return false; }
        currItemQuantity = FindItem(item);
        if (currItemQuantity.Key == null) { return false; }
        if (currItemQuantity.Value <= 0) { return false; }
        // Remove Item from our Inventory
        KillItem(currItemQuantity.Key.itemName, currItemQuantity.Value, false);
        // Add Item to New Inventory
        newInv.AddItem(item);
        return true;
    }
    //==========================================================================================
    // Overload Function, Transfer To New Inventory (String)
    //------------------------------------------------------------------------------------------
    public bool TransferToNewInventory(GameObject newInventory, string item){
        // Safety Check, If not Item Return
        Inventory newInv = newInventory.GetComponent<Inventory>();
        if (newInv == null || newInv.inventoryItems.Length <= 0) { return false; }
        if (string.IsNullOrEmpty(item)) { return false; }
        ItemStats newItem = new ItemStats { itemName = item };
        currItemQuantity = FindItem(newItem);
        if (currItemQuantity.Key == null) { return false; }
        if (currItemQuantity.Value <= 0) { return false; }
        // Remove Item from our Inventory
        KillItem(currItemQuantity.Key.itemName, currItemQuantity.Value, false);
        ItemStats transferData = CopyItem(currItemQuantity.Key, currItemQuantity.Value);
        // Add Item to New Inventory
        newInv.AddItem(transferData);
        return true;
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
    public void KillItem(string itemName, int amount, bool spawnItem, int index = 0) {
        int remaining = amount;
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++) {
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
    // Function, Spawn Item
    //------------------------------------------------------------------------------------------
    private void SpawnItem(ItemStats objectSpawn) {
        Vector3 frontPosition = gameManager.instance.player.transform.position + (gameManager.instance.player.transform.forward * distance);
        Quaternion localRotation = gameManager.instance.player.transform.localRotation;
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
    // Overload Function, Find Next Available Index Start Index
    //------------------------------------------------------------------------------------------
    private void FindNextAvailableIndex(GameObject objectSpawn, int index = 0) {
        ItemStats itemStats = objectSpawn.GetComponent<Item>().item;
        int remaining = itemStats.quantity;
        // Top off the Existing Item Stack
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++) {
            if (IsSlotEmpty(i)) continue;
            if (inventoryItems[i].itemName != itemStats.itemName) continue;
            int space = itemStats.stackSize - inventoryItems[i].quantity;
            if (space <= 0) continue;
            int add = Mathf.Min(space, remaining);
            inventoryItems[i].quantity += add;
            remaining -= add;
        }
        // Assign to Empty Slots
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++) {
            if (!IsSlotEmpty(i)) continue;
            int add = Mathf.Min(remaining, itemStats.stackSize);
            inventoryItems[i] = CopyItem(itemStats, add);
            remaining -= add;
            currItemAmount++;
        }
    }
    //==========================================================================================
    // Overload Function, Find Next Available Index Start Index by ItemStats
    //------------------------------------------------------------------------------------------
    private void FindNextAvailableIndex(ItemStats itemStats, int index = 0) {
        int remaining = itemStats.quantity;
        // Top off the Existing Item Stack
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++) {
            if (IsSlotEmpty(i)) continue;
            if (inventoryItems[i].itemName != itemStats.itemName) continue;
            int space = itemStats.stackSize - inventoryItems[i].quantity;
            if (space <= 0) continue;
            int add = Mathf.Min(space, remaining);
            inventoryItems[i].quantity += add;
            remaining -= add;
        }
        // Assign to Empty Slots
        for (int i = index; i < inventoryItems.Length && remaining > 0; i++) {
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
        // Safety Check, If not Item Return
        if (IsNotValidIndex(index)) return true;
        return inventoryItems[index] == null || string.IsNullOrEmpty(inventoryItems[index].itemName);
    }
    //==========================================================================================
    // Function, Is Slot Not Empty
    //------------------------------------------------------------------------------------------
    public bool IsSlotNotEmpty(int index) {
        // Safety Check, If not Item Return
        if (IsNotValidIndex(index)) return false;
        return inventoryItems[index] != null && !string.IsNullOrEmpty(inventoryItems[index].itemName);
    }
    //==========================================================================================
    // Function, Is Valid Index
    //------------------------------------------------------------------------------------------
    public bool IsValidIndex(int index) {
        // Safety Check, If not Item Return
        return index >= 0 && index < inventoryItems.Length;
    }
    //==========================================================================================
    // Function, Is Not Valid Index
    //------------------------------------------------------------------------------------------
    public bool IsNotValidIndex(int index) {
        // Safety Check, If not Item Return
        return index < 0 || index >= inventoryItems.Length;
    }
    //==========================================================================================
    // Overload Function, Is Valid Item
    //------------------------------------------------------------------------------------------
    public bool IsValidIndex(GameObject target, int index = 0) {
        // Safety Check, If not Item Return
        if (target.GetComponent<Item>() == null) { return false; }
        if (target.GetComponent<Item>().item.itemName == null) { return false; }
        if (!IsSlotEmpty(index)) { return false; }
        // Otherwise Guess Is Item
        return true;
    }
    //==========================================================================================
    // Overload Function, Is Valid Item
    //------------------------------------------------------------------------------------------
    public bool IsNotValidIndex(GameObject target, int index = 0) {
        // Safety Check, If Item Return
        if (target.GetComponent<Item>() == null) { return true; }
        if (target.GetComponent<Item>().item.itemName == null) { return true; }
        if (!IsSlotEmpty(index)) { return true; }
        // Otherwise Guess Is Not Item
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
    // Function, Find Item Item Stats Reference
    //------------------------------------------------------------------------------------------
    public KeyValuePair<ItemStats, int> FindItem(ItemStats item) {
        // Safety Check, If not Item Return
        if (IsNotValidIndex(0) || item == null) { return new KeyValuePair<ItemStats, int>(null, 0); }
        ItemStats foundItem = null;
        int foundQuantity = 0;
        for (int i = 0; i < inventoryItems.Length; i++) {
            if (IsSlotEmpty(i)) { continue; }
            if (inventoryItems[i].itemName == item.itemName) {
                if (foundItem == null) {
                    foundItem = inventoryItems[i];
                }
                foundQuantity += inventoryItems[i].quantity;
            }
        }
        currItemQuantity = new KeyValuePair<ItemStats, int>(foundItem, foundQuantity);
        return currItemQuantity;
    }
    //==========================================================================================
    // Overload Function, Find Item Object Reference
    //------------------------------------------------------------------------------------------
    public KeyValuePair<ItemStats, int> FindItem(GameObject objectRef) {
        // Safety Check, If not Item Return
        if (IsNotValidIndex(0) || objectRef == null) { return new KeyValuePair<ItemStats, int>(null, 0); }
        ItemStats item = objectRef.GetComponent<Item>()?.item;
        if (item == null || item.itemName == null) { return new KeyValuePair<ItemStats, int>(null, 0); }
        ItemStats foundItem = null;
        int foundQuantity = 0;
        for (int i = 0; i < inventoryItems.Length; i++) {
            if (IsSlotEmpty(i)) { continue; }
            if (inventoryItems[i].itemName == item.itemName) {
                if (foundItem == null) {
                    foundItem = inventoryItems[i];
                }
                foundQuantity += inventoryItems[i].quantity;
            }
        }
        currItemQuantity = new KeyValuePair<ItemStats, int>(foundItem, foundQuantity);
        return currItemQuantity;
    }
    //==========================================================================================
    // Function, Find Item Index
    //------------------------------------------------------------------------------------------
    public int FindItem(ItemStats item, int index = 0) {
        // Safety Check, If not Item Return
        if (item == null) { return -1; }
        if (item.itemName == null) { return -1; }
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
        // Safety Check, If not Item Return
        if (item == null) { return false; }
        if (item.itemName == null) { return false; }
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