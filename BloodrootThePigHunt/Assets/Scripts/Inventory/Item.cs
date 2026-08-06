//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
//==============================================================================================
// Declare Item
//==============================================================================================
public class Item : MonoBehaviour, IInteract {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] public ItemStats item;
    public bool canInteract;
    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Start
    //------------------------------------------------------------------------------------------
    private void OnValidate() {
        ApplyMeshToSelf();
        canInteract = true;
    }
    //==========================================================================================
    // Start
    //------------------------------------------------------------------------------------------
    public void ApplyMeshToSelf() {
        MeshFilter myFilter = GetComponent<MeshFilter>();
        MeshRenderer myRenderer = GetComponent<MeshRenderer>();
        if (item.itemMesh != null) {
            MeshFilter sourceFilter = item.itemMesh.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = item.itemMesh.GetComponent<MeshRenderer>();
            if (myFilter != null && sourceFilter != null) { myFilter.sharedMesh = sourceFilter.sharedMesh; }
            if (myRenderer != null && sourceRenderer != null) { myRenderer.sharedMaterials = sourceRenderer.sharedMaterials; }
        }
    }
    //==========================================================================================
    // Function, Send Interact
    //------------------------------------------------------------------------------------------
    public void SendInteract(Collider target) {
        if (!canInteract) { return; }
        // If the Interact Target Doesn't have Item Stats, Return
        canInteract = false;
        if (target.GetComponent<Item>() == null) { return; }
        if (gameManager.instance != null && gameManager.instance.player != null) {
            Inventory playerInv = gameManager.instance.player.GetComponent<Inventory>();
            if (playerInv == null) { return; }
            playerInv.AddItem(target.gameObject);
            if (target.GetComponent<Dissolver>() != null) { target.GetComponent<Dissolver>().StartCoroutine(target.GetComponent<Dissolver>().dissolve()); }
        }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Declare Item
//==============================================================================================