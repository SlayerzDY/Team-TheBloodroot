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
    private Quaternion initialRotation;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, Awake
    //------------------------------------------------------------------------------------------
    private void Awake() {
        initialRotation = transform.localRotation;
    }
    //==========================================================================================
    // Function, Door Open
    //------------------------------------------------------------------------------------------
    IEnumerator hingeRotate() {
        Quaternion closedRot = initialRotation * Quaternion.Euler(startPosition);
        Quaternion openRot = initialRotation * Quaternion.Euler(endPosition);
        Quaternion targetRotation = isOpen ? openRot : closedRot;
        while (Quaternion.Angle(transform.localRotation, targetRotation) > 0.01f)
        {
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                targetRotation,
                speed * 100f * Time.deltaTime
            );
            yield return null;
        }
        transform.localRotation = targetRotation;
        activeRotationRoutine = null;
    }
    //==========================================================================================
    // Function, Send Interact
    //==========================================================================================
    public void SendInteract(Collider target) {
        if (activeRotationRoutine != null){
            StopCoroutine(activeRotationRoutine);
        }
        isOpen = !isOpen;
        activeRotationRoutine = StartCoroutine(hingeRotate());
    }
    //==========================================================================================
}
//==============================================================================================
// End of Rotate CS
//==============================================================================================