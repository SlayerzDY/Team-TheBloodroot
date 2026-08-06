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
        // Safety Check, If not ItemStats Return
        if (objectSpawn.GetComponent<Item>().item == null) { return; }
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
    // Function, Spawn Item
    //------------------------------------------------------------------------------------------
    private void FindNextAvailableIndex(GameObject objectSpawn) {
        ItemStats itemStats = objectSpawn.GetComponent<Item>().item;
        for (int i = 0; i < inventoryItems.Length; i++) {
            if (inventoryItems[i] == null) {
                // Set Array Element to ItemStats and break
                inventoryItems[i] = itemStats;
                currItemAmount++;
                break;
            }
            if (inventoryItems[i].name == itemStats.name) {
                if ((itemStats.quantity + inventoryItems[i].quantity) > itemStats.stackSize) {
                    // Handle overflow
                    int cacheAmount = (itemStats.quantity + inventoryItems[i].quantity);
                    int stackSize = itemStats.stackSize;
                    inventoryItems[i] = itemStats;
                    inventoryItems[i].quantity = stackSize;
                    cacheAmount -= stackSize;
                    currItemAmount++;
                    while (cacheAmount > stackSize) {
                        for (int j = 0; j < inventoryItems.Length; j++) {
                            if (inventoryItems[j] == null) {
                                inventoryItems[j] = itemStats;
                                if (cacheAmount > stackSize) {
                                    inventoryItems[j].quantity = stackSize;
                                } else {
                                    inventoryItems[j].quantity = cacheAmount;
                                    break;
                                }
                                cacheAmount -= stackSize;
                                break;
                            }
                        }
                    }
                    break;
                }
            }
        }
    }
    //==========================================================================================
    // Function, Handle Overflow
    //------------------------------------------------------------------------------------------

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