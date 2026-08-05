//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Linq;
using UnityEngine;
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
    public void AddItem(ItemStats item, GameObject objectSpawn) {
        if (currItemAmount >= inventorySize) {
            Vector3 frontPosition = gameManager.instance.player.transform.position + (gameManager.instance.player.transform.forward * distance);
            Quaternion localRotation = gameManager.instance.player.transform.localRotation;
            objectSpawn.GetComponent<Item>().item = item;
            objectSpawn.GetComponent<Item>().canInteract = true;
            Instantiate(objectSpawn, frontPosition, localRotation);
            return;
        }
        for (int i = 0; i < inventoryItems.Length; i++) {
            if (inventoryItems[i] == null) {
                inventoryItems[i] = item;
                currItemAmount++;
                break;
            }
        }
    }
    //==========================================================================================
    // Function, Remove Item
    //------------------------------------------------------------------------------------------
    public void RemoveItem(ItemStats item) {

    }
    //==========================================================================================
    // Function, Assign Item
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