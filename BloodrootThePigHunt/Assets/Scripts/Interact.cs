//==============================================================================================
// Using Unity Engine
//==============================================================================================
using Unity.VisualScripting;
using UnityEngine;
//==============================================================================================
// Declare Interact
//==============================================================================================
public class Interact : MonoBehaviour, IInteract {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] int InteractRange;
    [SerializeField] LayerMask InteractLayer;
    RaycastHit interactObject;
    RaycastHit prevInteractObject;
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update() {
        if (Input.GetButtonDown("Interact")) { TryToInteract(); }
        if (!Input.GetButtonDown("Interact")) DisplayInteract();
    }
    //==========================================================================================
    // Function, Interact
    //==========================================================================================
    void IInteract.SendInteract(Collider target) {
        // Do Nothing
    }
    //==========================================================================================
    // Function, TryToInteract
    //==========================================================================================
    void TryToInteract() {
        DrawRaycast();
        if (!IsInteractable()) { return; }
        interactObject.collider.gameObject.SendMessage("SendInteract", interactObject.collider, SendMessageOptions.DontRequireReceiver);
    }

    //==========================================================================================
    // Function, DisplayInteract
    //==========================================================================================
    void DisplayInteract() {
        DrawRaycast();
        if (!IsInteractable()) {
            if (gameManager.instance != null) { gameManager.instance.InteractDisplay(false); }
            return;
        }
        if (IsInteractable()) {
            if (gameManager.instance != null) { gameManager.instance.InteractDisplay(true); }
            return;
        }
    }
    //==========================================================================================
    // Function, Draw Raycast
    //==========================================================================================
    void DrawRaycast() {
        //Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * InteractRange, Color.green);
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out interactObject, InteractRange, InteractLayer)) {
            // Debug.Log(interactObject.collider.name);
        } else {
            interactObject = new RaycastHit();
        }
    }
    //==========================================================================================
    // Function, Is Interactable
    //==========================================================================================
    bool IsInteractable() {
        if (interactObject.collider == null) return false;
        if (!interactObject.collider.gameObject.CompareTag("Interact")) return false;
        if (!interactObject.collider.CompareTag("Interact")) return false;
        IInteract interactComponent = interactObject.collider.GetComponent<IInteract>();
        if (interactComponent == null) { return false; }
        return true;
    }
    //==========================================================================================
}
//==============================================================================================
// Declare Interact
//==============================================================================================