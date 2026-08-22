//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
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
    //==========================================================================================
    // Function, Validate (Construction Script)
    //------------------------------------------------------------------------------------------
    private void OnValidate() {
        ApplyMeshToSelf();
        canInteract = true;
        //if (item != null && item.itemPickup == null &&
        //    UnityEditor.PrefabUtility.GetPrefabAssetType(gameObject) == UnityEditor.PrefabAssetType.Regular &&
        //    !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject))
        //{
        //    item.itemPickup = gameObject;
        //}
    }
    private void OnTriggerEnter(Collider other) {
        Inventory pick = other.GetComponent<Inventory>();
        if (pick == null)
        {
            pick = other.GetComponentInParent<Inventory>();
        }
        SendInteract(this.GetComponent<Collider>());
    }
    //==========================================================================================
    // Function, Start
    //------------------------------------------------------------------------------------------
    public void Start() {
        //if (this.GetComponent<Dissolver>() == null) return;
        //if (this.GetComponent<Dissolver>() != null) { this.GetComponent<Dissolver>().StartCoroutine(this.GetComponent<Dissolver>().dissolveReturn()); }
    }
    //==========================================================================================
    // Function, Apply Mesh to Self
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
        //if (target.GetComponent<Item>() == null) { return; }
        if (gameManager.instance != null && gameManager.instance.player != null) {
            Inventory playerInv = gameManager.instance.player.GetComponent<Inventory>();
            if (playerInv == null) { return; }
            playerInv.AddItem(target.gameObject);
            SphereCollider[] spheres = GetComponents<SphereCollider>();
            foreach (SphereCollider s in spheres) {
                s.enabled = false;
            }
            if (target.GetComponent<Dissolver>() != null) { target.GetComponent<Dissolver>().StartCoroutine(target.GetComponent<Dissolver>().dissolve()); }
        }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Declare Item
//==============================================================================================