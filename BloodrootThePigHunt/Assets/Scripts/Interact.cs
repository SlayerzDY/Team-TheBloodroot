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
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update() {
        if (Input.GetButton("Interact")) { TryToInteract(); }
        DisplayInteract(); 
    }
    //==========================================================================================
    // Function, Interact
    //==========================================================================================
    void IInteract.SendInteract(Collider target) {
        Debug.Log(gameObject.name);
    }
    //==========================================================================================
    // Function, TryToInteract
    //==========================================================================================
    void TryToInteract() {
        DrawRaycast();
        if (!IsInteractable()) { return; }
        IInteract interact = interactObject.collider.GetComponent<Interact>();
        if (interact != null) {
            interact.SendInteract(interactObject.collider);
        }
    }
    //==========================================================================================
    // Function, TryToInteract
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
        Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * InteractRange, Color.green);
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out interactObject, InteractRange, InteractLayer)) {
            Debug.Log(interactObject.collider.name);
            IInteract interact = interactObject.collider.GetComponent<Interact>();
        }
        
    }
    //==========================================================================================
    // Function, Is Interactable
    //==========================================================================================
    bool IsInteractable() {
        if (interactObject.collider == null) return false;
        if (!interactObject.collider.CompareTag("Interact")) return false;
        Interact interactComponent = interactObject.collider.GetComponent<Interact>();
        if (interactComponent == null || !interactComponent.enabled) { return false; }
        return true;
    }
    //==========================================================================================
}
//==============================================================================================
// Declare Interact
//==============================================================================================