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
    [Header("Rotation Settings")]
    [SerializeField] Vector3 startPosition;
    [SerializeField] Vector3 endPosition;
    [SerializeField] int speed;
    [Header("Audio Settings")]
    [SerializeField] AudioClip[] audOpenSound;
    [Range(0, 1)] public float audOpenSoundVolume;
    [SerializeField] AudioClip[] audCloseSound;
    [Range(0, 1)] public float audCloseSoundVolume;
    private bool isOpen;
    private Coroutine activeRotationRoutine;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, Door Open
    //------------------------------------------------------------------------------------------
    IEnumerator hingeRotate() {
        Debug.Log("Interacted With Door");
        Quaternion targetRotation = Quaternion.Euler(isOpen ? endPosition : startPosition);

        // Smoothly rotate until we are practically at the target rotation
        while (Quaternion.Angle(transform.localRotation, targetRotation) > 0.01f)
        {
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                targetRotation,
                speed * 100f * Time.deltaTime
            );
            yield return null;
        }

        // Snap precisely to target at the end
        transform.localRotation = targetRotation;
        activeRotationRoutine = null;
    }
    //==========================================================================================
    // Function, Send Interact
    //==========================================================================================
    public void SendInteract(Collider target)
    {
        // Stop any active rotation to prevent weird overlapping movement
        if (activeRotationRoutine != null)
        {
            StopCoroutine(activeRotationRoutine);
        }

        // Toggle state and start coroutine
        isOpen = !isOpen;
        activeRotationRoutine = StartCoroutine(hingeRotate());
    }
    //==========================================================================================
}
//==============================================================================================
// End of Rotate CS
//==============================================================================================