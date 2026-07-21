//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Rotate
//==============================================================================================
public class hingeRotator : MonoBehaviour, IInteract {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] Vector3 startPosition;
    [SerializeField] Vector3 endPosition;
    [SerializeField] int speed;
    [SerializeField] AudioClip[] audOpenSound;
    [Range(0, 1)] public float audOpenSoundVolume;
    [SerializeField] AudioClip[] audCloseSound;
    [Range(0, 1)] public float audCloseSoundVolume;
    private bool isOpen;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, Door Open
    //------------------------------------------------------------------------------------------
    IEnumerator hingeRotate() {
        Quaternion start = Quaternion.Euler(startPosition) * transform.rotation;
        Quaternion end = Quaternion.Euler(endPosition) * transform.rotation;
        while (transform.rotation != end) {
            if (isOpen) {
                transform.rotation = Quaternion.Lerp(transform.rotation, start, speed * Time.deltaTime);
            } else {
                transform.rotation = Quaternion.Lerp(transform.rotation, end, speed * Time.deltaTime);
            }
            yield return null;
        }
        isOpen = !isOpen;

    }
    //==========================================================================================
    // Function, Send Interact
    //==========================================================================================
    public void SendInteract(Collider target)
    {
        hingeRotate();
    }
    //==========================================================================================
}
//==============================================================================================
// End of Rotate CS
//==============================================================================================