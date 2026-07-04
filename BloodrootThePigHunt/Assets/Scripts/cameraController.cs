//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
//==============================================================================================
// Declare Camera Controller
//==============================================================================================
public class cameraController : MonoBehaviour {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] int sens;
    [SerializeField] int lockVertMin, lockVertMax;
    float camRotX, camRotY;
    //==========================================================================================
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //==========================================================================================
    void Start() {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    //==========================================================================================
    // Update is called once per frame 
    //==========================================================================================
    void Update() { 
       if (!gameManager.instance.isPaused) {
            float mouseX = Input.GetAxisRaw("Mouse X") * sens;
            float mouseY = Input.GetAxisRaw("Mouse Y") * sens;
            camRotX -= mouseY;
            camRotX = Mathf.Clamp(camRotX, lockVertMin, lockVertMax);
            // flight controls if you use others?
            transform.localRotation = Quaternion.Euler(camRotX, 0, 0);
            transform.parent.Rotate(Vector3.up * mouseX);
        }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Camera Controller .cs
//==============================================================================================